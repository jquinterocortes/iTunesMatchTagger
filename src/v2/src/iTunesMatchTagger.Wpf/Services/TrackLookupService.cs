using System.Net.Http;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Sources;
using iTunesMatchTagger.Core.Tracks;
using iTunesMatchTagger.Wpf.ViewModels;

namespace iTunesMatchTagger.Wpf.Services;

/// <summary>
/// Per-track lookup orchestration: Apple first (embedded catalog ID across
/// the configured storefronts, then every other storefront, then a tag
/// search), then the enabled tag sources in fallback order, and artwork
/// previews at the end. Ported from the WinForms MainForm.LookupRowAsync.
/// </summary>
public static class TrackLookupService
{
    public delegate void LogSink(string message, StatusKind severity = StatusKind.Neutral);

    public sealed record LookupOutcome(
        IReadOnlyList<TagCandidate> Candidates,
        string FoundIn,
        bool FoundViaSearch,
        long? AppleTrackId,
        int AutoSelectIndex,
        Dictionary<int, byte[]> ArtworkPreviews);

    public static async Task<LookupOutcome> LookupRowAsync(
        TrackRowViewModel row,
        ITunesSearchClient search,
        HttpClient artworkHttp,
        IReadOnlyList<string> countries,
        IReadOnlyList<ITagSource> fallbackSources,
        bool alwaysQueryAll,
        ISet<string> skippedSources,
        LogSink log,
        CancellationToken cancellationToken)
    {
        // Stage 1 - Apple first: the embedded catalog ID across storefronts.
        ITunesLookupResult? appleResult = null;
        var foundIn = string.Empty;
        var viaSearch = false;

        if (row.Track.TrackId > 0)
        {
            foreach (var country in countries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var found = await search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                if (found is not null)
                {
                    log($"Track ID {row.Track.TrackId} found in {country}", StatusKind.Neutral);
                    appleResult = found;
                    foundIn = country;
                    break;
                }
            }

            if (appleResult is null)
            {
                // The embedded ID is dead in every configured storefront (Apple
                // delists albums; the file keeps the old ID). Sweep the
                // remaining storefronts before giving up - regional catalogs
                // differ, so a delisted album may still live somewhere else.
                log($"Track ID {row.Track.TrackId} not found in the configured countries, sweeping the other storefronts", StatusKind.Neutral);
                var tried = countries.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var country in StoreCountries.All.Where(c => !tried.Contains(c)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var found = await search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                    if (found is not null)
                    {
                        appleResult = found;
                        foundIn = country;
                        break;
                    }
                }
            }

            if (appleResult is null)
            {
                // The ID is dead everywhere. Fall back to searching by the
                // current tags - re-released albums come back under a new ID
                // that an ID lookup would never find.
                log($"Track ID {row.Track.TrackId} not found in any storefront, trying search by current tags", StatusKind.Neutral);
            }
        }

        // Stage 2 - Apple term search across storefronts.
        if (appleResult is null && countries.Count > 0)
        {
            var term = $"{row.GetCurrent("artistName")} {row.GetCurrent("trackName")}".Trim();
            if (term.Length == 0)
            {
                log($"No tags to search for: {row.File}", StatusKind.Warning);
            }
            else
            {
                foreach (var country in countries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var results = await search.SearchAsync(term, country, cancellationToken).ConfigureAwait(false);
                    var first = results.FirstOrDefault(static r =>
                        (r.Kind is null or "song") && !string.IsNullOrWhiteSpace(r.TrackName));
                    if (first is not null)
                    {
                        appleResult = first;
                        foundIn = country;
                        viaSearch = true;
                        break;
                    }
                }
            }
        }

        // Build the candidate list: the Apple hit first, then other sources
        // (unless skipped, and always queried when requested).
        var candidates = new List<TagCandidate>();
        var autoSelectIndex = -1;
        if (appleResult is not null)
        {
            var tagCandidate = iTunesMatchTagger.Core.Sources.ITunesSource.CandidateFromLookupResult(appleResult);
            candidates.Add(tagCandidate);
            autoSelectIndex = 0;
        }

        var appleFound = appleResult is not null;
        if ((!appleFound || alwaysQueryAll) && fallbackSources.Count > 0)
        {
            var term = $"{row.GetCurrent("artistName")} {row.GetCurrent("trackName")}".Trim();
            foreach (var source in fallbackSources)
            {
                if (skippedSources.Contains(source.Id))
                {
                    continue;
                }

                try
                {
                    var query = new TagQuery(row.GetCurrent("artistName"), row.GetCurrent("trackName"), row.GetCurrent("collectionName"));
                    var sourceCandidates = await source.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                    if (sourceCandidates.Count > 0)
                    {
                        log($"{source.Id}: {sourceCandidates.Count} candidate(s)", StatusKind.Neutral);
                        if (autoSelectIndex < 0)
                        {
                            autoSelectIndex = candidates.Count;
                        }

                        candidates.AddRange(sourceCandidates);
                    }
                }
                catch (TagSourceAuthException ex)
                {
                    // one message per run - it is a configuration problem
                    skippedSources.Add(source.Id);
                    log($"{source.Id} disabled for this run: {ex.Message}", StatusKind.Error);
                }
                catch (Exception ex) when (ex is HttpRequestException or TagSourceException or TaskCanceledException)
                {
                    log($"{source.Id} lookup failed: {ex.Message}", StatusKind.Warning);
                }
            }
        }

        // Artwork previews (300px) for the candidate strip.
        candidates = Deduplicate(candidates);
        var previews = new Dictionary<int, byte[]>();
        foreach (var candidate in candidates)
        {
            if (candidate.ArtworkUrl is not { } url)
            {
                continue;
            }

            var candidateIndex = candidates.IndexOf(candidate);
            if (previews.ContainsKey(candidateIndex))
            {
                continue;
            }

            try
            {
                previews[candidateIndex] = await artworkHttp.GetByteArrayAsync(
                    ITunesSearchClient.SizedArtworkUrl(url, 600), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // preview only - skip silently
            }
        }

        return new LookupOutcome(
            candidates,
            foundIn,
            viaSearch,
            appleResult?.TrackId,
            autoSelectIndex,
            previews);
    }

    /// <summary>
    /// Drops candidates that repeat within the same source (Apple's ID lookup
    /// and term search often return the very same release).
    /// </summary>
    private static List<TagCandidate> Deduplicate(IReadOnlyList<TagCandidate> candidates)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TagCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var key = $"{candidate.SourceId}|{candidate.Title}|{candidate.Album}|{candidate.Details}";
            if (seen.Add(key))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    public static string BuildLookupStatus(TrackRowViewModel row, LookupOutcome outcome)
    {
        var active = row.ActiveCandidate!;
        return active.SourceId == TagSources.ITunes
            ? outcome.FoundViaSearch
                ? $"Found via search in {outcome.FoundIn} (catalog ID {outcome.AppleTrackId})"
                : $"Found in {outcome.FoundIn}"
            : $"Found on {active.SourceId} - {row.Candidates.Count} candidate(s), pick one in the detail panel";
    }
}

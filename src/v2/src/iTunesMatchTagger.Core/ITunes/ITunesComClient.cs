using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.Core.ITunes;

/// <summary>
/// Talks to a running iTunes for Windows through its COM automation
/// interface using late binding (IDispatch), so that no COM reference or
/// interop assembly is needed at build time - a plain `dotnet build` works
/// on any machine. iTunes must be installed (and, in practice, running)
/// when this class is actually used.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ITunesComClient : IDisposable
{
    public const string ProgId = "iTunes.Application";

    private object? _application;

    /// <summary>Connects to (or starts) iTunes and returns its application object.</summary>
    public dynamic Application => _application ??= Activator.CreateInstance(Type.GetTypeFromProgID(ProgId, throwOnError: true)!)!;

    /// <summary>
    /// The tracks currently selected in the iTunes UI, or an empty list when
    /// nothing is selected.
    /// </summary>
    public IReadOnlyList<ComTrack> GetSelectedTracks()
    {
        var tracks = new List<ComTrack>();

        dynamic selection = Application.SelectedTracks;
        if (selection is null)
        {
            return tracks;
        }

        int count = (int)selection.Count;
        for (int i = 1; i <= count; i++) // COM collections are 1-based
        {
            tracks.Add(new ComTrack(selection[i]));
        }

        return tracks;
    }

    /// <summary>
    /// Finds the LIBRARY representation of a song. Resolution prefers the
    /// fast playlist Search endpoint (a handful of calls) filtered by
    /// TrackDatabaseID; the full-library enumeration is only a fallback.
    /// When the database ID is unknown, the entry is matched by file
    /// location instead. Returns null when nothing matches.
    /// </summary>
    public ComTrack? FindLibraryTrack(long databaseId, string? location, string? nameHint)
    {
        if (!string.IsNullOrWhiteSpace(nameHint))
        {
            try
            {
                dynamic found = Application.LibraryPlaylist.Search(nameHint, 0 /* ITPlaylistSearchFieldAll */);
                int count = (int)found.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic candidate = found[i];
                    if (MatchesLibraryEntry(candidate, databaseId, location))
                    {
                        return new ComTrack(candidate);
                    }
                }
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // search unavailable or rejected the text; fall through
            }
        }

        // Fallback: full enumeration (slow - keep off the UI thread).
        dynamic tracks = Application.LibraryPlaylist.Tracks;
        int total = (int)tracks.Count;
        for (int i = 1; i <= total; i++)
        {
            dynamic candidate = tracks[i];
            if (MatchesLibraryEntry(candidate, databaseId, location))
            {
                return new ComTrack(candidate);
            }
        }

        return null;
    }

    private static bool MatchesLibraryEntry(dynamic track, long databaseId, string? location)
    {
        try
        {
            if (databaseId != 0 && (long)((int)track.TrackDatabaseID) == databaseId)
            {
                return true;
            }

            if (databaseId == 0 &&
                !string.IsNullOrEmpty(location) &&
                string.Equals((string?)track.Location, location, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            // unreadable entry (cloud-only track, ...)
        }

        return false;
    }

    /// <summary>The localized KindAsString of a track ("Matched AAC audio file", "Uploaded ...", ...).</summary>
    public string GetKindAsString(ComTrack track)
    {
        try
        {
            return (string)track.Raw.KindAsString;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return "unknown kind";
        }
    }

    /// <summary>
    /// Removes a track from the iTunes library. Depending on the iTunes
    /// version this may also delete (or trash) the underlying file, so
    /// callers must keep a copy of the bytes first.
    /// </summary>
    public void RemoveFromLibrary(ComTrack track) => track.Raw.Delete();

    /// <summary>
    /// Re-imports an audio file into the main library (the delete + re-add
    /// trick that makes Apple's match engine re-evaluate an uploaded track).
    /// Returns the freshly added library track, or null when iTunes added
    /// nothing (already present, filter rejected the kind, ...).
    /// </summary>
    public ComTrack? AddFileToLibrary(string path)
    {
        dynamic operation = Application.LibraryPlaylist.AddFile(path);
        dynamic tracks = operation.Tracks;
        int count = (int)tracks.Count;
        return count == 0 ? null : new ComTrack(tracks[1]); // 1-based
    }

    /// <summary>
    /// Names of the plain user playlists of the Music library (no smart
    /// or special/system playlists), sorted alphabetically.
    /// </summary>
    public IReadOnlyList<string> GetUserPlaylistNames()
    {
        var names = new List<string>();
        dynamic playlists = Application.LibrarySource.Playlists;
        int count = (int)playlists.Count;
        for (int i = 1; i <= count; i++)
        {
            var name = TryGetAsPlainUserPlaylist(playlists[i]);
            if (name is not null)
            {
                names.Add(name);
            }
        }

        return [.. names.Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Plain user playlists currently containing the track (via
    /// IITFileOrCDTrack::Playlists). Library/smart/special playlists excluded.
    /// </summary>
    public IReadOnlyList<string> GetTrackPlaylistNames(ComTrack track)
    {
        var names = new List<string>();
        try
        {
            dynamic playlists = track.Raw.Playlists;
            int count = (int)playlists.Count;
            for (int i = 1; i <= count; i++)
            {
                var name = TryGetAsPlainUserPlaylist(playlists[i]);
                if (name is not null)
                {
                    names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return []; // discovery unavailable; callers log and fall back
        }

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Adds an existing library track to a named user playlist. Returns
    /// false when the playlist does not exist or already contains the
    /// track. Track membership check runs on TrackDatabaseID.
    /// </summary>
    public bool AddTrackToPlaylist(string playlistName, ComTrack track)
    {
        dynamic? playlist = FindUserPlaylist(playlistName);
        if (playlist is null)
        {
            return false;
        }

        long databaseId = (long)((int)track.Raw.TrackDatabaseID);
        dynamic tracks = playlist.Tracks;
        int count = (int)tracks.Count;
        for (int i = 1; i <= count; i++)
        {
            if ((long)((int)tracks[i].TrackDatabaseID) == databaseId)
            {
                return false; // already a member
            }
        }

        playlist.AddTrack(track.Raw);
        return true;
    }

    private dynamic? FindUserPlaylist(string playlistName)
    {
        dynamic playlists = Application.LibrarySource.Playlists;
        int count = (int)playlists.Count;
        for (int i = 1; i <= count; i++)
        {
            dynamic candidate = playlists[i];
            var verified = TryGetAsPlainUserPlaylist(candidate);
            if (verified is not null &&
                string.Equals(verified, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the playlist name when it is a plain user playlist (user
    /// kind, not smart, not a system/special playlist); null otherwise.
    /// </summary>
    private static string? TryGetAsPlainUserPlaylist(dynamic playlist)
    {
        try
        {
            if ((int)playlist.Kind != 2 /* lib: ITPlaylistKindUser */)
            {
                return null;
            }

            if ((bool)playlist.Smart)
            {
                return null;
            }

            // Special kinds are the system "Music"/"Movies"/... playlists.
            if ((int)playlist.SpecialKind != 0)
            {
                return null;
            }

            var name = (string)playlist.Name;
            return name.Length > 0 ? name : null;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return null; // not a user playlist (no Smart/SpecialKind properties)
        }
    }

    public void Dispose()
    {
        if (_application is not null)
        {
            try
            {
                Marshal.FinalReleaseComObject(_application);
            }
            catch (ArgumentException)
            {
                // not a runtime-callable COM object; nothing to release
            }

            _application = null;
        }
    }
}

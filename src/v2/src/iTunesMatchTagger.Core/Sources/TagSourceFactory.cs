using iTunesMatchTagger.Core.Settings;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Builds live <see cref="ITagSource"/> instances from user settings,
/// in fallback order: Apple first, then enabled sources in catalog order.
/// Also resolves tokens. Pure factory - no HTTP happens here.
/// </summary>
public static class TagSourceFactory
{
    public static IReadOnlyList<ITagSource> CreateChain(AppSettings settings)
    {
        var sources = new List<ITagSource>();

        var apple = settings.Sources.FirstOrDefault(s => s.Id == TagSources.ITunes);
        if (apple is null || apple.Enabled)
        {
            sources.Add(new ITunesSource());
        }

        foreach (var info in TagSourceCatalog.All.Where(static i => i.Id != TagSources.ITunes))
        {
            var option = settings.Sources.FirstOrDefault(s => s.Id == info.Id);
            if (option is not { Enabled: true })
            {
                continue;
            }

            var source = Create(info, option.Token);
            if (source is not null)
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    public static ITagSource? Create(TagSourceInfo info, string? token = null) => info.Id switch
    {
        TagSources.ITunes => new ITunesSource(),
        TagSources.MusicBrainz => new MusicBrainzSource(),
        TagSources.Discogs => new DiscogsSource { Token = token },
        TagSources.Deezer => new DeezerSource(),
        _ => null,
    };
}

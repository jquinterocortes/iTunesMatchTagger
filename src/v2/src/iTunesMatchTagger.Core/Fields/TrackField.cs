using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Fields;

/// <summary>
/// Declarative definition of one taggable track field and how it maps
/// between the storage back ends and the lookup sources:
/// <list type="bullet">
///   <item>iTunes Search API JSON result (<see cref="GetFromLookup"/>)</item>
///   <item>any tag-source candidate - Apple, MusicBrainz, Discogs, ... (<see cref="GetFromCandidate"/>)</item>
///   <item>the live iTunes COM object model (<see cref="GetFromItunes"/> / <see cref="SetOnItunes"/>)</item>
///   <item>audio file tags via TagLib# (<see cref="GetFromFileTag"/> / <see cref="SetOnFileTag"/>)</item>
/// </list>
/// Replaces the upstream string-based reflection pairs
/// (<c>LookupMember</c> / <c>ITunesComMember</c> in <c>UpdateOption</c>).
/// </summary>
public sealed record TrackField(
    string DisplayName,
    string LookupMember,
    Func<ITunesLookupResult, object?>? GetFromLookup = null,
    Func<TagCandidate, object?>? GetFromCandidate = null,
    Func<dynamic, object?>? GetFromItunes = null,
    Action<dynamic, object?>? SetOnItunes = null,
    Func<TagLib.Tag, object?>? GetFromFileTag = null,
    Action<TagLib.Tag, object?>? SetOnFileTag = null,
    Func<object?, object?>? CoerceWrite = null,
    bool UpdateByDefault = false,
    bool VisibleInOptions = true,
    string? NullValue = null)
{
    public override string ToString() => DisplayName;
}

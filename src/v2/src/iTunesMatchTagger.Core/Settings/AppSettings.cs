using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Settings;

/// <summary>
/// User settings persisted as JSON under %AppData% - replaces the
/// upstream app.config / Settings machinery.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// iTunes storefronts tried in order for Apple lookups. Hand-edited in
    /// settings.json (the UI has no selector); when a track ID is not found
    /// in any of them, the app sweeps the remaining storefronts automatically.
    /// Default: the machine's own storefront first, then US, GB and JP.
    /// </summary>
    public List<string> SelectedCountries { get; set; } = [.. DefaultCountries()];

    private static List<string> DefaultCountries()
    {
        var countries = new List<string>();
        try
        {
            var own = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpperInvariant();
            if (StoreCountries.All.Contains(own))
            {
                countries.Add(own);
            }
        }
        catch (ArgumentException)
        {
            // no current region; the fallback list below still applies
        }

        foreach (var code in (string[])["US", "GB", "JP"])
        {
            if (!countries.Contains(code))
            {
                countries.Add(code);
            }
        }

        return countries;
    }

    /// <summary>Query every enabled tag source on every lookup, even when Apple matches (slower, richer comparison).</summary>
    public bool QueryAllSources { get; set; }

    /// <summary>Fetch and write lyrics from LRCLib during "3. Update tracks" (synced LRC when available, plain text otherwise).</summary>
    public bool IncludeLyrics { get; set; }

    /// <summary>Per-source options for the tag-source fallback chain. The list is the fallback order after Apple.</summary>
    public List<TagSourceSettings> Sources { get; set; } = [.. TagSourceCatalog.All
        .Where(static s => s.Id != TagSources.ITunes)
        .Select(static s => new TagSourceSettings { Id = s.Id, Enabled = false, Token = null })];

    public Dictionary<string, FieldOptions> Fields { get; set; } = [];

    public sealed class FieldOptions
    {
        public bool Update { get; set; }
        public bool Overwrite { get; set; }
        public string? OverwriteValue { get; set; }
    }

    public static string DefaultFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "iTunesMatchTagger",
            "settings.json");

    public static AppSettings Load(string? filePath = null)
    {
        filePath ??= DefaultFilePath;
        try
        {
            if (File.Exists(filePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // corrupted or unreadable settings fall back to defaults
        }

        return new AppSettings();
    }

    public void Save(string? filePath = null)
    {
        filePath ??= DefaultFilePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}

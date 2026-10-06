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

    public List<string> SelectedCountries { get; set; } = [.. StoreCountries.DefaultSelected];

    /// <summary>Query every enabled tag source on every lookup, even when Apple matches (slower, richer comparison).</summary>
    public bool QueryAllSources { get; set; }

    /// <summary>Last playlist chosen for the force re-scan flow.</summary>
    public string? LastRescanPlaylistName { get; set; }

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

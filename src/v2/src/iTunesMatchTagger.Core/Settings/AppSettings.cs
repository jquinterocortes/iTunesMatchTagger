using System.Text.Json;
using System.Text.Json.Serialization;
using iTunesMatchTagger.Core.Lookup;

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

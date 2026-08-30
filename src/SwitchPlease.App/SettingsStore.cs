using System.Text.Json;
using System.Text.Json.Serialization;
using SwitchPlease.Core.Config;

namespace SwitchPlease.App;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under %APPDATA%.
///
/// Every method comes in two forms: one that uses the well-known location, and one that
/// takes a path. The second exists so the tests can exercise a corrupt file, a file from an
/// older version, and a half-written one without any of that happening to the settings of
/// whoever is running them.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SwitchPlease");

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "settings.json");

    public static string LogPath { get; } = Path.Combine(DirectoryPath, "switch-please.log");

    public static AppSettings Load() => Load(FilePath);

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            // A corrupt settings file must not stop the app from starting.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings) => Save(settings, FilePath);

    /// <summary>
    /// Writes the settings, via a temporary file.
    ///
    /// Writing straight over the real one leaves a window in which the file exists and is
    /// half-written, and a crash or a power cut inside it takes every setting with it. A
    /// rename is atomic, so the file on disk is either the old one or the new one.
    /// </summary>
    public static void Save(AppSettings settings, string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = path + ".tmp";

        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, path, overwrite: true);
    }
}

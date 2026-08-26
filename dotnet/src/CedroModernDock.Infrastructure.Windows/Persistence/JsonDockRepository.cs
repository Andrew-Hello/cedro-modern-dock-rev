namespace CedroModernDock.Infrastructure.Windows.Persistence;

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>
/// Direct port of JsonDockRepository. Persists the dock configuration to
/// config.json using System.Text.Json with polymorphic @type discrimination,
/// keeping the JSON format identical to the original Java/Jackson output.
///
/// The config file lives in %APPDATA%\CedroModernDock\config.json (Windows-specific).
/// A future Infrastructure.MacOS/Linux sibling would use a different base path.
/// </summary>
public sealed class JsonDockRepository : IDockRepository
{
    private const string ConfigFileName = "config.json";
    private const string AppDataFolder = "CedroModernDock";
    private const string BackupsFolder = "Backups";

    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// True when Load() had to create a fresh default config because none
    /// existed (first run) or the existing file was corrupt/empty.
    /// </summary>
    public bool WasDefaultCreated { get; private set; }

    public JsonDockRepository() : this(GetDefaultConfigPath()) { }

    public JsonDockRepository(string configFilePath)
    {
        _configFilePath = configFilePath;
        _serializerOptions = CreateSerializerOptions();
    }

    public void Save(DockModel model)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_configFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(model, _serializerOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving the DockModel: {e.Message}");
        }
    }

    public DockModel Load()
    {
        if (File.Exists(_configFilePath) && new FileInfo(_configFilePath).Length > 0)
        {
            try
            {
                string json = File.ReadAllText(_configFilePath);
                var model = JsonSerializer.Deserialize<DockModel>(json, _serializerOptions);
                if (model != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[JsonDockRepository] config.json loaded from: {_configFilePath}");
                    return model;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error reading config.json, creating default: {e.Message}");
                return CreateAndSaveDefault();
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"config.json not found or empty. Creating default at: {_configFilePath}");
        return CreateAndSaveDefault();
    }

    private DockModel CreateAndSaveDefault()
    {
        WasDefaultCreated = true;
        var model = new DockModel();
        model.LoadDefaultItems();
        Save(model);
        return model;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // Use camelCase to match the original Java/Jackson config.json format
            // (label, path, module, iconsSize, dockColorRGB, etc.). Explicit
            // [JsonPropertyName] attributes on DockModel take precedence and are
            // kept for clarity, but the policy ensures DockItem subtypes' plain
            // auto-properties (Label, Path, Module) also match camelCase keys.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        // Unknown properties are ignored by default in STJ (no FAIL_ON_UNKNOWN_PROPERTIES equivalent needed)
        return options;
    }

    /// <summary>Current user's live Cedro configuration file.</summary>
    public static string DefaultConfigPath => GetDefaultConfigPath();

    /// <summary>Folder that contains config.json.</summary>
    public static string ConfigDirectoryPath => Path.GetDirectoryName(GetDefaultConfigPath())!;

    /// <summary>
    /// Automatic safety copies made before imports are stored here. The folder
    /// is created on demand and is also exposed in Settings for easy access.
    /// </summary>
    public static string BackupDirectoryPath
    {
        get
        {
            string path = Path.Combine(ConfigDirectoryPath, BackupsFolder);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>Copies the current live config to a user-selected destination.</summary>
    public static void ExportCurrentConfig(string destinationPath)
    {
        // Ensure first-run users have a physical config before exporting.
        string source = GetDefaultConfigPath();
        if (!File.Exists(source))
            _ = new JsonDockRepository().Load();

        string? destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
            Directory.CreateDirectory(destinationDir);
        File.Copy(source, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Validates a candidate Cedro config, automatically backs up the current
    /// live config, then atomically-ish replaces config.json. No live in-memory
    /// model is mutated; callers should restart the app after success.
    /// </summary>
    public static bool TryImportConfig(string sourcePath, out string? error)
    {
        error = null;
        string? tempPath = null;
        try
        {
            if (!File.Exists(sourcePath))
            {
                error = "The selected file does not exist.";
                return false;
            }

            string json = File.ReadAllText(sourcePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The selected file is empty.";
                return false;
            }

            // First verify this is recognizably a Cedro config rather than just
            // any syntactically-valid JSON document.
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("items", out JsonElement items) ||
                    items.ValueKind != JsonValueKind.Array)
                {
                    error = "The selected JSON does not look like a Cedro Modern Dock configuration.";
                    return false;
                }
            }

            DockModel? model = JsonSerializer.Deserialize<DockModel>(json, CreateSerializerOptions());
            if (model == null)
            {
                error = "The selected configuration could not be read.";
                return false;
            }

            string livePath = GetDefaultConfigPath();
            Directory.CreateDirectory(ConfigDirectoryPath);

            // Keep a timestamped escape hatch before replacing anything.
            if (File.Exists(livePath) && new FileInfo(livePath).Length > 0)
            {
                string backupName = $"config-before-import-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                File.Copy(livePath, Path.Combine(BackupDirectoryPath, backupName), overwrite: true);
            }

            // Write to a sibling temporary file first, then replace the live path.
            tempPath = livePath + ".importing";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, livePath, overwrite: true);
            tempPath = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (tempPath != null)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }

    private static string GetDefaultConfigPath()
    {
        string? appDataPath = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrEmpty(appDataPath))
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string configDir = Path.Combine(appDataPath, AppDataFolder);
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        return Path.Combine(configDir, ConfigFileName);
    }
}

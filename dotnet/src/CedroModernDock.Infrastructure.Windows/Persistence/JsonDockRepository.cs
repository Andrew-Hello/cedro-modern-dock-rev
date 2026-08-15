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

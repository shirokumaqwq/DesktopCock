using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DesktopCock.Core;

namespace DesktopCock;

public sealed class Settings
{
    public int Scale { get; set; } = 3;
    public string Skin { get; set; } = "original";
    public bool AutoExploreEnabled { get; set; } = true;
    public double ExploreIntervalSeconds { get; set; } = 90;
    public bool AudioMuted { get; set; }
    public double AudioVolume { get; set; } = .35;
    public int AudioVisualDelayMs { get; set; }
    [JsonIgnore] public BehaviorParameters Behavior { get; set; } = new();
    public void Validate()
    {
        Scale = Math.Clamp(Scale, 2, 4);
        AudioVolume = double.IsFinite(AudioVolume) ? Math.Clamp(AudioVolume, 0, 1) : .35;
        AudioVisualDelayMs = Math.Clamp(AudioVisualDelayMs, 0, 500);
        ExploreIntervalSeconds = double.IsFinite(ExploreIntervalSeconds) ? Math.Clamp(ExploreIntervalSeconds, 5, 3600) : 90;
        Behavior.Validate();
    }
    [JsonIgnore] public RuntimeOptions RuntimeOptions => new(Scale, AutoExploreEnabled, ExploreIntervalSeconds);
}

// Preserve the existing flat JSON format; runtime objects stay separated.
public static class SettingsCodec
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Settings Deserialize(string text)
    {
        var settings = JsonSerializer.Deserialize<Settings>(text) ?? new();
        settings.Behavior = JsonSerializer.Deserialize<BehaviorParameters>(text) ?? new();
        settings.Validate(); return settings;
    }
    public static string Serialize(Settings settings)
    {
        var root = JsonSerializer.SerializeToNode(settings, Json)!.AsObject();
        root.Remove(nameof(Settings.RuntimeOptions));
        foreach (var field in JsonSerializer.SerializeToNode(settings.Behavior, Json)!.AsObject())
            root[field.Key] = field.Value?.DeepClone();
        return root.ToJsonString(Json);
    }
}

internal static class SettingsStore
{
    internal static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if DEBUG
        "DesktopCock.Debug");
#else
        "DesktopCock");
#endif
    internal static Settings Load()
    {
        try
        {
            var path = Path.Combine(DataFolder, "settings.json");
            return File.Exists(path) ? SettingsCodec.Deserialize(File.ReadAllText(path)) : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(Path.Combine(DataFolder, "error.log"), $"{DateTime.Now:O} Settings reset: {e.Message}\n");
            return new();
        }
    }
    internal static void Save(Settings settings)
    {
        Directory.CreateDirectory(DataFolder);
        var path = Path.Combine(DataFolder, "settings.json");
        File.WriteAllText(path+".tmp", SettingsCodec.Serialize(settings)); File.Move(path+".tmp", path, true);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopCock.Core;
using NAudio.Wave;

namespace DesktopCock;

internal sealed class AudioClipSpec
{
    public string Id { get; set; } = "";
    public VocalizationKind Kind { get; set; }
    public string File { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public double Duration { get; set; }
    public MouthCue[] Mouth { get; set; } = [];
}
internal sealed class AudioManifest
{
    public int SchemaVersion { get; set; }
    public AudioClipSpec[] Clips { get; set; } = [];
}
internal sealed record AudioClip(string Id, VocalizationKind Kind, string Path, MouthTimeline Timeline);

internal sealed class AudioCatalog
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly List<AudioClip> clips = [];
    private readonly Dictionary<VocalizationKind, string> last = [];
    private readonly Random random = new();
    internal string Status { get; private set; } = "";
    internal IReadOnlyList<AudioClip> Clips => clips;
    internal AudioCatalog(string folder)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<AudioManifest>(File.ReadAllText(Path.Combine(folder,"catalog.json")), Json);
            if (manifest?.SchemaVersion != 1 || manifest.Clips == null) throw new InvalidDataException("声音清单版本不支持");
            foreach (var spec in manifest.Clips)
            {
                try
                {
                    if (spec == null || spec.Mouth == null || string.IsNullOrWhiteSpace(spec.File) ||
                        string.IsNullOrWhiteSpace(spec.Id) || clips.Any(c => c.Id == spec.Id) ||
                        spec.File != Path.GetFileName(spec.File) || !Enum.IsDefined(spec.Kind) || spec.Kind == VocalizationKind.None)
                        throw new InvalidDataException("声音条目无效");
                    var path = Path.Combine(folder, spec.File);
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), spec.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("声音文件校验失败");
                    using var reader = new WaveFileReader(path);
                    if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.BitsPerSample != 16 ||
                        Math.Abs(reader.TotalTime.TotalSeconds-spec.Duration) > .002) throw new InvalidDataException("声音格式或时长不符");
                    clips.Add(new(spec.Id, spec.Kind, path, new(spec.Duration, spec.Mouth)));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
                { Status = "部分声音不可用："+e.Message; }
            }
            if (clips.Count == 0) Status = "未找到可播放的鸟声";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or ArgumentException)
        { Status = "鸟声不可用："+e.Message; }
    }
    internal AudioClip? Pick(VocalizationKind kind)
    {
        var candidates = clips.Where(c => c.Kind == kind).ToArray();
        if (candidates.Length == 0) return null;
        var pool = candidates.Where(c => !last.TryGetValue(kind, out var id) || c.Id != id).ToArray();
        if (pool.Length == 0) pool = candidates;
        var clip = pool[random.Next(pool.Length)]; last[kind] = clip.Id; return clip;
    }
}

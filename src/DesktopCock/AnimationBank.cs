using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopCock.Core;

namespace DesktopCock;
internal sealed class FrameSpec
{
    public string File { get; set; } = "";
    public int[] Foot { get; set; } = [];
    public int[] Head { get; set; } = [];
    public int[] Body { get; set; } = [];
    public int[] Feet { get; set; } = [];
}
internal sealed class ClipFrame { public string Name { get; set; } = ""; public int Milliseconds { get; set; } }
internal sealed class Clip { public List<ClipFrame> Frames { get; set; } = []; public bool Loop { get; set; } }
internal sealed class Manifest
{
    public Dictionary<string, FrameSpec> Frames { get; set; } = [];
    public Dictionary<string, Clip> Clips { get; set; } = [];
}
internal sealed class Sprite
{
    public required BitmapSource Bitmap { get; init; }
    public required FrameSpec Spec { get; init; }
    public required byte[] Pixels { get; init; }
    public bool Opaque(int x, int y) => x is >=0 and <64 && y is >=0 and <64 && Pixels[(y*64+x)*4+3] != 0;
}
internal sealed class AnimationBank
{
    private readonly Manifest manifest;
    private readonly Dictionary<string, Sprite> sprites = [];
    public AnimationBank()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Assets");
        manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(folder,"animations.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var (name,spec) in manifest.Frames)
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(Path.Combine(folder,spec.File)); bitmap.EndInit(); bitmap.Freeze();
            var bgra = new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0); bgra.Freeze();
            if (bgra.PixelWidth != 64 || bgra.PixelHeight != 64) throw new InvalidDataException("Frames must be 64x64");
            var bytes = new byte[64*64*4]; bgra.CopyPixels(bytes,256,0);
            sprites[name] = new Sprite { Bitmap = bgra, Spec = spec, Pixels = bytes };
        }
        foreach (var mood in Enum.GetNames<Mood>())
        {
            if (!manifest.Clips.TryGetValue(mood,out var clip) || clip.Frames.Count == 0 || clip.Frames.Any(f => !sprites.ContainsKey(f.Name) || f.Milliseconds <= 0))
                throw new InvalidDataException($"Missing or invalid clip {mood}");
        }
    }
    public Sprite Get(Mood mood, double age)
    {
        var clip = manifest.Clips[mood.ToString()];
        double total = clip.Frames.Sum(f => f.Milliseconds), time = age*1000;
        time = clip.Loop ? time % total : Math.Min(time,total-1);
        foreach(var frame in clip.Frames) { if (time < frame.Milliseconds) return sprites[frame.Name]; time -= frame.Milliseconds; }
        return sprites[clip.Frames[^1].Name];
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCock;

internal sealed record SkinOption(string Id, string Name);
internal sealed class SkinSpec
{
    public string Name { get; set; } = "";
    public Dictionary<string, JsonElement> Palette { get; set; } = [];
}
internal sealed class MaterialMask
{
    public string Sha256 { get; set; } = "";
    public string[] Rows { get; set; } = [];
}
internal sealed class SkinManifest
{
    public int SchemaVersion { get; set; }
    public Dictionary<string, string> Regions { get; set; } = [];
    public Dictionary<string, SkinSpec> Skins { get; set; } = [];
    public Dictionary<string, MaterialMask> Frames { get; set; } = [];
}

// Region masks are tied to the exact base-frame hash; palette changes never alter
// animation timing, anchors, interaction rectangles or pixel alpha.
internal sealed class SkinBank
{
    private readonly Dictionary<string, string> masks = [];
    private readonly Dictionary<string, Dictionary<char, byte[]>> palettes = [];
    private readonly Dictionary<(string Skin, string Frame), Sprite> cache = [];
    internal IReadOnlyList<SkinOption> Options { get; }

    internal SkinBank(string folder, IReadOnlyDictionary<string, Sprite> sprites, string expectedHash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(folder, "skins.json"));
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Skin manifest does not match the animation assets. Rebuild the complete asset set.");
        var data = JsonSerializer.Deserialize<SkinManifest>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing skin manifest");
        if (data.SchemaVersion != 1 || !data.Skins.TryGetValue("original", out var original) || original.Palette.Count != 0)
            throw new InvalidDataException("Skins require schema 1 and an unchanged original palette");
        if (data.Regions.Any(r => r.Key.Length != 1 || r.Key == "." || string.IsNullOrWhiteSpace(r.Value)) ||
            data.Regions.Values.Distinct().Count() != data.Regions.Count)
            throw new InvalidDataException("Skin regions must have unique one-character codes and names");
        var regionCodes = data.Regions.ToDictionary(r => r.Value, r => r.Key[0]);
        if (data.Frames.Count != sprites.Count)
            throw new InvalidDataException("Skin masks must cover the complete animation set");
        foreach (var (name, source) in sprites)
        {
            if (!data.Frames.TryGetValue(name, out var mask) || mask.Sha256 != source.Spec.Sha256 ||
                mask.Rows.Length != source.Height || mask.Rows.Any(row => row.Length != source.Width))
                throw new InvalidDataException($"Skin mask does not match {name}. Regenerate the complete asset set.");
            var pixels = string.Concat(mask.Rows);
            for (int i = 0; i < pixels.Length; i++)
            {
                if ((pixels[i] == '.') != (source.Pixels[i*4+3] == 0) ||
                    (pixels[i] != '.' && !data.Regions.ContainsKey(pixels[i].ToString())))
                    throw new InvalidDataException($"Invalid material mask pixel in {name}");
            }
            masks[name] = pixels;
        }
        foreach (var (id, skin) in data.Skins)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(skin.Name))
                throw new InvalidDataException("Skins need an id and display name");
            var tables = new Dictionary<char, byte[]>();
            foreach (var (region, ramp) in skin.Palette)
            {
                if (!regionCodes.TryGetValue(region, out var code))
                    throw new InvalidDataException($"Unknown skin region: {region}");
                tables[code] = MakeTable(ramp);
            }
            palettes[id] = tables;
        }
        Options = data.Skins.Select(s => new SkinOption(s.Key, s.Value.Name)).ToArray();
    }

    internal string Resolve(string? id) => id != null && palettes.ContainsKey(id) ? id : "original";

    internal Sprite Get(string skin, string frame, Sprite source)
    {
        if (skin == "original") return source;
        if (cache.TryGetValue((skin, frame), out var existing)) return existing;
        var pixels = (byte[])source.Pixels.Clone();
        var palette = palettes[skin];
        var mask = masks[frame];
        for (int i = 0; i < mask.Length; i++)
        {
            if (!palette.TryGetValue(mask[i], out var table)) continue;
            int offset = i*4;
            int level = (54*pixels[offset+2]+183*pixels[offset+1]+19*pixels[offset]+128)/256;
            pixels[offset] = table[level*3+2];
            pixels[offset+1] = table[level*3+1];
            pixels[offset+2] = table[level*3];
        }
        var bitmap = BitmapSource.Create(source.Width,source.Height,96,96,PixelFormats.Bgra32,null,pixels,source.Width*4);
        bitmap.Freeze();
        var sprite = new Sprite { Bitmap = bitmap, Spec = source.Spec, Pixels = pixels };
        cache[(skin, frame)] = sprite;
        return sprite;
    }

    private static byte[] MakeTable(JsonElement ramp)
    {
        if (ramp.ValueKind != JsonValueKind.Array || ramp.GetArrayLength() < 2)
            throw new InvalidDataException("Palette ramps need at least two stops");
        var stops = new List<(int Level, byte[] Colour)>();
        foreach (var item in ramp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2 ||
                !item[0].TryGetInt32(out int level) || level is < 0 or > 255 ||
                (stops.Count > 0 && level <= stops[^1].Level) || item[1].ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Invalid palette stop");
            var hex = item[1].GetString()!;
            if (hex.Length != 7 || hex[0] != '#' || hex.Skip(1).Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Palette colours must use #RRGGBB");
            stops.Add((level, Convert.FromHexString(hex.AsSpan(1))));
        }
        if (stops[0].Level != 0 || stops[^1].Level != 255)
            throw new InvalidDataException("Palette ramps must cover brightness 0 through 255");
        var table = new byte[256*3];
        int right = 1;
        for (int level = 0; level < 256; level++)
        {
            while (level > stops[right].Level) right++;
            var lo = stops[right-1]; var hi = stops[right];
            int span = hi.Level-lo.Level, amount = level-lo.Level;
            for (int channel = 0; channel < 3; channel++)
                table[level*3+channel] = (byte)((lo.Colour[channel]*(span-amount)+hi.Colour[channel]*amount+span/2)/span);
        }
        return table;
    }
}

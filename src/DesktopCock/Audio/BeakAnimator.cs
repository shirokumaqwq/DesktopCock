using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopCock.Core;

namespace DesktopCock;

// Only touches the existing beak/cavity pixels of three established poses.
// Uses the already recoloured beak as its palette, so every feather skin works.
internal sealed class BeakAnimator
{
    private readonly Dictionary<(Sprite, MouthPose, string), Sprite> cache = [];
    private static readonly Dictionary<string,string> sourceHashes = new()
    {
        ["idle"]="323696e3aacfe90d2cd0c399a4c30cbfcd1a18c5ea28146008289bee73178c5d",
        ["sing1"]="5400181fb369bde5d19008040045bd5e1ea39ae6988062d34f44f4aab3b6f83d",
        ["sing2"]="742f10b1a7e98f8e876ba84f12a35d3eaf55580b7cc874b8b0fac6a00c9a7f3c"
    };
    internal Sprite Apply(Sprite source, string name, MouthPose mouth)
    {
        if (!sourceHashes.TryGetValue(name,out var hash) || source.Spec.Sha256!=hash) return source;
        if (name == "idle" && mouth == MouthPose.Closed) return source;
        if (cache.TryGetValue((source, mouth, name), out var result)) return result;
        var pixels = (byte[])source.Pixels.Clone();
        void Put(int x, int y, byte r, byte g, byte b)
        { int i = (y*source.Width+x)*4; pixels[i] = b; pixels[i+1] = g; pixels[i+2] = r; pixels[i+3] = 255; }
        if (name == "idle")
        {
            Put(43,31,31,22,23); Put(44,31,31,22,23);
            if (mouth == MouthPose.Open)
            { Put(43,32,31,22,23); Put(44,32,31,22,23); }
        }
        else
        {
            const int x = 42, y = 30;
            int sample = ((y-1)*source.Width+x)*4;
            // Normalize the tiny gape using the recoloured upper mandible.
            // An explicit two-pixel cavity survives display at native resolution.
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 2; dx++)
                    Put(x+dx,y+dy,source.Pixels[sample+2],source.Pixels[sample+1],source.Pixels[sample]);
            if (mouth != MouthPose.Closed) Put(x,y,31,22,23);
            if (mouth == MouthPose.Open)
            { Put(x+1,y,31,22,23); Put(x,y+1,31,22,23); Put(x+1,y+1,31,22,23); }
        }
        var bitmap = BitmapSource.Create(source.Width,source.Height,96,96,PixelFormats.Bgra32,null,pixels,source.Width*4);
        bitmap.Freeze(); result = new() { Bitmap = bitmap, Spec = source.Spec, Pixels = pixels };
        cache[(source,mouth,name)] = result; return result;
    }
}

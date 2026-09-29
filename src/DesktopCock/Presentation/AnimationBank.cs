using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopCock.Core;

namespace DesktopCock;
internal sealed class FrameSpec
{
    public string File { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int[] Foot { get; set; } = [];
    public int[] Head { get; set; } = [];
    public int[] Body { get; set; } = [];
    public int[] Feet { get; set; } = [];
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 64;
}
internal sealed class ClipFrame { public string Name { get; set; } = ""; public double Milliseconds { get; set; } }
internal sealed class Clip
{
    public List<ClipFrame> Frames { get; set; } = [];
    public bool Loop { get; set; }
    public double StridePixels { get; set; }
}
internal sealed class Manifest
{
    public string SkinManifestSha256 { get; set; } = "";
    public Dictionary<string, FrameSpec> Frames { get; set; } = [];
    public Dictionary<string, Clip> Clips { get; set; } = [];
}
internal sealed class Sprite
{
    public required BitmapSource Bitmap { get; init; }
    public required FrameSpec Spec { get; init; }
    public required byte[] Pixels { get; init; }
    public int Width => Bitmap.PixelWidth;
    public int Height => Bitmap.PixelHeight;
    public bool Opaque(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && Pixels[(y*Width+x)*4+3] != 0;
}
internal sealed class AnimationBank
{
    private readonly Manifest manifest;
    private readonly Dictionary<string, Sprite> sprites = [];
    private readonly SkinBank skins;
    private readonly BeakAnimator beaks = new();
    internal IReadOnlyList<SkinOption> Skins => skins.Options;
    internal string CurrentSkin { get; private set; } = "original";
    internal void SelectSkin(string? id) => CurrentSkin = skins.Resolve(id);
    internal double DurationSeconds(Mood mood) => manifest.Clips[mood.ToString()].Frames.Sum(f => f.Milliseconds) / 1000;
    private Sprite GetSprite(string name) => skins.Get(CurrentSkin, name, sprites[name]);
    public AnimationBank()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Assets");
        manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(folder,"animations.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var (name,spec) in manifest.Frames)
        {
            var path = Path.Combine(folder,spec.File);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), spec.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Asset does not match the animation manifest: {spec.File}. Rebuild the complete asset set.");
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
            var bgra = new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0); bgra.Freeze();
            if (bgra.PixelWidth != spec.Width || bgra.PixelHeight != spec.Height ||
                spec.Width is < 1 or > 96 || spec.Height is < 1 or > 96) throw new InvalidDataException("Invalid frame dimensions");
            var bytes = new byte[spec.Width*spec.Height*4]; bgra.CopyPixels(bytes,spec.Width*4,0);
            sprites[name] = new Sprite { Bitmap = bgra, Spec = spec, Pixels = bytes };
        }
        foreach (var mood in Enum.GetNames<Mood>().Concat(new[] { "LookFront", "LookBack", "LookUpDiagonal", "LookUpFront", "LookUpBack", "Turn", "TakingOff", "Flying", "Landing", "TakingOffFront", "FlyingFront", "LandingFront" }))
        {
            if (!manifest.Clips.TryGetValue(mood,out var clip) || clip.Frames.Count == 0 || clip.Frames.Any(f => !sprites.ContainsKey(f.Name) || f.Milliseconds <= 0))
                throw new InvalidDataException($"Missing or invalid clip {mood}");
        }
        if (!double.IsFinite(manifest.Clips["Walk"].StridePixels) || manifest.Clips["Walk"].StridePixels <= 0)
            throw new InvalidDataException("Walk needs a positive stride distance");
        skins = new SkinBank(folder, sprites, manifest.SkinManifestSha256);
    }
    public Sprite Get(PetSnapshot pet) => pet.Airborne ? GetFlight(pet.Locomotion, pet.LocomotionAge, pet.FlightFrontFacing) : Get(pet.Ground);
    public Sprite Get(PetSnapshot pet, VocalizationPlayback voice)
    {
        if (pet.Airborne) return Get(pet);
        if (pet.Ground.State == Mood.Sing)
        {
            // Body sway is independent of the mouth syllables, but shares the output clock.
            var age = voice.Playing ? voice.Position : pet.Ground.StateAge;
            var name = age % .46 < .22 ? "sing1" : "sing2";
            // A silent action preview still demonstrates singing. Real audio
            // keeps its authored mouth cues, including pauses between syllables.
            var phase = age % .46;
            var mouth = voice.Playing ? voice.Mouth : phase < .07 ? MouthPose.Closed
                : phase < .17 ? MouthPose.Small : phase < .34 ? MouthPose.Open : MouthPose.Small;
            return beaks.Apply(GetSprite(name), name, mouth);
        }
        if (voice.Playing && pet.Ground.State == Mood.Idle)
            return beaks.Apply(GetSprite("idle"), "idle", voice.Mouth);
        return Get(pet);
    }
    public Sprite Get(GroundSnapshot bird)
    {
        if (bird.State == Mood.Look && bird.IsTurning)
        {
            var turn = manifest.Clips["Turn"];
            return FrameAt(turn, Math.Min(bird.TurnProgress, .999999999) * turn.Frames.Sum(f => f.Milliseconds));
        }
        if (bird.State == Mood.Look)
        {
            var raised = bird.LookPose != Gaze.Level;
            var clip = bird.HeadPose switch
            {
                HeadYaw.Back => raised ? "LookUpBack" : "LookBack",
                HeadYaw.Front => raised ? "LookUpFront" : "LookFront",
                _ => raised ? "LookUpDiagonal" : "Look"
            };
            return GetClip(clip, bird.LookAge);
        }
        if (bird.State == Mood.Walk)
        {
            var clip = manifest.Clips["Walk"];
            // The same travelled distance advances both the window and the feet.
            // Changing WalkSpeed (including zero) keeps cadence matched to movement.
            var phase = bird.WalkDistance % clip.StridePixels / clip.StridePixels;
            return FrameAt(clip, phase * clip.Frames.Sum(f => f.Milliseconds));
        }
        return Get(bird.State, bird.StateAge);
    }
    public Sprite GetFlight(LocomotionState state,double age,bool frontFacing = false) =>
        GetClip(state + (frontFacing ? "Front" : ""),age);
    public Sprite Get(Mood mood, double age, Gaze gaze = Gaze.Level, double lookAge = 0)
    {
        string key = mood == Mood.Look ? gaze switch
        {
            Gaze.UpDiagonal => "LookUpDiagonal", Gaze.UpFront => "LookUpFront", _ => "Look"
        } : mood.ToString();
        if (mood == Mood.Look) age = lookAge;
        return GetClip(key, age);
    }
    private Sprite GetClip(string key, double age)
    {
        var clip = manifest.Clips[key];
        double total = clip.Frames.Sum(f => f.Milliseconds), time = age*1000;
        time = clip.Loop ? time % total : Math.Min(time,total-1);
        return FrameAt(clip, time);
    }
    private Sprite FrameAt(Clip clip, double time)
    {
        foreach(var frame in clip.Frames) { if (time < frame.Milliseconds) return GetSprite(frame.Name); time -= frame.Milliseconds; }
        return GetSprite(clip.Frames[^1].Name);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;

public sealed partial class PetWindow
{
    private readonly AudioCatalog audioCatalog = new(Path.Combine(AppContext.BaseDirectory,"Assets","Audio"));
    private readonly AudioService audio = new();
    private readonly VocalizationGate voiceGate = new();
    private VocalizationPlayback voicePlayback;
    private Mood voiceOwner;
    private Forms.ToolStripMenuItem? muteItem;
    private readonly Dictionary<int, Forms.ToolStripMenuItem> volumeItems = [];
    private void AddAudioMenu(Forms.ContextMenuStrip menu)
    {
        var sound = new Forms.ToolStripMenuItem("声音");
        muteItem = new("静音", null, (_,_) =>
        {
            Settings.AudioMuted = !Settings.AudioMuted; muteItem!.Checked = Settings.AudioMuted;
            if (Settings.AudioMuted) StopVoice(); SaveSettings(); Render();
        }) { Checked = Settings.AudioMuted };
        sound.DropDownItems.Add(muteItem);
        foreach (var level in new[] { 15, 35, 60, 100 })
        {
            var item = new Forms.ToolStripMenuItem($"音量 {level}%", null, (_,_) =>
            {
                Settings.AudioVolume = level/100.0; audio.SetVolume(Settings.AudioVolume);
                foreach (var (n, value) in volumeItems) value.Checked = n == level;
                SaveSettings();
            }) { Checked = Math.Abs(Settings.AudioVolume*100-level) < .1 };
            volumeItems[level] = item; sound.DropDownItems.Add(item);
        }
        menu.Items.Add(sound);
        AddLearningMenu(menu);
    }
    private void UpdateVoice(bool interrupted = false)
    {
        var allowed = FlightVisible && !paused && !flight.Airborne && !Settings.AudioMuted && Settings.AudioVolume > 0 && !LearningOwnsAudio;
        if (audio.HasSession && (!allowed || interrupted || Bird.State != voiceOwner ||
            Bird.VoiceRequest is not { } owner || owner.Id != audio.RequestId)) StopVoice();
        if (audio.HasSession)
        {
            voicePlayback = audio.Read(); Bird.UpdateVoice(voicePlayback);
            if (!voicePlayback.Playing) StopVoice();
        }
        if (Bird.VoiceRequest is not { } request) return;
        if (!voiceGate.Accept(request, clock.Elapsed.TotalSeconds, allowed && !interrupted, audio.HasSession))
        {
            if (!audio.HasSession) Bird.UpdateVoice(new(request.Id,false,0,0,MouthPose.Closed));
            return;
        }
        var selected = ChooseLearnedVoice(request.Kind) ?? audioCatalog.Pick(request.Kind);
        if (selected != null && audio.Play(request.Id, selected, Settings.AudioVolume, Settings.AudioVisualDelayMs))
        {
            voiceOwner = request.Owner;
            voicePlayback = new(request.Id,true,0,selected.Timeline.Duration,MouthPose.Closed);
            Bird.UpdateVoice(voicePlayback);
        }
        else Bird.UpdateVoice(new(request.Id,false,0,0,MouthPose.Closed));
    }
    private void StopVoice()
    {
        if (audio.HasSession)
        {
            Bird.UpdateVoice(new(audio.RequestId,false,0,0,MouthPose.Closed));
            SuppressLearningTail();
        }
        audio.Stop(); voicePlayback = default;
    }
    private void SyncAudioLifecycle()
    {
        if (!FlightVisible || paused) { StopVoice(); SuspendLearning(); }
    }
    private void CloseAudio()
    { StopVoice(); CloseLearning(); audio.Dispose(); }
}

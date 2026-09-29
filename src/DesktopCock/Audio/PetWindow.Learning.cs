using System;
using System.Windows;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;

public sealed partial class PetWindow
{
    private LearningService? learning;
    private LearningWindow? learningWindow;
    private MicrophoneCapture? microphone;
    private bool teaching, automaticLearning;
    private double suppressUntil, teachingStarted;
    private readonly Random learnedVoiceRandom = new();
    private string? lastLearnedVoice;
    private bool LearningOwnsAudio => teaching;
    internal LearningService Learning => learning ??= new();
    internal bool IsTeaching => teaching;
    internal bool AutomaticLearning => automaticLearning;
    internal string LearningCaptureStatus { get; private set; } = "麦克风未开启";
    private void AddLearningMenu(Forms.ContextMenuStrip menu) => menu.Items.Add("教它说话…",null,(_,_) =>
    {
        if (learningWindow == null)
        {
            learningWindow = new LearningWindow(this);
            learningWindow.Closed += (_,_) => { SuspendLearning(); learningWindow=null; };
        }
        learningWindow.Show(); learningWindow.Activate();
    });
    internal bool BeginTeaching()
    {
        if (!FlightVisible || paused || flight.Airborne || Learning.Busy || !Learning.Available) return false;
        SetAutomaticLearning(false); StopVoice();
        try
        {
            teaching = true; teachingStarted = clock.Elapsed.TotalSeconds;
            StartMicrophone(false); LearningCaptureStatus = "正在听你说话…松开结束（最长 10 秒）"; return true;
        }
        catch (Exception e) when (AudioService.IsAudioFailure(e))
        { SuspendLearning(); LearningCaptureStatus=e.Message; return false; }
    }
    private void StartMicrophone(bool automatic)
    {
        var capture = new MicrophoneCapture(automatic); microphone = capture;
        capture.Segment += pcm => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(microphone,capture)) return;
            if (automatic)
            {
                if (!audio.HasSession && clock.Elapsed.TotalSeconds>=suppressUntil && FlightVisible && !paused)
                    Learning.Submit(pcm,true);
            }
            else { EndTeaching(false); Learning.Submit(pcm,false); }
        }));
        capture.Failed += error => Dispatcher.BeginInvoke(new Action(() =>
        { if (ReferenceEquals(microphone,capture)) { SuspendLearning(); LearningCaptureStatus=error; } }));
        capture.Start();
    }
    internal void EndTeaching(bool submit)
    {
        if (!teaching) return;
        teaching=false; var capture=microphone;microphone=null;
        try { var pcm=capture?.Finish(); if (submit && pcm != null) Learning.Submit(pcm,false); }
        catch (Exception e) when (AudioService.IsAudioFailure(e)) { LearningCaptureStatus=e.Message; }
        finally { capture?.Dispose(); }
        LearningCaptureStatus="麦克风未开启"; Bird.Resume();
    }
    internal void SetAutomaticLearning(bool enabled)
    {
        if (!enabled)
        {
            automaticLearning=false;
            if (!teaching) { microphone?.Dispose();microphone=null; }
            learning?.Cancel();
            LearningCaptureStatus="麦克风未开启"; return;
        }
        if (automaticLearning || teaching || !FlightVisible || paused || !Learning.Available) return;
        try
        {
            StopVoice(); automaticLearning=true; StartMicrophone(true);
            LearningCaptureStatus="自动学习已开启 · 仅麦克风 · 关闭此窗口即停止";
        }
        catch (Exception e) when (AudioService.IsAudioFailure(e))
        { SetAutomaticLearning(false);LearningCaptureStatus=e.Message; }
    }
    private void TickLearning()
    {
        if (teaching && clock.Elapsed.TotalSeconds-teachingStarted>=10.2) EndTeaching(true);
        if (automaticLearning)
            microphone?.Suppress(audio.HasSession || clock.Elapsed.TotalSeconds<suppressUntil || Learning.Busy);
    }
    private void SuppressLearningTail() { suppressUntil=clock.Elapsed.TotalSeconds+.8;microphone?.Suppress(true); }
    private void SuspendLearning()
    {
        EndTeaching(false); SetAutomaticLearning(false); learning?.Cancel();
    }
    private void CloseLearning() { SuspendLearning();learningWindow?.Close();learning?.Dispose(); }
    private AudioClip? ChooseLearnedVoice(VocalizationKind kind)
    {
        if (kind!=VocalizationKind.Song || learnedVoiceRandom.NextDouble()>=.35) return null;
        var clip=Learning.ChooseVoice(learnedVoiceRandom);
        if (clip?.Id==lastLearnedVoice) { lastLearnedVoice=null;return null; }
        lastLearnedVoice=clip?.Id;return clip;
    }
    internal void RemoveLearnedPhrase(string id)
    { if (audio.ClipId=="learned-"+id) StopVoice(); Learning.Remove(id); }
}

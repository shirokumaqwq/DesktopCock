using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DesktopCock.Core;
using NAudio.Wave;

namespace DesktopCock;

internal sealed class LearningService : IDisposable
{
    private readonly object sync = new();
    private readonly string folder;
    private readonly WhisperRecognizer recognizer;
    private readonly PhraseMemory memory;
    private CancellationTokenSource? job;
    private Task? processing;
    private bool unreadableStore;
    private int busy;
    private bool disposed;
    internal event Action? Changed;
    internal bool Available => recognizer.Available;
    internal bool Busy => Volatile.Read(ref busy) != 0;
    internal string Status { get; private set; } = "按住教学按钮说一句话，松开后在本地处理。";
    internal string Folder => folder;
    internal LearningService(string? path = null, WhisperRecognizer? engine = null)
    {
        folder = path ?? Path.Combine(SettingsStore.DataFolder,"AudioLearning"); recognizer = engine ?? new();
        try
        {
            var file = Path.Combine(folder,"memory.json");
            memory = new(File.Exists(file) ? JsonSerializer.Deserialize<List<LearnedPhrase>>(File.ReadAllText(file), AudioCatalog.Json) : null);
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        { memory = new(); unreadableStore=true; Status = "学习记录读取失败，原文件已保留："+e.Message; }
    }
    internal LearnedPhrase[] Phrases
    {
        get { lock (sync) return memory.Phrases.Select(p => new LearnedPhrase { Id=p.Id,Text=p.Text,
            Repetitions=p.Repetitions,Familiarity=p.Familiarity,Confidence=p.Confidence,LastHeard=p.LastHeard }).ToArray(); }
    }
    internal void Submit(byte[] pcm, bool automatic)
    {
        if (disposed || pcm.Length < 16000 || !Available || Interlocked.CompareExchange(ref busy,1,0) != 0) return;
        // Reject empty captures before spawning the recognizer. Silero is the speech gate.
        double energy = 0;
        for (int i=0;i+1<pcm.Length;i+=2) { double sample = BitConverter.ToInt16(pcm,i)/32768.0; energy += sample*sample; }
        if (Math.Sqrt(energy/(pcm.Length/2)) < .002) { Volatile.Write(ref busy,0); SetStatus("没有检测到有效声音，请靠近麦克风再试。"); return; }
        var cancellation = new CancellationTokenSource(); job = cancellation;
        SetStatus("正在本地识别…");
        processing = Task.Run(async () =>
        {
            string? temporary = null;
            try
            {
                var directory = Path.Combine(folder,"Temp"); Directory.CreateDirectory(directory);
                var session = Guid.NewGuid().ToString("N"); temporary = Path.Combine(directory,session+".wav");
                WriteWave(temporary,pcm);
                var recognized = await recognizer.Recognize(temporary,cancellation.Token).ConfigureAwait(false);
                int learned = 0;
                lock (sync)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (disposed) return;
                    foreach (var value in recognized)
                    {
                        var phrase = memory.Observe(value.Text,value.Confidence,DateTimeOffset.UtcNow,session,automatic);
                        if (phrase == null) continue;
                        var first = Math.Clamp((int)(value.Start*16000)*2,0,pcm.Length);
                        var last = Math.Clamp((int)(value.End*16000)*2,first,pcm.Length);
                        if (last-first >= 8000) WriteWave(Path.Combine(folder,phrase.Id+".wav"),pcm[first..last]);
                        learned++;
                    }
                    if (learned > 0) Save();
                }
                SetStatus(learned > 0 ? $"记住了 {learned} 个片段。真实玄凤学舌模型尚未验收；目前不会用人声冒充。" : "未识别到可信的短语，这次没有增加熟练度。");
            }
            catch (OperationCanceledException) { SetStatus("本次教学已取消。"); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or JsonException or ArgumentException or System.ComponentModel.Win32Exception)
            { SetStatus("教学未完成："+e.Message); }
            finally
            {
                if (temporary != null)
                    foreach (var path in new[] { temporary,Path.ChangeExtension(temporary,".json") })
                        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (ReferenceEquals(job,cancellation)) job = null;
                cancellation.Dispose(); Volatile.Write(ref busy,0); Changed?.Invoke();
            }
        });
    }
    internal void Cancel() { try { job?.Cancel(); } catch (ObjectDisposedException) { } }
    private void SetStatus(string value) { Status = value; Changed?.Invoke(); }
    private void Save()
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder,"memory.json");
        if (unreadableStore && File.Exists(path))
        {
            File.Copy(path,Path.Combine(folder,"memory-unreadable-"+Guid.NewGuid().ToString("N")+".json"));
            unreadableStore=false;
        }
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(memory.Phrases,AudioCatalog.Json));
        File.Move(path+".tmp",path,true);
    }
    internal void Remove(string id)
    {
        Cancel();
        lock (sync)
        {
            if (!memory.Phrases.Any(p => p.Id==id)) return;
            foreach (var path in new[] { Path.Combine(folder,id+".wav"),Path.Combine(folder,"Voices",id+".wav"),Path.Combine(folder,"Voices",id+".json") })
                try { File.Delete(path); } catch (DirectoryNotFoundException) { }
            memory.Remove(id); Save();
        }
        SetStatus("已删除该短语、教学录音及对应声音。");
    }
    internal AudioClip? ChooseVoice(Random random)
    {
        lock (sync)
        {
            var candidates = memory.Phrases.Where(p => memory.FamiliarityAt(p,DateTimeOffset.UtcNow) >= 2.5).ToArray();
            foreach (var phrase in candidates.OrderBy(_ => random.Next()))
            {
                var voice = ReadVoice(phrase.Id);
                if (voice != null) return voice;
            }
            return null;
        }
    }
    internal bool HasVoice(string id) => ReadVoice(id) != null;
    private AudioClip? ReadVoice(string id)
    {
        if (!Guid.TryParseExact(id,"N",out _)) return null;
        try
        {
            var path = Path.Combine(folder,"Voices",id+".wav");
            var json = Path.Combine(folder,"Voices",id+".json");
            if (!File.Exists(path) || !File.Exists(json)) return null;
            var spec = JsonSerializer.Deserialize<AudioClipSpec>(File.ReadAllText(json),AudioCatalog.Json);
            if (spec?.Mouth == null) return null;
            if (!string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),spec.Sha256,StringComparison.OrdinalIgnoreCase)) return null;
            return new("learned-"+id,VocalizationKind.Song,path,new(spec.Duration,spec.Mouth));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
    // Only explicitly reviewed, phrase-specific parrot recordings enter playback.
    // This import does not imply arbitrary speech conversion or a trained model.
    internal void ImportVoice(string id, string source)
    {
        lock (sync)
        {
            if (!memory.Phrases.Any(p => p.Id == id)) throw new InvalidOperationException("短语不存在");
            using var reader = new WaveFileReader(source);
            if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.BitsPerSample != 16 ||
                reader.WaveFormat.Channels != 1 || reader.WaveFormat.SampleRate is < 16000 or > 96000 ||
                reader.TotalTime.TotalSeconds is < .25 or > 15)
                throw new InvalidDataException("请导入 0.25–15 秒、16 位单声道 PCM WAV。");
            var bytes = new byte[checked((int)reader.Length)]; reader.ReadExactly(bytes);
            var spec = new AudioClipSpec { Id="learned-"+id,Kind=VocalizationKind.Song,File=id+".wav",Duration=reader.TotalTime.TotalSeconds,
                Mouth=EstimateMouth(bytes,reader.WaveFormat.SampleRate) };
            var destination = Path.Combine(folder,"Voices"); Directory.CreateDirectory(destination);
            var wave = Path.Combine(destination,id+".wav");
            File.Copy(source,wave+".tmp",true); File.Move(wave+".tmp",wave,true);
            spec.Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(wave)));
            var json = Path.Combine(destination,id+".json");
            File.WriteAllText(json+".tmp",JsonSerializer.Serialize(spec,AudioCatalog.Json)); File.Move(json+".tmp",json,true);
        }
        SetStatus("已导入该短语的玄凤示范，口型为自动估计；达到熟练度后可自然复述。");
    }
    private static MouthCue[] EstimateMouth(byte[] pcm,int rate)
    {
        int step = Math.Max(1,rate/50)*2;
        var values = new List<double>();
        for (int i=0;i<pcm.Length;i+=step)
        {
            double sum=0; int end=Math.Min(i+step,pcm.Length);
            for (int j=i;j+1<end;j+=2) { double x=BitConverter.ToInt16(pcm,j)/32768.0;sum+=x*x; }
            values.Add(Math.Sqrt(sum/Math.Max(1,(end-i)/2)));
        }
        double threshold = Math.Max(.006,values.Max()*.12);
        var cues = new List<MouthCue> { new(0,MouthPose.Closed) }; int quiet=0;
        for (int i=1;i<values.Count;i++)
        {
            var pose=values[i]>=threshold ? (values[i]>threshold*2 ? MouthPose.Open : MouthPose.Small) : (++quiet>=2 ? MouthPose.Closed : cues[^1].Pose);
            if (values[i]>=threshold) quiet=0;
            if (pose!=cues[^1].Pose) cues.Add(new(i*step/(rate*2.0),pose));
        }
        if (cues[^1].Pose!=MouthPose.Closed) cues.Add(new(pcm.Length/(rate*2.0),MouthPose.Closed));
        return cues.ToArray();
    }
    private static void WriteWave(string path,byte[] pcm)
    { using var writer=new WaveFileWriter(path,new WaveFormat(16000,16,1));writer.Write(pcm,0,pcm.Length); }
    public void Dispose()
    {
        disposed=true;Cancel();
        // Give the killed child a bounded opportunity to remove its temporary recording.
        try { processing?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }
}

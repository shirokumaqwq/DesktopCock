using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopCock;

internal sealed record RecognizedPhrase(string Text, double Confidence, double Start, double End);
internal sealed class WhisperRecognizer
{
    internal static string RuntimeFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopCock","Speech");
    private readonly string folder;
    internal WhisperRecognizer(string? path = null) => folder = path ?? RuntimeFolder;
    private string Executable => Path.Combine(folder,"whisper-cli.exe");
    private string Model => Path.Combine(folder,"ggml-base.bin");
    private string Vad => Path.Combine(folder,"ggml-silero-v6.2.0.bin");
    internal bool Available => File.Exists(Executable) && File.Exists(Model) && File.Exists(Vad);
    internal async Task<IReadOnlyList<RecognizedPhrase>> Recognize(string wave, CancellationToken token)
    {
        if (!Available) throw new InvalidOperationException("请先安装本地语音组件；录音不会发送到网络。");
        var prefix = Path.ChangeExtension(wave,null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var start = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = folder };
        foreach (var argument in new[] { "-m",Model,"-f",wave,"-l","zh","-t","2","-ojf","-of",prefix,
            "--vad","-vm",Vad,"-vt","0.6","-vspd","250","-vsd","250","-ng" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("无法启动本地识别器");
        using var termination = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        });
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await stdout.ConfigureAwait(false); var errors = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("本地语音识别失败。请检查模型与运行库。"+
                (errors.Length > 0 ? " "+errors[^Math.Min(240,errors.Length)..] : ""));
            return Parse(await File.ReadAllTextAsync(prefix+".json", timeout.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch (InvalidOperationException) { }
            if (!token.IsCancellationRequested) throw new IOException("本地语音处理超时，请缩短教学片段。");
            throw;
        }
    }
    internal static IReadOnlyList<RecognizedPhrase> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = new List<RecognizedPhrase>();
        if (!document.RootElement.TryGetProperty("transcription",out var segments)) return results;
        foreach (var segment in segments.EnumerateArray())
        {
            var text = segment.GetProperty("text").GetString()?.Trim() ?? "";
            if (text.Contains('[') || text.Contains('(') || text.Contains('（') || text.Contains('【') ||
                string.IsNullOrWhiteSpace(text)) continue;
            var probabilities = new List<double>();
            if (segment.TryGetProperty("tokens",out var tokens))
                foreach (var item in tokens.EnumerateArray())
                {
                    var value = item.GetProperty("text").GetString() ?? "";
                    if (value.StartsWith('[') || string.IsNullOrWhiteSpace(value)) continue;
                    if (item.TryGetProperty("p",out var p) && p.TryGetDouble(out var probability)) probabilities.Add(probability);
                }
            if (probabilities.Count == 0) continue; // No invented confidence for unknown output formats.
            var confidence = probabilities.Average();
            var offsets = segment.GetProperty("offsets");
            var from = offsets.GetProperty("from").GetDouble()/1000;
            var to = offsets.GetProperty("to").GetDouble()/1000;
            if (confidence >= .55 && to-from >= .25) results.Add(new(text,confidence,from,to));
        }
        return results;
    }
}

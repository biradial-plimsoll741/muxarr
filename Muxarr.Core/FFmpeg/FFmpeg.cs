using System.Diagnostics;
using Muxarr.Core.Utilities;

namespace Muxarr.Core.FFmpeg;

/// <summary>
/// Wrapper around ffmpeg and ffprobe. Mirrors the surface of
/// <see cref="Muxarr.Core.MkvToolNix.MkvMerge"/> so both toolchains sit behind
/// the same shape where muxarr needs them (metadata edits on MP4, stream
/// probing, process lifecycle).
/// </summary>
public static class FFmpeg
{
    internal const string FfmpegExecutable = "ffmpeg";
    internal const string FfprobeExecutable = "ffprobe";

    /// <summary>
    /// ffmpeg returns 0 on success and anything non-zero on error. Unlike
    /// mkvmerge there is no "warnings-but-ok" code.
    /// </summary>
    public static bool IsSuccess(ProcessResult result) => result.ExitCode == 0;

    /// <summary>
    /// Probes a media file with ffprobe and returns its stream layout.
    /// </summary>
    public static async Task<ProcessJsonResult<FFprobeResult>> GetStreamInfo(string file)
    {
        var result = await ProcessExecutor.ExecuteProcessAsync(
            FfprobeExecutable,
            $"-v error -print_format json -show_streams -show_format \"{file}\"",
            TimeSpan.FromSeconds(30));

        var json = new ProcessJsonResult<FFprobeResult>(result);

        if (!IsSuccess(result) || string.IsNullOrEmpty(result.Output))
        {
            return json;
        }

        try
        {
            json.Result = JsonHelper.Deserialize<FFprobeResult>(result.Output);
        }
        catch (Exception e)
        {
            result.Error = e.ToString();
        }

        return json;
    }

    /// <summary>
    /// Runs ffmpeg and parses its <c>-progress pipe:1</c> stream into
    /// percentage updates. The caller includes the <c>-progress</c> option in
    /// the argument string; this method only handles parsing.
    /// </summary>
    /// <param name="onOutput">
    /// Receives <c>(line, percent, isStderr)</c> for every ffmpeg output line.
    /// <c>isStderr</c> is true for diagnostic lines and false for the
    /// structured progress stream, so callers can log the former and drop the latter.
    /// </param>
    public static async Task<ProcessResult> ExecuteAsync(
        string arguments,
        long durationMs,
        Action<string, int, bool>? onOutput = null,
        TimeSpan? timeout = null)
    {
        var lastProgress = 0;

        return await ProcessExecutor.ExecuteProcessAsync(
            FfmpegExecutable,
            arguments,
            timeout ?? TimeSpan.FromMinutes(60),
            onOutputLine: OnOutputLine);

        void OnOutputLine(string line, bool isError)
        {
            // -progress pipe:1 key=value lines arrive on stdout; diagnostics on stderr.
            if (!isError && line.StartsWith("out_time_us=", StringComparison.Ordinal))
            {
                var raw = line.Substring("out_time_us=".Length);
                if (long.TryParse(raw, out var outTimeUs) && durationMs > 0)
                {
                    var percent = (int)(outTimeUs / 1000 * 100 / durationMs);
                    lastProgress = Math.Clamp(percent, 0, 100);
                }
            }

            onOutput?.Invoke(line, lastProgress, isError);
        }
    }

    public static void KillExistingProcesses()
    {
        var processes = Process.GetProcesses().Where(p =>
        {
            try
            {
                return string.Equals(p.ProcessName, FfmpegExecutable, StringComparison.CurrentCultureIgnoreCase)
                       || string.Equals(p.ProcessName, FfprobeExecutable, StringComparison.CurrentCultureIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }).ToList();

        foreach (var process in processes)
        {
            try
            {
                process.Kill();
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}

using Muxarr.Core.Utilities;

namespace Muxarr.Tests.Integration;

/// <summary>
/// Resolves fixture paths for integration tests. Committed fixtures live in
/// Muxarr.Tests/Fixtures/; derived fixtures (MP4 variants) are generated once
/// per test-run into a shared pool directory and cached on source mtime.
/// Generation is triggered from [AssemblyInitialize] in IntegrationTestBase.
/// </summary>
public static class FixtureFactory
{
    public static readonly string SourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    public static readonly string PoolDir = Path.Combine(Path.GetTempPath(), "muxarr-it-pool");

    public static string Resolve(string name)
    {
        var fromSource = Path.Combine(SourceDir, name);
        if (File.Exists(fromSource))
        {
            return fromSource;
        }

        var fromPool = Path.Combine(PoolDir, name);
        if (File.Exists(fromPool))
        {
            return fromPool;
        }

        throw new FileNotFoundException($"Fixture '{name}' not found in source dir or pool.");
    }

    public static async Task EnsurePoolAsync()
    {
        Directory.CreateDirectory(PoolDir);

        await GenerateMp4FromMkvAsync("test.mkv", "test.mp4");
        await GenerateMp4FromMkvAsync("test_complex.mkv", "test_complex.mp4");
        await GenerateAsymmetricMkvAsync("asymmetric.mkv");
    }

    /// <summary>
    /// Generates a Matroska file with a short video track and a long audio
    /// track so the container duration (max of streams) is meaningfully
    /// larger than the video-only length. Used by the validator-rejection
    /// test: dropping the long audio track produces an output whose duration
    /// is shorter than the source, which the OutputValidator catches as
    /// truncation and triggers the .muxbak rollback path.
    /// </summary>
    private static async Task GenerateAsymmetricMkvAsync(string targetName)
    {
        var target = Path.Combine(PoolDir, targetName);
        if (File.Exists(target))
        {
            return;
        }

        // 3s video, 10s audio. Synthetic lavfi sources so the fixture is
        // deterministic and small (~10-20 KB). mpeg4 + ac3 picked because
        // they are in every reasonable ffmpeg build.
        var args =
            "-y -loglevel error " +
            "-f lavfi -i \"testsrc=duration=3:size=160x120:rate=10\" " +
            "-f lavfi -i \"sine=duration=10:frequency=440\" " +
            "-c:v mpeg4 -c:a ac3 " +
            $"\"{target}\"";
        var result = await ProcessExecutor.ExecuteProcessAsync("ffmpeg", args, TimeSpan.FromSeconds(60));
        if (!result.Success || !File.Exists(target))
        {
            Assert.Inconclusive($"Failed to generate {targetName}: {result.Error?.Trim()}");
        }
    }

    private static async Task GenerateMp4FromMkvAsync(string sourceName, string targetName)
    {
        var source = Path.Combine(SourceDir, sourceName);
        var target = Path.Combine(PoolDir, targetName);

        if (!File.Exists(source))
        {
            Assert.Inconclusive($"Source fixture '{sourceName}' missing at {source}.");
        }

        // Cache: skip regeneration if pool file is newer than the source.
        if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source))
        {
            return;
        }

        if (File.Exists(target))
        {
            File.Delete(target);
        }

        // Stream-copy video, transcode audio to AAC (MP4-friendly), map video
        // + audio only - drop subs whose codec MP4 cannot carry. Covers every
        // track shape in our committed MKV fixtures.
        var args = $"-y -loglevel error -i \"{source}\" -map 0:v -map 0:a -c:v copy -c:a aac \"{target}\"";
        var result = await ProcessExecutor.ExecuteProcessAsync("ffmpeg", args, TimeSpan.FromSeconds(60));
        if (!result.Success || !File.Exists(target))
        {
            Assert.Inconclusive($"Failed to generate {targetName} from {sourceName}: {result.Error?.Trim()}");
        }
    }
}

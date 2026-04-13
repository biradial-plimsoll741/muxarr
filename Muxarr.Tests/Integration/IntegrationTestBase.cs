namespace Muxarr.Tests.Integration;

/// <summary>
/// Common lifecycle for integration tests: binary preflight, shared fixture
/// pool generation, per-test temp dir, per-test DI fixture.
///
/// Integration tests assume ffmpeg, ffprobe, mkvmerge and mkvpropedit are on
/// PATH. If any is missing the assembly init marks the whole suite
/// inconclusive rather than failing with a raw Win32Exception stack.
/// </summary>
[TestClass]
public static class IntegrationAssemblyInit
{
    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext _)
    {
        foreach (var bin in new[] { "ffmpeg", "ffprobe", "mkvmerge", "mkvpropedit" })
        {
            if (!IntegrationTestBase.BinaryOnPath(bin))
            {
                Assert.Inconclusive($"{bin} not on PATH; integration tests require all four tool binaries.");
            }
        }

        await FixtureFactory.EnsurePoolAsync();
    }
}

[TestCategory("Integration")]
public abstract class IntegrationTestBase
{
    public TestContext TestContext { get; set; } = null!;

    protected string TempDir { get; private set; } = null!;
    protected ConverterIntegrationFixture Fixture { get; private set; } = null!;

    [TestInitialize]
    public async Task BaseSetup()
    {
        TempDir = Path.Combine(Path.GetTempPath(), "muxarr-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);
        Fixture = await ConverterIntegrationFixture.CreateAsync(TempDir);
    }

    [TestCleanup]
    public void BaseTeardown()
    {
        try
        {
            Fixture?.Dispose();
        }
        catch
        {
            // best effort
        }

        try
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    internal static bool BinaryOnPath(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full))
                {
                    return true;
                }

                if (OperatingSystem.IsWindows() && File.Exists(full + ".exe"))
                {
                    return true;
                }
            }
            catch
            {
                // skip malformed path entries
            }
        }
        return false;
    }
}

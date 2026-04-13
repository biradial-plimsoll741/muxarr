using Microsoft.EntityFrameworkCore;
using Muxarr.Core.Extensions;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;

namespace Muxarr.Tests.Integration;

/// <summary>
/// Real-scanner tests: invokes MediaScannerService.ScanFile against the
/// committed .mkv fixtures and the derived .mp4 fixtures, asserts the
/// persisted MediaFile + MediaTrack rows reflect the probe output.
/// </summary>
[TestClass]
public class MediaScannerIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task Scan_SimpleMkv_PersistsVideoAndAudioTracks()
    {
        var path = Fixture.MaterializeFixture("test.mkv");
        var profile = await Fixture.SeedProfile();

        await Fixture.Scanner.ScanFile(path, forceRescan: true, profile);

        var file = await Fixture.WithDbContext(async ctx =>
            await ctx.MediaFiles.WithTracks().FirstAsync(f => f.Path == path));

        Assert.AreEqual("Matroska", file.ContainerType);
        Assert.AreEqual(ContainerFamily.Matroska, file.ContainerType.ToContainerFamily());
        Assert.IsTrue(file.DurationMs > 0, "duration should be populated");
        Assert.IsTrue(file.Size > 0, "size should be populated");
        Assert.IsTrue(file.Tracks.Count >= 2, $"expected >=2 tracks, got {file.Tracks.Count}");
        Assert.IsTrue(file.Tracks.Any(t => t.Type == MediaTrackType.Video));
        Assert.IsTrue(file.Tracks.Any(t => t.Type == MediaTrackType.Audio));
    }

    [TestMethod]
    public async Task Scan_ComplexMkv_ParsesNineTracksWithExpectedFlags()
    {
        var path = Fixture.MaterializeFixture("test_complex.mkv");
        var profile = await Fixture.SeedProfile();

        await Fixture.Scanner.ScanFile(path, forceRescan: true, profile);

        var file = await Fixture.WithDbContext(async ctx =>
            await ctx.MediaFiles.WithTracks().FirstAsync(f => f.Path == path));

        Assert.AreEqual(9, file.Tracks.Count);
        Assert.AreEqual(9, file.TrackCount);

        var tracks = file.Tracks.OrderBy(t => t.TrackNumber).ToList();
        Assert.AreEqual(MediaTrackType.Video, tracks[0].Type);
        Assert.AreEqual(3, tracks.Count(t => t.Type == MediaTrackType.Audio));
        Assert.AreEqual(5, tracks.Count(t => t.Type == MediaTrackType.Subtitles));

        // Spot-check a few flags we know from the committed fixture (mirrors
        // FFprobeComplexTests.SetFileDataFromFFprobe_ParsesAllFlagsFromComplexFile)
        var defaultAudio = tracks.First(t => t.Type == MediaTrackType.Audio && t.IsDefault);
        Assert.AreEqual("English", defaultAudio.LanguageName);
        Assert.IsTrue(tracks.Any(t => t.Type == MediaTrackType.Audio && t.IsCommentary));
        Assert.IsTrue(tracks.Any(t => t.Type == MediaTrackType.Subtitles && t.IsForced));
        Assert.IsTrue(tracks.Any(t => t.Type == MediaTrackType.Subtitles && t.IsHearingImpaired));
    }

    [TestMethod]
    public async Task Scan_DerivedMp4_PersistsAsMp4Container()
    {
        var path = Fixture.MaterializeFixture("test.mp4");
        var profile = await Fixture.SeedProfile();

        await Fixture.Scanner.ScanFile(path, forceRescan: true, profile);

        var file = await Fixture.WithDbContext(async ctx =>
            await ctx.MediaFiles.WithTracks().FirstAsync(f => f.Path == path));

        Assert.AreEqual(ContainerFamily.Mp4, file.ContainerType.ToContainerFamily(),
            $"expected Mp4 family, container was: {file.ContainerType}");
        Assert.IsTrue(file.Tracks.Any(t => t.Type == MediaTrackType.Video));
        Assert.IsTrue(file.Tracks.Any(t => t.Type == MediaTrackType.Audio));
    }
}

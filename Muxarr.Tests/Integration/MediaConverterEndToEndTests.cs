using Muxarr.Core.Extensions;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;

namespace Muxarr.Tests.Integration;

/// <summary>
/// End-to-end conversion tests: seeds a MediaConversion in the DB, runs the
/// real MediaConverterService pipeline (ffprobe scan, real mkvpropedit /
/// mkvmerge), asserts on the resulting file and DB state.
///
/// Uses IsCustomConversion = true so the converter keeps the TargetSnapshot
/// we craft rather than rebuilding it from the profile (non-custom path
/// calls BuildTargetSnapshot which filters by profile settings).
/// </summary>
[TestClass]
public class MediaConverterEndToEndTests : IntegrationTestBase
{
    [TestMethod]
    public async Task Skip_WhenTargetEqualsCurrent_LeavesFileByteIdentical()
    {
        var path = Fixture.MaterializeFixture("test_complex.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        var hashBefore = FileAssertions.Sha256(path);
        var sizeBefore = new FileInfo(path).Length;

        // Target = current state, flagged as custom so the converter doesn't
        // rebuild the target from the (empty) profile and silently filter tracks.
        var target = file.ToMediaSnapshot();
        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Completed, result.State,
            $"expected Completed, got {result.State}. Log: {result.Log}");
        Assert.AreEqual(100, result.Progress);
        Assert.AreEqual(0, result.SizeDifference);
        Assert.IsTrue(File.Exists(path), "original file should still exist");
        FileAssertions.AssertSha256Equals(path, hashBefore, "Skip path must not touch the file");
        Assert.AreEqual(sizeBefore, new FileInfo(path).Length);
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    [TestMethod]
    public async Task MetadataEdit_Matroska_FlipsDefaultFlagInPlace()
    {
        var path = Fixture.MaterializeFixture("test_complex.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        // Find the current default audio track and pick a non-default one to
        // promote. Guarantees exactly one flag flip.
        var currentDefault = file.Tracks.First(t => t.Type == MediaTrackType.Audio && t.IsDefault);
        var newDefault = file.Tracks.First(t => t.Type == MediaTrackType.Audio && !t.IsDefault);

        var targetTracks = file.Tracks.ToSnapshots();
        targetTracks.First(t => t.TrackNumber == currentDefault.TrackNumber).IsDefault = false;
        targetTracks.First(t => t.TrackNumber == newDefault.TrackNumber).IsDefault = true;
        var target = file.ToMediaSnapshot(targetTracks);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Completed, result.State,
            $"expected Completed, got {result.State}. Log: {result.Log}");

        // Re-probe the real file; the flags must actually have changed.
        var probed = await FileAssertions.ProbeAsync(path);
        var promoted = probed.Tracks.First(t => t.TrackNumber == newDefault.TrackNumber);
        var demoted = probed.Tracks.First(t => t.TrackNumber == currentDefault.TrackNumber);
        Assert.IsTrue(promoted.IsDefault, $"track #{newDefault.TrackNumber} should be default after mkvpropedit");
        Assert.IsFalse(demoted.IsDefault, $"track #{currentDefault.TrackNumber} should no longer be default");
        Assert.AreEqual(9, probed.Tracks.Count, "metadata edit must not change track count");
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    [TestMethod]
    public async Task Remux_Matroska_DropsSubtitleTrack()
    {
        var path = Fixture.MaterializeFixture("test_complex.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        var originalTrackCount = file.Tracks.Count;
        var originalSubCount = file.Tracks.Count(t => t.Type == MediaTrackType.Subtitles);
        var droppedSub = file.Tracks.First(t => t.Type == MediaTrackType.Subtitles);

        var keptTracks = file.Tracks
            .Where(t => t.TrackNumber != droppedSub.TrackNumber)
            .ToSnapshots();
        var target = file.ToMediaSnapshot(keptTracks);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Completed, result.State,
            $"expected Completed, got {result.State}. Log: {result.Log}");
        Assert.IsTrue(result.SizeAfter > 0, "size after must be populated");

        // mkvmerge renumbers remaining tracks from 0 so we can't assert by
        // original TrackNumber; assert on counts per type instead.
        var probed = await FileAssertions.ProbeAsync(path);
        Assert.AreEqual(originalTrackCount - 1, probed.Tracks.Count,
            "remux must reduce track count by exactly one");
        Assert.AreEqual(originalSubCount - 1,
            probed.Tracks.Count(t => t.Type == MediaTrackType.Subtitles),
            "subtitle track count must drop by one");
        await FileAssertions.AssertContainerFamily(path, ContainerFamily.Matroska);
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    [TestMethod]
    public async Task Remux_Mp4_DropsAudioTrack_ViaFFmpeg()
    {
        var path = Fixture.MaterializeFixture("test_complex.mp4");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        var originalTrackCount = file.Tracks.Count;
        var originalAudioCount = file.Tracks.Count(t => t.Type == MediaTrackType.Audio);
        Assert.IsTrue(originalAudioCount >= 2,
            $"test_complex.mp4 should have >=2 audio tracks to drop one; got {originalAudioCount}");

        var droppedAudio = file.Tracks.Last(t => t.Type == MediaTrackType.Audio);
        var keptTracks = file.Tracks
            .Where(t => t.TrackNumber != droppedAudio.TrackNumber)
            .ToSnapshots();
        var target = file.ToMediaSnapshot(keptTracks);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Completed, result.State,
            $"expected Completed, got {result.State}. Log: {result.Log}");

        var probed = await FileAssertions.ProbeAsync(path);
        Assert.AreEqual(originalTrackCount - 1, probed.Tracks.Count);
        Assert.AreEqual(originalAudioCount - 1,
            probed.Tracks.Count(t => t.Type == MediaTrackType.Audio));
        await FileAssertions.AssertContainerFamily(path, ContainerFamily.Mp4);
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    [TestMethod]
    public async Task Remux_CorruptSource_FailsGracefully_OriginalPreserved()
    {
        // Seed a healthy scan first so the MediaFile row reflects the good
        // state, then corrupt the bytes on disk before the converter runs.
        // Whichever error path trips (ffprobe failure, stale-target check,
        // tool error, or validator rejection), two things must hold:
        //   - conversion state ends up Failed
        //   - original path still holds a readable file (either never touched
        //     or restored from .muxbak)
        var path = Fixture.MaterializeFixture("test_complex.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        var droppedSub = file.Tracks.First(t => t.Type == MediaTrackType.Subtitles);
        var keptTracks = file.Tracks
            .Where(t => t.TrackNumber != droppedSub.TrackNumber)
            .ToSnapshots();
        var target = file.ToMediaSnapshot(keptTracks);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        CorruptFile(path);
        var corruptedSize = new FileInfo(path).Length;

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Failed, result.State,
            $"expected Failed, got {result.State}. Log: {result.Log}");
        Assert.IsTrue(File.Exists(path),
            "original path must still hold a file after a failed conversion");
        Assert.AreEqual(corruptedSize, new FileInfo(path).Length,
            "the file at the original path must be byte-identical to what we left there before the run");
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    private static void CorruptFile(string path)
    {
        // Overwrite a 4 KB chunk at ~10% into the file. Keeps the EBML header
        // readable (so ffprobe still parses track metadata) but mangles the
        // payload - mkvmerge either errors out or emits a warning that fails
        // the validator's "new scan warning on output" check.
        using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var offset = fs.Length / 10;
        fs.Seek(offset, SeekOrigin.Begin);
        var garbage = new byte[4096];
        new Random(1337).NextBytes(garbage);
        fs.Write(garbage);
    }

    [TestMethod]
    public async Task Remux_OutputTooShort_ValidatorRejects_RestoresFromBackup()
    {
        // Real validator rejection, no stubs. asymmetric.mkv has a 3s video
        // track and a 10s audio track, so its container duration is 10s.
        // Target drops the long audio, so mkvmerge produces a 3s output.
        // OutputValidator.ValidateOrThrow compares actual.DurationMs (3000)
        // against source.DurationMs (10000) with a tolerance of
        // max(500ms, 1%) = 500ms and throws, triggering the .muxbak rollback.
        var path = Fixture.MaterializeFixture("asymmetric.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        Assert.IsTrue(file.DurationMs >= 9000,
            $"asymmetric fixture should report >=9s container duration, got {file.DurationMs}ms");

        var hashBefore = FileAssertions.Sha256(path);
        var sizeBefore = new FileInfo(path).Length;

        // Keep only the video track. The long audio gets dropped, output is
        // 3s, validator trips on the duration tolerance.
        var videoOnly = file.Tracks
            .Where(t => t.Type == MediaTrackType.Video)
            .ToSnapshots();
        var target = file.ToMediaSnapshot(videoOnly);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Failed, result.State,
            $"expected Failed, got {result.State}. Log: {result.Log}");
        StringAssert.Contains(result.Log, "duration",
            $"log should mention the duration mismatch that tripped the validator. Log: {result.Log}");

        // The core rollback guarantee: the file at the original path is the
        // byte-identical pre-conversion source, restored from .muxbak.
        Assert.IsTrue(File.Exists(path), "original file must still exist");
        Assert.AreEqual(sizeBefore, new FileInfo(path).Length,
            "restored file must match pre-conversion size");
        FileAssertions.AssertSha256Equals(path, hashBefore,
            "restored file must be byte-identical to the pre-conversion source");
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }

    [TestMethod]
    public async Task CustomConversion_StaleTarget_FailsWithClearMessage()
    {
        // Regression guard for the fix shipped on 2026-04-12: a custom
        // conversion whose TargetSnapshot references a track number that no
        // longer exists in the rescanned source must fail fast with a clear
        // "source has changed" message instead of silently producing a
        // mis-targeted file.
        var path = Fixture.MaterializeFixture("test.mkv");
        var profile = await Fixture.SeedProfile();
        var file = await Fixture.ScanAndPersist(path, profile);

        // Build a target that includes a phantom track number beyond anything
        // real in the source. The rescan at HandleConversion time will list
        // the real tracks, the fix's validation will flag #99 as missing.
        var tracks = file.Tracks.ToSnapshots();
        tracks.Add(new TrackSnapshot
        {
            Type = MediaTrackType.Audio,
            TrackNumber = 99,
            LanguageCode = "eng",
            LanguageName = "English",
            Codec = "Aac"
        });
        var target = file.ToMediaSnapshot(tracks);

        var conversion = await Fixture.SeedConversion(file, target, custom: true);

        await Fixture.Converter.RunAsync(CancellationToken.None);

        var result = await Fixture.ReloadConversion(conversion.Id);
        Assert.AreEqual(ConversionState.Failed, result.State,
            $"expected Failed, got {result.State}. Log: {result.Log}");
        StringAssert.Contains(result.Log, "Source file has changed",
            "failure log must explain why the conversion was rejected");
        StringAssert.Contains(result.Log, "99",
            "log should name the missing track number");
        FileAssertions.AssertNoStrayArtifacts(TempDir, Path.GetFileName(path));
    }
}

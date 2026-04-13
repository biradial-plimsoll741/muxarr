using System.Security.Cryptography;
using Muxarr.Core.Extensions;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;

namespace Muxarr.Tests.Integration;

/// <summary>
/// Probe-based assertion helpers. Always goes through real ffprobe so asserts
/// stay honest across ffmpeg builds - no byte-level comparison except
/// <see cref="AssertSha256Equals"/>, which is reserved for the Skip scenario
/// where the file must not be touched at all.
/// </summary>
public static class FileAssertions
{
    public static async Task<MediaFile> ProbeAsync(string path)
    {
        var file = new MediaFile { Path = path };
        var probe = await file.SetFileDataFromFFprobe();
        Assert.IsNotNull(probe.Result, $"ffprobe failed for {path}: {probe.Error?.Trim()}");
        return file;
    }

    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public static void AssertSha256Equals(string path, string expectedHash, string message = "")
    {
        var actual = Sha256(path);
        Assert.AreEqual(expectedHash, actual, $"SHA256 mismatch for {path}. {message}");
    }

    public static async Task AssertTrackCount(string path, int expected)
    {
        var file = await ProbeAsync(path);
        Assert.AreEqual(expected, file.Tracks.Count,
            $"Track count mismatch for {path}. Got: {string.Join(",", file.Tracks.Select(t => $"#{t.TrackNumber}:{t.Type}"))}");
    }

    public static async Task AssertContainerFamily(string path, ContainerFamily expected)
    {
        var file = await ProbeAsync(path);
        var actual = file.ContainerType.ToContainerFamily();
        Assert.AreEqual(expected, actual,
            $"Container family mismatch for {path}. ContainerType was: {file.ContainerType}");
    }

    public static async Task AssertTrackFlag(string path, int trackNumber, Func<MediaTrack, bool> predicate,
        string description)
    {
        var file = await ProbeAsync(path);
        var track = file.Tracks.FirstOrDefault(t => t.TrackNumber == trackNumber);
        Assert.IsNotNull(track, $"Track #{trackNumber} not found in {path}");
        Assert.IsTrue(predicate(track), $"Track #{trackNumber} in {path} did not satisfy: {description}");
    }

    public static void AssertNoStrayArtifacts(string directory, string originalFileName)
    {
        var muxtmp = Path.Combine(directory, originalFileName + ".muxtmp");
        var muxbak = Path.Combine(directory, originalFileName + ".muxbak");
        Assert.IsFalse(File.Exists(muxtmp), $"Unexpected leftover .muxtmp: {muxtmp}");
        Assert.IsFalse(File.Exists(muxbak), $"Unexpected leftover .muxbak: {muxbak}");
    }
}

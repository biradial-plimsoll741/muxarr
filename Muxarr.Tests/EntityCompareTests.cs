using System.Reflection;
using Muxarr.Core.Extensions;
using Muxarr.Core.Models;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;
using static Muxarr.Tests.TestData;

namespace Muxarr.Tests;

[TestClass]
public class EntityCompareTests
{
    [TestMethod]
    public void Equal_IdenticalTracks_True()
    {
        Assert.IsTrue(EntityCompare.Equal(Audio(1, "English"), Audio(1, "English")));
    }

    [TestMethod]
    public void Equal_IgnoredFields_DontMatter()
    {
        var a = Audio(1);
        var b = Audio(1);
        a.Id = 1; b.Id = 999;
        a.SnapshotId = 5; b.SnapshotId = 7;
        Assert.IsTrue(EntityCompare.Equal(a, b));
    }

    [TestMethod]
    public void Equal_NestedTrackChanged_False()
    {
        var a = MakeFile(null, Video(), Audio(1, "English")).Snapshot;
        var b = MakeFile(null, Video(), Audio(1, "English")).Snapshot;
        b.Tracks[1].IsCommentary = true;
        Assert.IsFalse(EntityCompare.Equal(a, b));
    }

    // Coverage: if you add a new compared scalar and forget about it,
    // this fails with the property name attached.
    [TestMethod]
    public void Equal_EveryComparedScalar_BreaksEquality_Track()
        => AssertEveryScalarAffectsEquality(() => Audio(1, "English", trackName: "English"));

    [TestMethod]
    public void Equal_EveryComparedScalar_BreaksEquality_Snapshot()
        => AssertEveryScalarAffectsEquality(() =>
        {
            var s = MakeFile(null, Video(), Audio(1, "English")).Snapshot;
            s.ContainerType = "Matroska";
            s.Resolution = "1920x1080";
            s.DurationMs = 1000;
            s.VideoBitDepth = 10;
            s.HasChapters = true;
            return s;
        });

    [TestMethod]
    public void ToSnapshot_PreservesAllComparedFields()
    {
        var source = Audio(42, "French", codec: "Eac3", channels: 8,
            commentary: true, hi: true, isDefault: true, dub: true,
            isOriginal: true, trackName: "French Dub", languageCode: "fre");
        Assert.IsTrue(EntityCompare.Equal(source, ((IMediaTrack)source).ToSnapshot()));
    }

    private static void AssertEveryScalarAffectsEquality<T>(Func<T> factory) where T : class
    {
        var scalars = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite
                        && p.GetCustomAttribute<CompareIgnoreAttribute>() is null
                        && IsSimpleScalar(p.PropertyType));

        foreach (var prop in scalars)
        {
            var a = factory();
            var b = factory();
            prop.SetValue(b, Distinct(prop.PropertyType, prop.GetValue(b)));
            Assert.IsFalse(EntityCompare.Equal(a, b),
                $"EntityCompare missed change on {typeof(T).Name}.{prop.Name}");
        }
    }

    private static bool IsSimpleScalar(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || u == typeof(string);
    }

    private static object? Distinct(Type type, object? current)
    {
        var u = Nullable.GetUnderlyingType(type) ?? type;
        if (u == typeof(bool)) return !(bool)(current ?? false);
        if (u == typeof(string)) return (string?)current == "__x" ? "__y" : "__x";
        if (u.IsEnum) return Enum.GetValues(u).Cast<object>().First(v => !Equals(v, current));
        if (u.IsPrimitive) return Convert.ChangeType(Convert.ToInt64(current ?? 0) == 0 ? 1 : 0, u);
        throw new NotSupportedException(u.Name);
    }
}

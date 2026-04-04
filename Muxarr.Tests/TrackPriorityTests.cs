using Muxarr.Core.Extensions;
using Muxarr.Core.Language;
using Muxarr.Core.MkvToolNix;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;
using static Muxarr.Tests.TestData;

namespace Muxarr.Tests;

[TestClass]
public class TrackPriorityTests
{
    // --- Quality Scoring ---

    [TestMethod]
    public void QualityScore_LosslessSpatial_HigherThanLossless()
    {
        var truhdAtmos = Audio(codec: "TrueHd", channels: 8, trackName: "TrueHD Atmos 7.1");
        var truhd = Audio(codec: "TrueHd", channels: 8);

        Assert.IsTrue(
            TrackQualityScorer.ScoreTrack(truhdAtmos) > TrackQualityScorer.ScoreTrack(truhd));
    }

    [TestMethod]
    public void QualityScore_Lossless_HigherThanLossy()
    {
        var flac = Audio(codec: "Flac", channels: 2);
        var aac = Audio(codec: "Aac", channels: 2);

        Assert.IsTrue(TrackQualityScorer.ScoreTrack(flac) > TrackQualityScorer.ScoreTrack(aac));
    }

    [TestMethod]
    public void QualityScore_MoreChannels_HigherWithinSameTier()
    {
        var ac3_51 = Audio(codec: "Ac3", channels: 6);
        var ac3_20 = Audio(codec: "Ac3", channels: 2);

        Assert.IsTrue(TrackQualityScorer.ScoreTrack(ac3_51) > TrackQualityScorer.ScoreTrack(ac3_20));
    }

    [TestMethod]
    public void QualityScore_SmallestSize_InvertsRanking()
    {
        var truhd = Audio(codec: "TrueHd", channels: 8);
        var aac = Audio(codec: "Aac", channels: 2);

        Assert.IsTrue(
            TrackQualityScorer.ScoreTrack(aac, AudioQualityStrategy.SmallestSize) >
            TrackQualityScorer.ScoreTrack(truhd, AudioQualityStrategy.SmallestSize));
    }

    [TestMethod]
    public void QualityScore_Subtitles_TextPreferredOverBitmap()
    {
        var srt = Sub();
        var pgs = Sub(codec: "Pgs");

        Assert.IsTrue(TrackQualityScorer.ScoreTrack(srt) > TrackQualityScorer.ScoreTrack(pgs));
    }

    // --- MaxTracks (Deduplication) ---

    [TestMethod]
    public void MaxTracks_KeepsBestQualityTrack()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            AllowedLanguages =
            [
                new LanguagePreference(IsoLanguage.Find("English")) { MaxTracks = 1 }
            ]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "English", "TrueHd", 8),
            Audio(3, "English", "Ac3", 6),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("TrueHd", result[0].Codec);
    }

    [TestMethod]
    public void MaxTracks_SmallestSize_KeepsLowestQuality()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            AllowedLanguages =
            [
                new LanguagePreference(IsoLanguage.Find("English"))
                {
                    MaxTracks = 1,
                    QualityStrategy = AudioQualityStrategy.SmallestSize
                }
            ]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "TrueHd", 8),
            Audio(2, "English", "Aac", 2),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Aac", result[0].Codec);
    }

    [TestMethod]
    public void MaxTracks_KeepTwo_KeepsTopTwo()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            AllowedLanguages =
            [
                new LanguagePreference(IsoLanguage.Find("English")) { MaxTracks = 2 }
            ]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "English", "TrueHd", 8),
            Audio(3, "English", "Ac3", 6),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(2, result.Count);
        // Best two: TrueHd 7.1 and AC3 5.1
        Assert.IsTrue(result.Any(t => t.Codec == "TrueHd"));
        Assert.IsTrue(result.Any(t => t.Codec == "Ac3"));
    }

    [TestMethod]
    public void MaxTracks_Null_KeepsAll()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            AllowedLanguages = [IsoLanguage.Find("English")]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "English", "TrueHd", 8),
            Audio(3, "English", "Ac3", 6),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void MaxTracks_PerLanguage_IndependentLimits()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            AllowedLanguages =
            [
                new LanguagePreference(IsoLanguage.Find("English")) { MaxTracks = 1 },
                new LanguagePreference(IsoLanguage.Find("Japanese")) { MaxTracks = 2 }
            ]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "TrueHd", 8),
            Audio(2, "English", "Aac", 2),
            Audio(3, "Japanese", "Flac", 2),
            Audio(4, "Japanese", "Aac", 2),
            Audio(5, "Japanese", "Ac3", 6),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(1, result.Count(t => t.LanguageName == "English"));
        Assert.AreEqual(2, result.Count(t => t.LanguageName == "Japanese"));
    }

    // --- Language Priority Reordering ---

    [TestMethod]
    public void Priority_ReordersTracksByLanguageListOrder()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            ApplyLanguagePriority = true,
            ReorderStrategy = TrackReorderStrategy.MatchLanguagePriority,
            AllowedLanguages = [IsoLanguage.Find("Japanese"), IsoLanguage.Find("English")]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "Japanese", "Aac", 2),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Japanese", result[0].LanguageName);
        Assert.AreEqual("English", result[1].LanguageName);
    }

    [TestMethod]
    public void Priority_PreservesSourceOrderWithinSameLanguage()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            ApplyLanguagePriority = true,
            AllowedLanguages = [IsoLanguage.Find("English")]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "TrueHd", 8),
            Audio(2, "English", "Aac", 2),
            Audio(3, "English", "Ac3", 6),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(3, result.Count);
        // Source order preserved within English
        Assert.AreEqual(1, result[0].TrackNumber);
        Assert.AreEqual(2, result[1].TrackNumber);
        Assert.AreEqual(3, result[2].TrackNumber);
    }

    [TestMethod]
    public void Priority_Disabled_PreservesSourceOrder()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            ApplyLanguagePriority = false,
            AllowedLanguages = [IsoLanguage.Find("Japanese"), IsoLanguage.Find("English")]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "Japanese", "Aac", 2),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual("English", result[0].LanguageName);
        Assert.AreEqual("Japanese", result[1].LanguageName);
    }

    [TestMethod]
    public void Priority_OriginalLanguage_UsesPlaceholderPosition()
    {
        var settings = new TrackSettings
        {
            Enabled = true,
            ApplyLanguagePriority = true,
            ReorderStrategy = TrackReorderStrategy.MatchLanguagePriority,
            AllowedLanguages =
            [
                IsoLanguage.OriginalLanguage,     // position 0
                IsoLanguage.Find("English"),       // position 1
            ]
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "English", "Aac", 2),
            Audio(2, "Japanese", "Aac", 2),
        };

        // Japanese is the original language, should sort to position 0
        var result = tracks.GetAllowedTracks(settings, "Japanese");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Japanese", result[0].LanguageName);
        Assert.AreEqual("English", result[1].LanguageName);
    }

    // --- Default Flag Reassignment ---

    [TestMethod]
    public void DefaultFlag_SpecCompliant_AllNormalTracksEligible()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                DefaultStrategy = DefaultTrackStrategy.SpecCompliant,
                AllowedLanguages = [IsoLanguage.Find("English"), IsoLanguage.Find("Spanish")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "English", "Aac", 2, isDefault: false),
            Audio(2, "Spanish", "Aac", 2, isDefault: false)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);

        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();
        Assert.IsTrue(audioOutputs[0].IsDefault == true);   // English = normal, eligible
        Assert.IsTrue(audioOutputs[1].IsDefault == true);   // Spanish = normal, eligible
    }

    [TestMethod]
    public void DefaultFlag_SpecCompliant_CommentaryNotEligible()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                DefaultStrategy = DefaultTrackStrategy.SpecCompliant,
                AllowedLanguages = [IsoLanguage.Find("English")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "English", "Aac", 6),
            Audio(2, "English", "Aac", 2, commentary: true)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);

        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();
        Assert.IsTrue(audioOutputs[0].IsDefault == true);    // Normal track = eligible
        Assert.IsTrue(audioOutputs[1].IsDefault == false);   // Commentary = not eligible
    }

    [TestMethod]
    public void DefaultFlag_ForceFirstLanguage_OnlyFirstIsDefault()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                DefaultStrategy = DefaultTrackStrategy.ForceFirstLanguage,
                AllowedLanguages = [IsoLanguage.Find("English"), IsoLanguage.Find("Spanish")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "English", "Aac", 2, isDefault: false),
            Audio(2, "Spanish", "Aac", 2, isDefault: true)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);

        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();
        Assert.IsTrue(audioOutputs[0].IsDefault == true);   // English = first priority
        Assert.IsTrue(audioOutputs[1].IsDefault == false);   // Spanish = second
    }

    [TestMethod]
    public void DefaultFlag_PriorityDisabled_PreservesOriginalDefault()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = false,
                AllowedLanguages = [IsoLanguage.Find("English"), IsoLanguage.Find("Spanish")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "English", "Aac", 2, isDefault: false),
            Audio(2, "Spanish", "Aac", 2, isDefault: true)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);

        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();
        Assert.IsTrue(audioOutputs[0].IsDefault == false);  // English preserved as non-default
        Assert.IsTrue(audioOutputs[1].IsDefault == true);   // Spanish preserved as default
    }

    [TestMethod]
    public void DefaultFlag_CustomConversion_NotReassigned()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                AllowedLanguages = [IsoLanguage.Find("English"), IsoLanguage.Find("Spanish")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "English", "Aac", 2, isDefault: false),
            Audio(2, "Spanish", "Aac", 2, isDefault: true)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(),
            isCustomConversion: true);

        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();
        Assert.IsTrue(audioOutputs[0].IsDefault == false);  // Custom: flags not touched
        Assert.IsTrue(audioOutputs[1].IsDefault == true);
    }

    // --- Preview matches BuildTrackOutputs ---

    [TestMethod]
    public void Preview_ShowsReorderedTracksWithCorrectDefault()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                ReorderStrategy = TrackReorderStrategy.MatchLanguagePriority,
                DefaultStrategy = DefaultTrackStrategy.ForceFirstLanguage,
                AllowedLanguages = [IsoLanguage.Find("Japanese"), IsoLanguage.Find("English")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("Japanese",
            Audio(1, "English", "Aac", 2, isDefault: true),
            Audio(2, "Japanese", "Aac", 2, isDefault: false)
        );

        var previews = file.GetPreviewTracks(profile);
        var audioPreviews = previews.Where(p => p.Type == MediaTrackType.Audio).ToList();

        Assert.AreEqual("Japanese", audioPreviews[0].LanguageName);
        Assert.IsTrue(audioPreviews[0].IsDefault);
        Assert.AreEqual("English", audioPreviews[1].LanguageName);
        Assert.IsFalse(audioPreviews[1].IsDefault);
    }

    // --- Combined: MaxTracks + Priority + Default ---

    [TestMethod]
    public void Combined_DeduplicateReorderAndSetDefault()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                ReorderStrategy = TrackReorderStrategy.MatchLanguagePriority,
                DefaultStrategy = DefaultTrackStrategy.ForceFirstLanguage,
                AllowedLanguages =
                [
                    new LanguagePreference(IsoLanguage.Find("English")) { MaxTracks = 1 },
                    IsoLanguage.Find("Spanish"),
                ]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        var file = MakeFile("English",
            Audio(1, "Spanish", "Ac3", 6, isDefault: true),
            Audio(2, "English", "TrueHd", 8),
            Audio(3, "English", "Aac", 2)
        );

        var allowed = file.GetAllowedTracks(profile);

        // English deduped to 1 (TrueHD best), Spanish kept, reordered English first
        Assert.AreEqual(2, allowed.Count);
        Assert.AreEqual("English", allowed[0].LanguageName);
        Assert.AreEqual("TrueHd", allowed[0].Codec);
        Assert.AreEqual("Spanish", allowed[1].LanguageName);

        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);
        var audioOutputs = outputs.Where(o => o.Type == MkvMerge.AudioTrack).ToList();

        Assert.IsTrue(audioOutputs[0].IsDefault == true);   // English = default
        Assert.IsTrue(audioOutputs[1].IsDefault == false);   // Spanish = not default
    }

    // --- Independent features: reorder without removal, defaults without matching tracks ---

    [TestMethod]
    public void Reorder_Alphabetical_WorksWithoutEnabledFilter()
    {
        var settings = new TrackSettings
        {
            Enabled = false,
            ReorderStrategy = TrackReorderStrategy.Alphabetical
        };

        var tracks = new List<MediaTrack>
        {
            Audio(1, "Japanese", "Aac", 2),
            Audio(2, "English", "Aac", 2),
            Audio(3, "Dutch", "Aac", 2),
        };

        var result = tracks.GetAllowedTracks(settings, null);

        Assert.AreEqual(3, result.Count, "No tracks should be removed when Enabled=false");
        Assert.AreEqual("Dutch", result[0].LanguageName);
        Assert.AreEqual("English", result[1].LanguageName);
        Assert.AreEqual("Japanese", result[2].LanguageName);
    }

    [TestMethod]
    public void ForceFirstLanguage_NoMatchingTracks_PreservesOriginalDefaults()
    {
        var profile = new Profile
        {
            AudioSettings = new TrackSettings
            {
                Enabled = true,
                ApplyLanguagePriority = true,
                DefaultStrategy = DefaultTrackStrategy.ForceFirstLanguage,
                AllowedLanguages = [IsoLanguage.Find("English")]
            },
            SubtitleSettings = new TrackSettings { Enabled = false }
        };

        // File only has French audio (kept via fallback), no English tracks at all
        var file = MakeFile("French",
            Audio(1, "French", "Aac", 6, isDefault: true)
        );

        var allowed = file.GetAllowedTracks(profile);
        var outputs = file.BuildTrackOutputs(profile, allowed.ToSnapshots(), file.Tracks.ToSnapshots(), false);

        var audioOutput = outputs.First(o => o.Type == MkvMerge.AudioTrack);
        Assert.IsTrue(audioOutput.IsDefault == true,
            "Original default flag should be preserved when no tracks match the priority list");
    }

}

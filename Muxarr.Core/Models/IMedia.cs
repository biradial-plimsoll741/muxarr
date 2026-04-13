namespace Muxarr.Core.Models;

public interface IMedia<TTrack> where TTrack : IMediaTrack
{
    List<TTrack> Tracks { get; }
    bool HasChapters { get; }
    bool HasAttachments { get; }
}

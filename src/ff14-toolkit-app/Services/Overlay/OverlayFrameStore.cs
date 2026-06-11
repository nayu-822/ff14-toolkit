namespace FF14Toolkit.App.Services.Overlay;

public interface IOverlayFrameStore
{
    IReadOnlyList<OverlayFrame> GetAll();

    void AddOrUpdate(OverlayFrame frame);

    bool Remove(string frameId);

    IReadOnlyList<OverlayFrame> RemoveByOwner(string ownerId);

    void Clear();
}

public sealed class OverlayFrameStore : IOverlayFrameStore
{
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, OverlayFrame> frames = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<OverlayFrame> GetAll()
    {
        lock (syncRoot)
        {
            return frames.Values.ToArray();
        }
    }

    public void AddOrUpdate(OverlayFrame frame)
    {
        lock (syncRoot)
        {
            frames[frame.FrameId] = frame;
        }
    }

    public bool Remove(string frameId)
    {
        lock (syncRoot)
        {
            return frames.Remove(frameId);
        }
    }

    public IReadOnlyList<OverlayFrame> RemoveByOwner(string ownerId)
    {
        lock (syncRoot)
        {
            List<string> frameIds = frames.Values
                .Where(frame => string.Equals(frame.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                .Select(frame => frame.FrameId)
                .ToList();

            List<OverlayFrame> removedFrames = new(frameIds.Count);
            foreach (string frameId in frameIds)
            {
                if (frames.Remove(frameId, out OverlayFrame? frame))
                {
                    removedFrames.Add(frame);
                }
            }

            return removedFrames;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            frames.Clear();
        }
    }
}

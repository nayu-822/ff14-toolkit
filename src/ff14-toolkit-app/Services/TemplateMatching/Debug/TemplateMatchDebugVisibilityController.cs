using System.Drawing;
using System.Runtime.InteropServices;

namespace FF14Toolkit.App.Services.TemplateMatching.Debug;

public sealed class TemplateMatchDebugVisibilityController
{
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, SuppressionState> states = new(StringComparer.OrdinalIgnoreCase);

    public VisibilityDecision Evaluate(string monitorId, Rectangle? matchedBounds)
    {
        lock (syncRoot)
        {
            states.TryGetValue(monitorId, out SuppressionState? state);
            state ??= new SuppressionState();

            bool leftButtonDown = (GetAsyncKeyState(VirtualKeyLeftButton) & KeyDownMask) != 0;
            bool suppressedNow = false;
            bool restoredNow = false;

            if (leftButtonDown && !state.WasLeftButtonDown && matchedBounds is Rectangle clickableBounds)
            {
                Point cursor = GetCursorPosition();
                if (clickableBounds.Contains(cursor))
                {
                    state.IsSuppressed = true;
                    state.SuppressedBounds = clickableBounds;
                    suppressedNow = true;
                }
            }

            state.WasLeftButtonDown = leftButtonDown;

            if (state.IsSuppressed)
            {
                if (matchedBounds is null)
                {
                    state.IsSuppressed = false;
                    state.SuppressedBounds = null;
                    restoredNow = true;
                }
                else if (state.SuppressedBounds is not Rectangle suppressedBounds || suppressedBounds != matchedBounds.Value)
                {
                    state.IsSuppressed = false;
                    state.SuppressedBounds = null;
                    restoredNow = true;
                }
            }

            states[monitorId] = state;
            return new VisibilityDecision(state.IsSuppressed, suppressedNow, restoredNow);
        }
    }

    public void Reset(string monitorId)
    {
        lock (syncRoot)
        {
            states.Remove(monitorId);
        }
    }

    private static Point GetCursorPosition()
    {
        if (!GetCursorPos(out NativePoint nativePoint))
        {
            return Point.Empty;
        }

        return new Point(nativePoint.X, nativePoint.Y);
    }

    public sealed record VisibilityDecision(bool IsSuppressed, bool SuppressedNow, bool RestoredNow);

    private sealed class SuppressionState
    {
        public bool WasLeftButtonDown { get; set; }

        public bool IsSuppressed { get; set; }

        public Rectangle? SuppressedBounds { get; set; }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private const int VirtualKeyLeftButton = 0x01;
    private const short KeyDownMask = unchecked((short)0x8000);
}

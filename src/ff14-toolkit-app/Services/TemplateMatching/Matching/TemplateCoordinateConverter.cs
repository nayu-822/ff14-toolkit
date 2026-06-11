using System.Drawing;

namespace FF14Toolkit.App.Services.TemplateMatching.Matching;

public static class TemplateCoordinateConverter
{
    public static Rectangle ToScreenCoordinates(Rectangle localBounds, Rectangle captureScreenBounds)
    {
        return new Rectangle(
            captureScreenBounds.Left + localBounds.Left,
            captureScreenBounds.Top + localBounds.Top,
            localBounds.Width,
            localBounds.Height);
    }

    public static Rectangle ToLocalCoordinates(Rectangle screenBounds, Rectangle captureScreenBounds)
    {
        return new Rectangle(
            screenBounds.Left - captureScreenBounds.Left,
            screenBounds.Top - captureScreenBounds.Top,
            screenBounds.Width,
            screenBounds.Height);
    }

    public static Rectangle ClampLocalSearchBounds(Rectangle? searchBounds, Rectangle captureScreenBounds, int captureWidth, int captureHeight)
    {
        Rectangle local = searchBounds is null
            ? new Rectangle(0, 0, captureWidth, captureHeight)
            : ToLocalCoordinates(searchBounds.Value, captureScreenBounds);

        int left = Math.Max(0, local.Left);
        int top = Math.Max(0, local.Top);
        int right = Math.Min(captureWidth, local.Right);
        int bottom = Math.Min(captureHeight, local.Bottom);

        if (right <= left || bottom <= top)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}

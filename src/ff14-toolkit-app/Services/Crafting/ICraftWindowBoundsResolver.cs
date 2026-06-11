using System.Drawing;

namespace FF14Toolkit.App.Services.Crafting;

public interface ICraftWindowBoundsResolver
{
    bool TryResolve(
        Rectangle craftingLogTitleBounds,
        double scale,
        Rectangle screenBounds,
        out Rectangle windowBounds,
        out double visibleAreaRatio);

    Rectangle CalculateButtonSearchRegion(Rectangle windowBounds);
}

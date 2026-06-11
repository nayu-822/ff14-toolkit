using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FF14Toolkit.App.Services.TemplateMatching.Capture;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public ScreenCaptureFrame Capture(Rectangle screenBounds)
    {
        Bitmap bitmap = new(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);

        try
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, screenBounds.Size, CopyPixelOperation.SourceCopy);
            }

            Rectangle bitmapBounds = new(0, 0, bitmap.Width, bitmap.Height);
            BitmapData? bitmapData = null;

            try
            {
                bitmapData = bitmap.LockBits(bitmapBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = Math.Abs(bitmapData.Stride);
                byte[] pixels = new byte[stride * bitmapData.Height];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                return new ScreenCaptureFrame(
                    screenBounds,
                    bitmap.Width,
                    bitmap.Height,
                    stride,
                    pixels,
                    DateTimeOffset.Now,
                    bitmap);
            }
            finally
            {
                if (bitmapData is not null)
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class MakeSocialPreview
{
    private const int Width = 1280;
    private const int Height = 640;

    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: MakeSocialPreview <photo.png> <output.jpg>");
        using (var photo = Image.FromFile(args[0]))
        using (var canvas = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(Color.FromArgb(24, 28, 33));

            Rectangle photoArea = new Rectangle(735, 0, 545, Height);
            Rectangle source = CoverCrop(photo.Size, photoArea.Size);
            graphics.DrawImage(photo, photoArea, source, GraphicsUnit.Pixel);

            DrawLogo(graphics, 72, 74, 104);
            using (var title = new Font("Segoe UI", 48F, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var subtitle = new Font("Segoe UI", 25F, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var detail = new Font("Segoe UI", 18F, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var badge = new Font("Segoe UI", 17F, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.FromArgb(245, 247, 250)))
            using (var muted = new SolidBrush(Color.FromArgb(185, 194, 204)))
            using (var accent = new SolidBrush(Color.FromArgb(255, 145, 0)))
            using (var badgeText = new SolidBrush(Color.FromArgb(28, 31, 36)))
            {
                graphics.DrawString("MK212 DriveGlow", title, white, 72, 210);
                graphics.DrawString("Turn the OMOTON MK212 accent bar", subtitle, muted, 76, 294);
                graphics.DrawString("into a classic disk activity light.", subtitle, muted, 76, 334);
                graphics.DrawString("Windows 10/11  •  Open source  •  No drivers", detail, muted, 76, 415);

                using (GraphicsPath pill = RoundedRectangle(new RectangleF(76, 494, 126, 42), 21))
                {
                    graphics.FillPath(accent, pill);
                    graphics.DrawString("v1.4.0", badge, badgeText, 103, 503);
                }
            }

            ImageCodecInfo encoder = Array.Find(ImageCodecInfo.GetImageEncoders(), codec => codec.FormatID == ImageFormat.Jpeg.Guid);
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
                canvas.Save(args[1], encoder, parameters);
            }
        }
    }

    private static Rectangle CoverCrop(Size source, Size target)
    {
        double targetRatio = (double)target.Width / target.Height;
        double sourceRatio = (double)source.Width / source.Height;
        if (sourceRatio > targetRatio)
        {
            int width = (int)Math.Round(source.Height * targetRatio);
            return new Rectangle((source.Width - width) / 2, 0, width, source.Height);
        }
        int height = (int)Math.Round(source.Width / targetRatio);
        int top = Math.Max(0, (source.Height - height) / 2 - 90);
        return new Rectangle(0, top, source.Width, height);
    }

    private static void DrawLogo(Graphics graphics, int x, int y, int size)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(x, y);
        graphics.ScaleTransform(size / 256F, size / 256F);
        using (GraphicsPath background = RoundedRectangle(new RectangleF(4, 4, 248, 248), 50))
        using (GraphicsPath drive = RoundedRectangle(new RectangleF(35, 55, 186, 146), 18))
        using (var backgroundBrush = new SolidBrush(Color.FromArgb(42, 47, 54)))
        using (var driveBrush = new SolidBrush(Color.FromArgb(55, 61, 68)))
        using (var edge = new Pen(Color.FromArgb(220, 225, 230), 13F))
        using (var detail = new Pen(Color.FromArgb(170, 180, 190), 12F))
        {
            edge.LineJoin = LineJoin.Round;
            detail.StartCap = LineCap.Round;
            detail.EndCap = LineCap.Round;
            graphics.FillPath(backgroundBrush, background);
            graphics.FillPath(driveBrush, drive);
            graphics.DrawPath(edge, drive);
            graphics.DrawEllipse(detail, 75, 82, 88, 88);
            graphics.DrawLine(detail, 148, 148, 187, 101);
        }
        graphics.Restore(state);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = radius * 2F;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

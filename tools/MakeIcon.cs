using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class MakeIcon
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);

    public static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2) return 2;
        using (var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var body = new SolidBrush(Color.FromArgb(47, 52, 58)))
            using (var edge = new Pen(Color.FromArgb(220, 225, 230), 2.2f))
            using (var platter = new Pen(Color.FromArgb(170, 180, 190), 1.8f))
            using (var light = new SolidBrush(Color.FromArgb(255, 145, 0)))
            {
                g.FillRectangle(body, 3, 6, 26, 20);
                g.DrawRectangle(edge, 4, 7, 24, 18);
                g.DrawEllipse(platter, 9, 10, 12, 12);
                g.DrawLine(platter, 17, 17, 23, 12);
                g.FillEllipse(light, 23, 20, 4, 4);
            }
            IntPtr handle = bitmap.GetHicon();
            try
            {
                using (Icon icon = Icon.FromHandle(handle))
                using (FileStream file = File.Create(args[0])) icon.Save(file);
                if (args.Length > 1) bitmap.Save(args[1], ImageFormat.Png);
            }
            finally { DestroyIcon(handle); }
        }
        return 0;
    }
}

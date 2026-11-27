using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class ObjArt
{
    static void Crop(string src, string dst, Rectangle r, RotateFlipType rot)
    {
        using (Bitmap s = new Bitmap(src))
        using (Bitmap b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(s, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            b.RotateFlip(rot);
            b.Save(dst, ImageFormat.Png);
        }
    }

    // 감염 얼룩: 가장자리가 부드럽고 살짝 울퉁불퉁한 보라 타원 (바닥·천장에 납작하게 붙임)
    static void Stain(string dst, int w, int h, int seed)
    {
        Random rnd = new Random(seed);
        double[] bump = new double[16];
        for (int i = 0; i < bump.Length; i++) bump[i] = 0.85 + rnd.NextDouble() * 0.3;
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = (x + 0.5) / w * 2 - 1, dy = (y + 0.5) / h * 2 - 1;
                    double ang = Math.Atan2(dy, dx), rr = Math.Sqrt(dx * dx + dy * dy);
                    double f = (ang + Math.PI) / (2 * Math.PI) * bump.Length;
                    int i0 = (int)f % bump.Length, i1 = (i0 + 1) % bump.Length;
                    double edge = bump[i0] + (bump[i1] - bump[i0]) * (f - Math.Floor(f));
                    double k = rr / edge;
                    double a = k >= 1 ? 0 : Math.Pow(1 - k, 0.6);
                    // 가운데는 진한 보라, 가장자리는 밝은 보라
                    int R = (int)(110 + 60 * k), G = (int)(30 + 30 * k), B = (int)(140 + 50 * k);
                    b.SetPixel(x, y, Color.FromArgb((int)(Math.Min(1, a * 1.3) * 215), Math.Min(255, R), Math.Min(255, G), Math.Min(255, B)));
                }
            b.Save(dst, ImageFormat.Png);
        }
    }

    public static string Build(string srcDir, string dstDir)
    {
        Directory.CreateDirectory(dstDir);
        Crop(Path.Combine(srcDir, "적혈구.png"), Path.Combine(dstDir, "적혈구_발판.png"), new Rectangle(23, 68, 1090, 327), RotateFlipType.RotateNoneFlipNone);
        Crop(Path.Combine(srcDir, "적혈구.png"), Path.Combine(dstDir, "적혈구_혈소판.png"), new Rectangle(23, 68, 1090, 327), RotateFlipType.RotateNoneFlipNone);
        Crop(Path.Combine(srcDir, "적혈구.png"), Path.Combine(dstDir, "적혈구_부서지는발판.png"), new Rectangle(23, 68, 1090, 327), RotateFlipType.RotateNoneFlipNone);
        Crop(Path.Combine(srcDir, "부서지는바닥.png"), Path.Combine(dstDir, "부서지는바닥.png"), new Rectangle(18, 23, 1952, 1010), RotateFlipType.RotateNoneFlipNone);
        Crop(Path.Combine(srcDir, "백혈구.png"), Path.Combine(dstDir, "백혈구_세로.png"), new Rectangle(11, 11, 1594, 636), RotateFlipType.Rotate90FlipNone);
        Stain(Path.Combine(dstDir, "감염 얼룩.png"), 512, 160, 7);
        return "ok";
    }
}

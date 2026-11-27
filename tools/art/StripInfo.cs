using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public static class StripInfo
{
    public static string Analyze(string path)
    {
        using (Bitmap bm = new Bitmap(path))
        {
            int W = bm.Width, H = bm.Height;
            BitmapData d = bm.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int S = d.Stride;
            byte[] px = new byte[S * H];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bm.UnlockBits(d);
            Func<int, int, int> A = (x, y) => px[y * S + x * 4 + 3];
            int minx = W, maxx = 0, miny = H, maxy = 0;
            for (int y = 0; y < H; y += 2) for (int x = 0; x < W; x += 2)
                if (A(x, y) > 128) { if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
            List<int> tops = new List<int>(), bottoms = new List<int>();
            for (int x = minx + 400; x < maxx - 400; x += 20)
            {
                for (int y = 0; y < H; y++) if (A(x, y) > 200) { tops.Add(y); break; }
                for (int y = H - 1; y >= 0; y--) if (A(x, y) > 200) { bottoms.Add(y); break; }
            }
            tops.Sort(); bottoms.Sort();
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}x{1} content x {2}~{3} y {4}~{5}\n", W, H, minx, maxx, miny, maxy);
            sb.AppendFormat("top: p10 {0} median {1} p90 {2} | bottom: min {3} median {4} max {5}\n",
                tops[tops.Count / 10], tops[tops.Count / 2], tops[tops.Count * 9 / 10], bottoms[0], bottoms[bottoms.Count / 2], bottoms[bottoms.Count - 1]);
            // 아래쪽 색 (가운데, 바닥 근처)
            int cx = W / 2;
            foreach (int yy in new[] { tops[tops.Count / 2] + 100, (tops[tops.Count / 2] + bottoms[bottoms.Count / 2]) / 2, bottoms[bottoms.Count / 2] - 40 })
            {
                int o = yy * S + cx * 4;
                sb.AppendFormat("y{0}: r{1} g{2} b{3} a{4}\n", yy, px[o + 2], px[o + 1], px[o], px[o + 3]);
            }
            // 왼쪽 끝 모양: 각 x에서 윗면 높이 (처음 600px)
            sb.Append("left edge tops: ");
            for (int x = minx; x < minx + 600; x += 50) { int t = -1; for (int y = 0; y < H; y++) if (A(x, y) > 200) { t = y; break; } sb.Append(x + ":" + t + " "); }
            sb.Append("\nright edge tops: ");
            for (int x = maxx - 600; x <= maxx; x += 50) { int t = -1; for (int y = 0; y < H; y++) if (A(x, y) > 200) { t = y; break; } sb.Append(x + ":" + t + " "); }
            return sb.ToString();
        }
    }
}

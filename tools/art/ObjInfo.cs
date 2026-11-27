using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public static class ObjInfo
{
    // 그림 내용 범위와, 가운데 근처 여러 열에서 처음 불투명해지는 줄(윗면)
    public static string Analyze(string path)
    {
        using (Bitmap bm = new Bitmap(path))
        {
            int W = bm.Width, H = bm.Height;
            BitmapData d = bm.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int S = d.Stride; byte[] px = new byte[S * H];
            Marshal.Copy(d.Scan0, px, 0, px.Length); bm.UnlockBits(d);
            int minx = W, maxx = 0, miny = H, maxy = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                if (px[y * S + x * 4 + 3] > 128) { if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}x{1} content x {2}~{3} y {4}~{5}\n tops at % of width: ", W, H, minx, maxx, miny, maxy);
            foreach (double f in new[] { 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8 })
            {
                int x = (int)(minx + (maxx - minx) * f), t = -1;
                for (int y = 0; y < H; y++) if (px[y * S + x * 4 + 3] > 200) { t = y; break; }
                sb.AppendFormat("{0:0.0}:{1} ", f, t);
            }
            return sb.ToString();
        }
    }
}

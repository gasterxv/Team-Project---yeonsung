using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class GroundTiles
{
    // full = 합친 땅 띠 그림. y0 부터 H 만큼 잘라서 가운데 반복 타일과 양 끝 조각을 만듦
    public static string Build(string fullPath, string dstDir, int y0, int H, int F)
    {
        using (Bitmap full = new Bitmap(fullPath))
        {
            int W = full.Width, Hf = full.Height;
            BitmapData fd = full.LockBits(new Rectangle(0, 0, W, Hf), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int S = fd.Stride;
            byte[] px = new byte[S * Hf];
            Marshal.Copy(fd.Scan0, px, 0, px.Length);
            full.UnlockBits(fd);

            // 조각 사이 투명한 틈(약 4px) 메우기: 땅 몸통 높이에서 투명한 세로줄은 왼쪽 가장 가까운 불투명한 줄을 통째로 복사
            int probeY = 1100, filled = 0;
            for (int x = 0; x < 0; x++)   // 끔: 몸통에는 틈이 없었음 (양 끝 둥근 테두리만 망가뜨림)
            {
                if (px[probeY * S + x * 4 + 3] >= 250) continue;
                int src = x - 1;
                while (src > 0 && px[probeY * S + src * 4 + 3] < 250) src--;
                for (int y = 0; y < Hf; y++) Buffer.BlockCopy(px, y * S + src * 4, px, y * S + x * 4, 4);
                filled++;
            }

            int Wt = 2408;
            // 가운데: 원본 764~3172. 끝 F칸은 원본 604~763(왼끝 조각의 오른쪽)으로 섞어서 다시 처음(764)과 이어지게
            Make(px, S, y0, H, Wt, dstDir + "\\땅_가운데_반복.png", x =>
                x < Wt - F ? new double[] { 764 + x, 0, 0 } : new double[] { 764 + x, 604 + (x - (Wt - F)), ((x - (Wt - F)) + 0.5) / F });
            // 오른끝: 원본 3172~3880. 처음 F칸은 원본 764~(가운데 시작)에서 섞여 들어오게
            Make(px, S, y0, H, 708, dstDir + "\\땅_오른끝.png", x =>
                x < F ? new double[] { 764 + x, 3172 + x, (x + 0.5) / F } : new double[] { 3172 + x, 0, 0 });
            // 왼끝: 원본 100~764 (오른쪽 끝이 가운데 시작과 그대로 이어짐)
            Make(px, S, y0, H, 664, dstDir + "\\땅_왼끝.png", x => new double[] { 100 + x, 0, 0 });
            return "ok " + Wt + "x" + H + ", 메운 틈 세로줄 " + filled + "개";
        }
    }

    static void Make(byte[] px, int S, int y0, int H, int w, string file, Func<int, double[]> map)
    {
        using (Bitmap bm = new Bitmap(w, H, PixelFormat.Format32bppArgb))
        {
            BitmapData d = bm.LockBits(new Rectangle(0, 0, w, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] b = new byte[d.Stride * H];
            double[][] m = new double[w][];
            for (int x = 0; x < w; x++) m[x] = map(x);
            for (int y = 0; y < H; y++)
            {
                int sy = (y0 + y) * S, dy = y * d.Stride;
                for (int x = 0; x < w; x++)
                {
                    int ia = sy + (int)m[x][0] * 4, ib = sy + (int)m[x][1] * 4, o = dy + x * 4;
                    double t = m[x][2];
                    for (int c = 0; c < 4; c++)
                        b[o + c] = t <= 0 ? px[ia + c] : (byte)Math.Round(px[ia + c] * (1 - t) + px[ib + c] * t);
                }
            }
            Marshal.Copy(b, 0, d.Scan0, b.Length);
            bm.UnlockBits(d);
            bm.Save(file, ImageFormat.Png);
        }
    }
}

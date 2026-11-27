using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// 중경_땅1·2로 바닥 조각 만들기.
// - 두 그림의 밟는 면(윗면 중간값)을 같은 줄(Top)에 맞춤
// - 가운데: 땅1(A) 300~12470, 땅2(B) 300~12400 → 약 2칸(segPx)씩 잘라 A00.., B00.. (A→B→A 순서로 이어지게 끝을 섞음)
// - 끝 조각: 땅1 왼쪽 0~300, 오른쪽 12470~끝
// - 아래쪽 40%는 땅 안쪽 어두운 색으로 서서히 바뀌고, 맨 아래 검은 테두리는 잘라냄 → 벽·안쪽과 자연스럽게
public static class FloorTiles
{
    const int Top = 140;          // 출력 그림에서 밟는 면 줄
    const int Below = 900;        // 밟는 면 아래로 남길 높이 (맨 아래 테두리 제외)
    const int F = 220;            // 섞는 폭
    static readonly int[] Fill = { 28, 9, 10 };   // 땅 안쪽 색 (r,g,b)

    class Img { public byte[] px; public int S, W, H, surf; }

    static Img Load(string path, int surf)
    {
        using (Bitmap b = new Bitmap(path))
        {
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Img i = new Img { S = d.Stride, W = b.Width, H = b.Height, surf = surf };
            i.px = new byte[d.Stride * b.Height];
            Marshal.Copy(d.Scan0, i.px, 0, i.px.Length);
            b.UnlockBits(d);
            return i;
        }
    }

    // 출력 줄 r 에 해당하는 원본 화소 (밟는 면 맞춤), 범위 밖은 투명
    static void Get(Img im, int x, int r, double[] c)
    {
        int y = r - Top + im.surf;
        if (x < 0 || x >= im.W || y < 0 || y >= im.H) { c[0] = c[1] = c[2] = c[3] = 0; return; }
        int o = y * im.S + x * 4;
        c[0] = im.px[o]; c[1] = im.px[o + 1]; c[2] = im.px[o + 2]; c[3] = im.px[o + 3];
    }

    public delegate void Src(int x, int r, double[] c);

    static void Save(string file, int w, Src src)
    {
        int H = Top + Below;
        using (Bitmap bm = new Bitmap(w, H, PixelFormat.Format32bppArgb))
        {
            BitmapData d = bm.LockBits(new Rectangle(0, 0, w, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] b = new byte[d.Stride * H];
            double[] c = new double[4];
            for (int r = 0; r < H; r++)
            {
                // 아래쪽 40%부터 땅 안쪽 색으로 (불투명한 곳만)
                double t = r <= Top + Below * 0.6 ? 0 : Math.Min(1, (r - (Top + Below * 0.6)) / (Below * 0.4));
                t = t * t * (3 - 2 * t);
                for (int x = 0; x < w; x++)
                {
                    src(x, r, c);
                    int o = r * d.Stride + x * 4;
                    b[o] = (byte)Math.Round(c[0] * (1 - t) + Fill[2] * t);
                    b[o + 1] = (byte)Math.Round(c[1] * (1 - t) + Fill[1] * t);
                    b[o + 2] = (byte)Math.Round(c[2] * (1 - t) + Fill[0] * t);
                    b[o + 3] = (byte)Math.Round(c[3]);
                }
            }
            Marshal.Copy(b, 0, d.Scan0, b.Length);
            bm.UnlockBits(d);
            bm.Save(file, ImageFormat.Png);
        }
    }

    static void Lerp(double[] a, double[] b, double t, double[] o) { for (int k = 0; k < 4; k++) o[k] = a[k] * (1 - t) + b[k] * t; }

    // 원본 x열에서 처음 불투명해지는 줄 (출력 줄 기준) = 그 열의 윗면 높이
    static int SurfOut(Img im, int x)
    {
        if (x < 0 || x >= im.W) return Top;
        for (int y = 0; y < im.H; y++) if (im.px[y * im.S + x * 4 + 3] > 160) return y - im.surf + Top;
        return Top;
    }

    // 두 열을 섞을 때 윗면 높이를 먼저 맞추고 섞음 (테두리가 겹쳐 흐릿한 줄이 생기지 않게)
    static readonly double[] ba = new double[4], bb = new double[4];
    static void Blend(Img ia, int xa, Img ib, int xb, int r, double t, double[] c)
    {
        int sa = SurfOut(ia, xa), sb = SurfOut(ib, xb);
        double s = sa + (sb - sa) * t;
        Get(ia, xa, (int)Math.Round(r + sa - s), ba);
        Get(ib, xb, (int)Math.Round(r + sb - s), bb);
        Lerp(ba, bb, t, c);
    }

    public static string Build(string p1, int surf1, string p2, int surf2, string dst, int segCount)
    {
        Img A = Load(p1, surf1), B = Load(p2, surf2);
        StringBuilder sb = new StringBuilder();
        double[] t1 = new double[4], t2 = new double[4];

        // a0: 왼쪽 둥근 끝(테두리 곡선)을 지나 몸통이 고르게 시작하는 곳. 섞는 구간(a0-F ~ a0)도 몸통 안에 있어야 함
        int a0 = 520, a1 = 12470, b0 = 300, b1 = 12400;
        // 가운데 A: 끝 F칸은 A[300] 앞부분(A[80..300))으로 섞여 다음 조각(A 시작 또는 B 시작=A 시작 모양)과 이어짐
        Src srcA = (x, r, c) =>
        {
            int ax = a0 + x, wA = a1 - a0;
            Get(A, ax, r, c);
            if (x >= wA - F) { double t = (x - (wA - F) + 0.5) / F; Blend(A, ax, A, a0 - F + (x - (wA - F)), r, t, c); }
        };
        // 가운데 B: 처음 F칸은 A 시작 모양에서 B로, 끝 F칸은 다시 A 시작 앞부분으로
        Src srcB = (x, r, c) =>
        {
            int bx = b0 + x, wB = b1 - b0;
            Get(B, bx, r, c);
            if (x < F) { double t = (x + 0.5) / F; Blend(A, a0 + x, B, bx, r, t, c); }
            else if (x >= wB - F) { double t = (x - (wB - F) + 0.5) / F; Blend(B, bx, A, a0 - F + (x - (wB - F)), r, t, c); }
        };
        sb.Append(Slices(dst, "중경땅_A_", a1 - a0, segCount, srcA));
        sb.Append(Slices(dst, "중경땅_B_", b1 - b0, segCount, srcB));

        // 왼끝: A 0~300 (오른쪽 끝이 A[300] = 가운데 시작과 이어짐)
        Save(Path.Combine(dst, "중경땅_왼끝.png"), a0, (x, r, c) => Get(A, x, r, c));
        // 오른끝: A 12470~끝, 처음 F칸은 A 시작 모양에서 섞여 들어옴
        int wR = A.W - a1;
        Save(Path.Combine(dst, "중경땅_오른끝.png"), wR, (x, r, c) =>
        {
            Get(A, a1 + x, r, c);
            if (x < F) { double t = (x + 0.5) / F; Blend(A, a0 + x, A, a1 + x, r, t, c); }
        });

        // 끝 조각 옆면 위치 (몸통 높이 = 밟는 면 아래 300px에서 불투명한 마지막 칸)
        int bodyY = surf1 + 300;
        int le = 0; while (le < A.W && A.px[bodyY * A.S + le * 4 + 3] < 128) le++;
        int re = A.W - 1; while (re > 0 && A.px[bodyY * A.S + re * 4 + 3] < 128) re--;
        sb.AppendFormat("왼끝 옆면 x={0} (조각 안 {0}), 오른끝 옆면 x={1} (조각 안 {2}), 높이 {3}\n", le, re, re - a1, Top + Below);
        return sb.ToString();
    }

    static string Slices(string dst, string prefix, int width, int n, Src src)
    {
        StringBuilder sb = new StringBuilder(prefix + ": ");
        for (int k = 0; k < n; k++)
        {
            int x0 = width * k / n, x1 = width * (k + 1) / n;
            int off = x0;
            Save(Path.Combine(dst, prefix + k.ToString("00") + ".png"), x1 - x0, (x, r, c) => src(off + x, r, c));
            sb.Append((x1 - x0) + " ");
        }
        return sb.Append("\n").ToString();
    }
}

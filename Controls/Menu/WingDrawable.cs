using Raphael.Driver.Services;

namespace Raphael.Driver.Controls.Menu
{
    /// <summary>
    /// One wing of the Angel menus, drawn on a 160 x 114 canvas with the shoulder at the bottom
    /// left and the tip at the top right. The left wing is the same drawing mirrored.
    /// Ported from the design pages (<c>_meta/design/identity/k-bat-wings.html</c> and
    /// <c>round-3.html</c>), so the geometry here is theirs, number for number.
    /// </summary>
    public sealed class WingDrawable : IDrawable
    {
        public const float W = 160, H = 114;

        public bool Dove { get; init; }
        public bool Mirrored { get; init; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.Scale(dirtyRect.Width / W, dirtyRect.Height / H);
            if (Mirrored)
            {
                canvas.Translate(W, 0);
                canvas.Scale(-1, 1);
            }

            Paint(canvas);
            canvas.RestoreState();
        }

        /// <summary>Draws the wing on its own 160 x 114 canvas, shoulder at the bottom left.</summary>
        public void Paint(ICanvas canvas)
        {
            if (Dove) DrawDove(canvas); else DrawWing(canvas);
        }

        // ---------------- palettes, per theme ----------------

        private record Palette(Color Fill, Color Fill2, Color Stroke, Color Edge);

        private Palette Colors()
        {
            var t = ThemeManager.Effective;
            if (Dove)
                return t switch
                {
                    SkinTheme.Gray => new(C("#F7F9FA"), C("#CDD7DD"), C("#8C9BA4"), C("#E9A23B")),
                    SkinTheme.Dark => new(C("#F2F7F9"), C("#B9CCD7"), C("#7E96A4"), C("#F2C14E")),
                    SkinTheme.Infrared => new(C("#FBE3DF"), C("#E6A9A0"), C("#C0473C"), C("#F5B94A")),
                    _ => new(C("#FFFFFF"), C("#D9E7EF"), C("#A9BFCC"), C("#E9A23B")),
                };
            return t switch
            {
                SkinTheme.Gray => new(C("#F4F7F9"), C("#DCE2E6"), C("#7E8E98"), C("#E9A23B")),
                SkinTheme.Dark => new(C("#E8F0F4"), C("#BFD0DA"), C("#6F8796"), C("#F2C14E")),
                // The dark red wings the driver liked in Infrared.
                SkinTheme.Infrared => new(C("#3A1513"), C("#2C1515"), C("#B3261E"), C("#FF6F61")),
                _ => new(C("#FFFFFF"), C("#EAF3F8"), C("#9FB6C4"), C("#E9A23B")),
            };
        }

        private static Color C(string hex) => Color.FromArgb(hex);

        // ---------------- the Angel wing (round 5): one piece, flight feathers and coverts ----------------

        private void DrawWing(ICanvas c)
        {
            var p = Colors();
            const int n = 7;
            var pts = new PointF[n + 1];
            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                var s = MathF.Sin(MathF.PI * t);
                pts[i] = new PointF(150 - 138 * t + 34 * s, 10 + 96 * t + 22 * s);
            }

            var wing = new PathF();
            wing.MoveTo(4, 110);
            wing.CurveTo(22, 70, 70, 22, 150, 10);
            for (var i = 0; i < n; i++) Scallop(wing, pts[i], pts[i + 1], -11);
            wing.Close();
            Fill(c, wing, p.Fill, p.Stroke, 1.4f);

            c.StrokeColor = p.Stroke.WithAlpha(.45f);
            c.StrokeSize = .9f;
            for (var i = 1; i < n; i++) c.DrawLine(4, 110, pts[i].X, pts[i].Y);

            var cv = pts.Select(q => new PointF(4 + (q.X - 4) * .52f, 110 + (q.Y - 110) * .52f)).ToArray();
            var coverts = new PathF();
            coverts.MoveTo(4, 110);
            coverts.CurveTo(14, 86, 40, 58, cv[0].X, cv[0].Y);
            for (var i = 0; i < n; i++) Scallop(coverts, cv[i], cv[i + 1], -6);
            coverts.Close();
            Fill(c, coverts, p.Fill2, p.Stroke, 1.2f);

            var edge = new PathF();
            edge.MoveTo(4, 110);
            edge.CurveTo(22, 70, 70, 22, 150, 10);
            c.StrokeColor = p.Edge;
            c.StrokeSize = 2.6f;
            c.StrokeLineCap = LineCap.Round;
            c.DrawPath(edge);
        }

        private static void Scallop(PathF path, PointF a, PointF b, float k)
        {
            float mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2, dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy);
            path.QuadTo(mx + dy / l * k, my - dx / l * k, b.X, b.Y);
        }

        // ---------------- the dove wing: three layers of round-ended feathers ----------------

        private static readonly PointF Sh = new(6, 106), Mid = new(70, 62);
        private static readonly PointF[] Lc = [new(6, 106), new(10, 56), new(62, 8), new(152, 8)];

        private void DrawDove(ICanvas c)
        {
            var p = Colors();
            var bend = new PointF(44, 40);

            Fill(c, Layer(1f, 7, bend, 24, 13), p.Fill2, p.Stroke, 1.1f);

            c.StrokeColor = p.Stroke.WithAlpha(.6f);
            c.StrokeSize = .9f;
            var shafts = Curve(new(152, 8), new(22, 110), 7, bend);
            for (var i = 1; i < shafts.Length - 1; i++)
            {
                var q = shafts[i];
                c.DrawLine(Lerp(q.X, Sh.X, .38f), Lerp(q.Y, Sh.Y, .38f), q.X, q.Y);
            }

            Fill(c, Layer(.62f, 8, bend, 13, 9), p.Fill, p.Stroke, 1.1f);
            Fill(c, Layer(.32f, 9, bend, 9, 7), p.Fill, p.Stroke, 1f);

            var arm = new PathF();
            arm.MoveTo(Lc[0]);
            arm.CurveTo(Lc[1], Lc[2], Lc[3]);
            c.StrokeColor = p.Edge;
            c.StrokeSize = 1.8f;
            c.StrokeLineCap = LineCap.Round;
            c.DrawPath(arm);
        }

        /// <summary>The leading edge, then the trailing edge as round feather ends that bulge away
        /// from the middle of the wing. <paramref name="frac"/> below 1 is a row of coverts.</summary>
        private static PathF Layer(float frac, int n, PointF bend, float dTip, float dRoot)
        {
            var outer = Curve(new(152, 8), new(22, 110), n, bend);
            var pts = new PointF[outer.Length];
            for (var i = 0; i < outer.Length; i++)
            {
                var l = Lead(1 - i / (float)n * .94f);
                pts[i] = new PointF(l.X + (outer[i].X - l.X) * frac, l.Y + (outer[i].Y - l.Y) * frac);
            }

            var path = new PathF();
            path.MoveTo(Sh);
            path.CurveTo(Lc[1], Lc[2], Lc[3]);
            for (var i = 0; i < n; i++)
            {
                PointF a = pts[i], b = pts[i + 1];
                float mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2, dx = b.X - a.X, dy = b.Y - a.Y;
                var len = MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 1);
                float ox = -dy / len, oy = dx / len;
                if (ox * (mx - Mid.X) + oy * (my - Mid.Y) < 0) { ox = -ox; oy = -oy; }
                var d = Lerp(dTip, dRoot, i / (float)(n - 1)) * MathF.Max(frac, .5f);
                path.CurveTo(a.X + ox * d, a.Y + oy * d, b.X + ox * d, b.Y + oy * d, b.X, b.Y);
            }
            path.LineTo(Sh);
            path.Close();
            return path;
        }

        private static PointF Lead(float u)
        {
            var v = 1 - u;
            float a = v * v * v, b = 3 * v * v * u, cc = 3 * v * u * u, d = u * u * u;
            return new PointF(a * Lc[0].X + b * Lc[1].X + cc * Lc[2].X + d * Lc[3].X,
                              a * Lc[0].Y + b * Lc[1].Y + cc * Lc[2].Y + d * Lc[3].Y);
        }

        private static PointF[] Curve(PointF a, PointF b, int n, PointF bend)
        {
            var r = new PointF[n + 1];
            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                var s = MathF.Sin(MathF.PI * t);
                r[i] = new PointF(Lerp(a.X, b.X, t) + bend.X * s, Lerp(a.Y, b.Y, t) + bend.Y * s);
            }
            return r;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static void Fill(ICanvas c, PathF path, Color fill, Color stroke, float width)
        {
            c.FillColor = fill;
            c.FillPath(path);
            c.StrokeColor = stroke;
            c.StrokeSize = width;
            c.StrokeLineJoin = LineJoin.Round;
            c.DrawPath(path);
        }
    }

    /// <summary>
    /// Both Angel wings on one layer the size of the menu. The fold and the opening are done here,
    /// on the canvas, with the prototype's numbers (round 5): the shoulder 30 from the centre of the
    /// logo when folded and 8 when open, turned 38° and 4°, at 38 % and full size. Done with the
    /// view's Rotation and Scale, Android turned each wing around its middle instead of its shoulder
    /// and the folded wings landed far from the logo.
    /// </summary>
    public sealed class AngelWingsDrawable(bool dove) : IDrawable
    {
        private readonly WingDrawable _wing = new() { Dove = dove };

        /// <summary>The centre of the logo, in this layer's coordinates.</summary>
        public PointF Center { get; set; }

        /// <summary>0 folded, 1 open.</summary>
        public float Open { get; set; }

        /// <summary>The wings' size when open: 1 on the design's 340-wide phone, more on a wider one.</summary>
        public float OpenSize { get; set; } = 1;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Center.X <= 0) return;
            var p = Open;
            var shoulder = 30 - 22 * p;
            var turn = 38 - 34 * p;
            var size = .40f + (OpenSize - .40f) * p;
            foreach (var side in new[] { 1f, -1f })
            {
                canvas.SaveState();
                canvas.Translate(Center.X + side * shoulder, Center.Y + 6);
                canvas.Rotate(-side * turn);
                canvas.Scale(size * side, size);
                canvas.Translate(0, -WingDrawable.H);
                _wing.Paint(canvas);
                canvas.RestoreState();
            }
        }
    }
}

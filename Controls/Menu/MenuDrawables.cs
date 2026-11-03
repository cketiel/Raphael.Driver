using Raphael.Driver.Services;

namespace Raphael.Driver.Controls.Menu
{
    /// <summary>Pulse (L): a flat line with one heartbeat over the selected item. <see cref="Progress"/>
    /// draws it from left to right, 0 to 1.</summary>
    public sealed class PulseDrawable : IDrawable
    {
        public float SpikeX { get; set; }
        public float Progress { get; set; } = 1;

        public void Draw(ICanvas canvas, RectF r)
        {
            var mid = r.Height / 2;
            var x = SpikeX;
            var pts = new List<PointF>
            {
                new(0, mid), new(x - 17, mid), new(x - 13, mid - 3), new(x - 9, mid), new(x - 6, mid - 11),
                new(x - 2, mid + 11), new(x + 1, mid - 2), new(x + 4, mid), new(r.Width, mid),
            };

            var path = new PathF();
            path.MoveTo(pts[0]);
            var limit = r.Width * Progress;
            for (var i = 1; i < pts.Count; i++)
            {
                if (pts[i].X <= limit) { path.LineTo(pts[i]); continue; }
                var a = pts[i - 1];
                var t = (limit - a.X) / Math.Max(pts[i].X - a.X, .01f);
                path.LineTo(a.X + (pts[i].X - a.X) * t, a.Y + (pts[i].Y - a.Y) * t);
                break;
            }

            canvas.StrokeColor = ThemeManager.GetColor("NextColor", Color.FromArgb("#C9412F"));
            canvas.StrokeSize = 2;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }
    }

    /// <summary>
    /// Radar (M): the dark half disc behind the fan (design round 3, «.mrf»): rings every 36,
    /// spokes every 30°, a bright sweep line at <see cref="Sweep"/> degrees (180 = left, 0 = right)
    /// moving clockwise, with its fading wedge trailing behind it, and the blips it lights up.
    /// </summary>
    public sealed class RadarDrawable : IDrawable
    {
        public float Sweep { get; set; } = 180;

        /// <summary>Where the fan's items are, in the same degrees: they glow as the sweep passes.</summary>
        public float[] Blips { get; set; } = [152, 121, 90, 59, 28];

        public void Draw(ICanvas canvas, RectF r)
        {
            float cx = r.Width / 2, cy = r.Height, rad = r.Width / 2;
            var ir = ThemeManager.Effective == SkinTheme.Infrared;
            var ground = ir ? Color.FromArgb("#1E0807") : Color.FromArgb("#071E2C");
            var line = ir ? Color.FromArgb("#FF6F61") : Color.FromArgb("#50ABC1");
            PointF At(float deg, float len) => new(cx + MathF.Cos(deg * MathF.PI / 180) * len, cy - MathF.Sin(deg * MathF.PI / 180) * len);

            var disc = new PathF();
            disc.MoveTo(cx - rad, cy);
            for (var d = 180; d >= 0; d -= 3) disc.LineTo(At(d, rad));
            disc.Close();
            canvas.FillColor = ground.WithAlpha(.94f);
            canvas.FillPath(disc);

            canvas.StrokeColor = line.WithAlpha(.35f);
            canvas.StrokeSize = 1;
            for (var k = 36f; k < rad; k += 36)
                canvas.DrawArc(cx - k, cy - k, k * 2, k * 2, 0, 180, false, false);
            for (var d = 30; d < 180; d += 30) { var e = At(d, rad); canvas.DrawLine(cx, cy, e.X, e.Y); }

            // The wedge trails behind the line, on the side it has already swept (larger angles).
            // A full turn, as in the design: below the horizon the sweep is out of the disc and hidden.
            for (var i = 0; i < 28; i++)
            {
                float a0 = Sweep + i * 2.5f, a1 = a0 + 2.5f;
                if (a0 > 180) break;
                if (a1 <= 0) continue;
                a0 = Math.Max(a0, 0);
                var w = new PathF();
                w.MoveTo(cx, cy);
                w.LineTo(At(a0, rad));
                w.LineTo(At(Math.Min(a1, 180), rad));
                w.Close();
                canvas.FillColor = line.WithAlpha(.42f * (1 - i / 28f));
                canvas.FillPath(w);
            }
            if (Sweep >= 0 && Sweep <= 180)
            {
                var tip = At(Sweep, rad);
                canvas.StrokeColor = line;
                canvas.StrokeSize = 2;
                canvas.DrawLine(cx, cy, tip.X, tip.Y);
            }

            foreach (var b in Blips)
            {
                var since = b - Sweep; // degrees swept since it was found
                if (since < 0) continue;
                var glow = Math.Max(0, 1 - since / 90f);
                var p = At(b, rad * .72f);
                canvas.FillColor = line.WithAlpha(.25f + .75f * glow);
                canvas.FillCircle(p.X, p.Y, 3 + 3 * glow);
            }
        }
    }

    /// <summary>Steering Wheel (G): a white wheel with three spokes, on the dark centre button.</summary>
    public sealed class SteeringDrawable : IDrawable
    {
        public void Draw(ICanvas c, RectF r)
        {
            float cx = r.Width / 2, cy = r.Height / 2, rad = MathF.Min(cx, cy) - 3;
            c.StrokeColor = Colors.White;
            c.StrokeSize = 3.4f;
            c.StrokeLineCap = LineCap.Round;
            c.DrawCircle(cx, cy, rad);
            c.FillColor = Colors.White;
            c.FillCircle(cx, cy, rad * .28f);
            c.DrawLine(cx - rad, cy - 2, cx - rad * .28f, cy);
            c.DrawLine(cx + rad, cy - 2, cx + rad * .28f, cy);
            c.DrawLine(cx, cy + rad * .28f, cx, cy + rad);
        }
    }

    /// <summary>Tire (O): the tire, the rim with its spokes and an R on the hubcap.</summary>
    public sealed class TireDrawable : IDrawable
    {
        public void Draw(ICanvas c, RectF r)
        {
            float cx = r.Width / 2, cy = r.Height / 2, rad = MathF.Min(cx, cy);
            c.FillColor = Color.FromArgb("#1B2329");
            c.FillCircle(cx, cy, rad);
            c.StrokeColor = Color.FromArgb("#3A4750");
            c.StrokeSize = MathF.Max(rad * .06f, 1);
            for (var i = 0; i < 16; i++)
            {
                var a = i * MathF.PI / 8;
                c.DrawLine(cx + MathF.Cos(a) * rad * .82f, cy + MathF.Sin(a) * rad * .82f, cx + MathF.Cos(a) * rad * .97f, cy + MathF.Sin(a) * rad * .97f);
            }
            c.FillColor = Color.FromArgb("#C9D5DC");
            c.FillCircle(cx, cy, rad * .62f);
            c.StrokeColor = Color.FromArgb("#8EA2AD");
            c.StrokeSize = MathF.Max(rad * .08f, 1);
            for (var i = 0; i < 5; i++)
            {
                var a = i * 2 * MathF.PI / 5 - MathF.PI / 2;
                c.DrawLine(cx + MathF.Cos(a) * rad * .22f, cy + MathF.Sin(a) * rad * .22f, cx + MathF.Cos(a) * rad * .58f, cy + MathF.Sin(a) * rad * .58f);
            }
            c.FillColor = Color.FromArgb("#07577A");
            c.FillCircle(cx, cy, rad * .24f);
            if (rad > 14)
            {
                c.FontColor = Colors.White;
                c.FontSize = rad * .3f;
                c.DrawString("R", cx - rad * .24f, cy - rad * .2f, rad * .48f, rad * .4f, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }

    /// <summary>Pin & Van (H): a map pin drawn like the logo, a drop with a white eye. The icon of
    /// the page sits in the eye, as a label over this drawing. The selected pin turns coral.</summary>
    public sealed class PinDrawable(bool on) : IDrawable
    {
        /// <summary>0 eye open, 1 shut: the blink when the pin is chosen.</summary>
        public float Blink { get; set; }

        public void Draw(ICanvas c, RectF r)
        {
            c.SaveState();
            c.Scale(r.Width / 100, r.Height / 128);
            c.FillColor = Colors.Black.WithAlpha(.2f);
            c.FillEllipse(31, 118, 38, 9);
            var p = new PathF();
            p.MoveTo(50, 121.5f);
            p.CurveTo(48, 121.5f, 46.8f, 120.3f, 45.6f, 118.5f);
            p.CurveTo(34, 100, 2, 82, 2, 49);
            p.CurveTo(2, 22.5f, 23.5f, 1, 50, 1);
            p.CurveTo(76.5f, 1, 98, 22.5f, 98, 49);
            p.CurveTo(98, 82, 66, 100, 54.4f, 118.5f);
            p.CurveTo(53.2f, 120.3f, 52, 121.5f, 50, 121.5f);
            p.Close();
            c.FillColor = on ? ThemeManager.GetColor("NextColor", Color.FromArgb("#C9412F")) : Color.FromArgb(ThemeManager.Effective == SkinTheme.Infrared ? "#B3261E" : "#318FB0");
            c.FillPath(p);
            c.FillColor = Colors.White;
            var eye = 34 * (1 - .9f * Blink);
            c.FillEllipse(16, 48 - eye, 68, eye * 2);
            c.RestoreState();
        }
    }

    /// <summary>
    /// Pin & Van (H): the Raphael van, white with the blue and coral swoosh, the logo's pin on the
    /// door and RAPHAEL on its side. <see cref="FacingLeft"/> turns the van round; the lettering is
    /// drawn after, unturned, so it always reads the right way.
    /// </summary>
    public sealed class VanDrawable : IDrawable
    {
        public bool FacingLeft { get; set; }

        public void Draw(ICanvas c, RectF r)
        {
            c.SaveState();
            c.Scale(r.Width / 64, r.Height / 32);
            c.SaveState();
            if (FacingLeft) { c.Translate(64, 0); c.Scale(-1, 1); }
            var body = new PathF();
            body.MoveTo(3, 9); body.CurveTo(3, 6, 5, 4, 8, 4); body.LineTo(41, 4); body.LineTo(51.5f, 13.2f);
            body.LineTo(57, 13.2f); body.CurveTo(59.2f, 13.2f, 61, 15, 61, 17.2f); body.LineTo(61, 25); body.LineTo(3, 25); body.Close();
            c.FillColor = Colors.White; c.FillPath(body);
            c.StrokeColor = Color.FromArgb("#0E1B22"); c.StrokeSize = .8f; c.DrawPath(body);
            c.FillColor = Color.FromArgb("#9FD3E6");
            c.FillRoundedRectangle(7, 7, 10, 5.4f, 1.2f);
            c.FillRoundedRectangle(19, 7, 10, 5.4f, 1.2f);
            var win = new PathF(); win.MoveTo(42, 5.6f); win.LineTo(50.6f, 13.2f); win.LineTo(42, 13.2f); win.Close(); c.FillPath(win);
            c.StrokeColor = Color.FromArgb("#B9C7D0"); c.StrokeSize = .6f; c.DrawLine(30.4f, 5.2f, 30.4f, 24.8f);
            // the swoosh
            var blue = new PathF(); blue.MoveTo(3.4f, 19.6f); blue.CurveTo(18, 14.8f, 34, 22, 60.6f, 16.2f); blue.LineTo(60.6f, 19.6f); blue.CurveTo(34, 25.4f, 18, 18.6f, 3.4f, 23.2f); blue.Close();
            c.FillColor = Color.FromArgb("#318FB0"); c.FillPath(blue);
            var coral = new PathF(); coral.MoveTo(3.4f, 23.2f); coral.CurveTo(18, 18.6f, 34, 25.4f, 60.6f, 19.6f); coral.LineTo(60.6f, 21); coral.CurveTo(34, 26.8f, 18, 20, 3.4f, 24.6f); coral.Close();
            c.FillColor = Color.FromArgb("#F26B5B"); c.FillPath(coral);
            // the logo's pin on the door, with its halo
            c.FillColor = Color.FromArgb("#318FB0"); c.FillCircle(36, 9, 3.2f);
            var pinTip = new PathF(); pinTip.MoveTo(33.4f, 10.6f); pinTip.LineTo(36, 14.6f); pinTip.LineTo(38.6f, 10.6f); pinTip.Close(); c.FillPath(pinTip);
            c.FillColor = Colors.White; c.FillCircle(36, 9, 1.8f);
            c.StrokeColor = Color.FromArgb("#E9A23B"); c.StrokeSize = .6f; c.DrawEllipse(34.6f, 4.6f, 2.8f, 1);
            c.FillColor = Color.FromArgb("#F2C14E"); c.FillRectangle(57.6f, 15, 3, 2);
            c.FillColor = Color.FromArgb("#E5584A"); c.FillRectangle(3, 14, 1.6f, 3);
            foreach (var x in new[] { 13f, 49f })
            {
                c.FillColor = Color.FromArgb("#0E1B22"); c.FillCircle(x, 25.5f, 5);
                c.FillColor = Color.FromArgb("#C9D5DC"); c.FillCircle(x, 25.5f, 2.3f);
            }
            c.RestoreState();
            c.FontColor = Color.FromArgb("#07577A");
            c.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            c.FontSize = 4.6f;
            c.DrawString("RAPHAEL", FacingLeft ? 34 : 6, 13, 24, 5, HorizontalAlignment.Center, VerticalAlignment.Center);
            c.RestoreState();
        }
    }

    /// <summary>Pin & Van (H): the garage at the end of the road, with a roll-up door that closes
    /// as <see cref="Door"/> goes from 0 (open) to 1 (shut).</summary>
    public sealed class GarageDrawable : IDrawable
    {
        public float Door { get; set; }

        public void Draw(ICanvas c, RectF r)
        {
            var red = ThemeManager.GetColor("DangerColor", Color.FromArgb("#C2362F"));
            float w = r.Width, h = r.Height;
            var house = new PathF();
            house.MoveTo(2, h - 1); house.LineTo(2, h * .38f); house.LineTo(w / 2, 2); house.LineTo(w - 2, h * .38f); house.LineTo(w - 2, h - 1);
            c.StrokeColor = red; c.StrokeSize = 2.4f; c.StrokeLineJoin = LineJoin.Round; c.DrawPath(house);
            float dx = w * .2f, dy = h * .45f, dw = w * .6f, dh = h - 1 - dy;
            c.FillColor = red.WithAlpha(.12f); c.FillRectangle(dx, dy, dw, dh);
            var shut = dh * (.14f + .86f * Door);
            c.FillColor = red; c.FillRectangle(dx, dy, dw, shut);
            c.StrokeColor = Colors.White.WithAlpha(.7f); c.StrokeSize = .8f;
            for (var y = dy + 3; y < dy + shut; y += 3.5f) c.DrawLine(dx + 1, y, dx + dw - 1, y);
        }
    }

    /// <summary>Speedometer (R): a chrome bezel, a dark dial with fine and bold ticks, a red zone,
    /// and a lit arc from the left up to the needle at <see cref="Angle"/> (degrees, 180 = left).</summary>
    public sealed class GaugeDrawable : IDrawable
    {
        public float Angle { get; set; } = 165;

        public void Draw(ICanvas c, RectF r)
        {
            float cx = r.Width / 2, cy = 131;
            var ir = ThemeManager.Effective == SkinTheme.Infrared;
            c.FillColor = Color.FromArgb("#B9C7D0");
            c.FillArc(cx - 131, cy - 131, 262, 262, 0, 180, false);
            c.FillColor = Color.FromArgb(ir ? "#1E0B09" : "#10232E");
            c.FillArc(cx - 123, cy - 123, 246, 246, 0, 180, false);
            c.FillRectangle(cx - 123, cy - 1, 246, 20);
            c.FillColor = Color.FromArgb("#B9C7D0");
            c.StrokeLineCap = LineCap.Butt;
            for (var d = 0; d <= 180; d += 6)
            {
                var a = d * MathF.PI / 180;
                var major = d % 30 == 0;
                var r0 = major ? 89f : 95f;
                c.StrokeColor = major ? Color.FromArgb("#F2F7F9") : Color.FromArgb("#8CDCE8EE");
                c.StrokeSize = major ? 2.2f : 1;
                c.DrawLine(cx + MathF.Cos(a) * r0, cy - MathF.Sin(a) * r0, cx + MathF.Cos(a) * 100, cy - MathF.Sin(a) * 100);
            }
            c.StrokeColor = Color.FromArgb("#E5584A");
            c.StrokeSize = 3;
            c.DrawArc(cx - 98, cy - 98, 196, 196, 0, 30, false, false);
            c.StrokeColor = ir ? Color.FromArgb("#FF8A7A") : Color.FromArgb("#7CC8E0");
            c.StrokeSize = 4;
            c.DrawArc(cx - 108, cy - 108, 216, 216, Angle, 180, false, false);
            c.FontColor = Color.FromArgb("#8CDCE8EE");
            c.FontSize = 7;
            c.DrawString("RAPHAEL", cx - 40, cy - 70, 80, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

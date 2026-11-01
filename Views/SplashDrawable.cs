namespace Raphael.Driver.Views
{
    /// <summary>
    /// Draws design Q, "all the trips" (chosen 2026-10-31), over the street map
    /// (<see cref="SplashMap"/>): the day's trips appear around the city as small pins, fly to the
    /// centre and melt into the official logo, which beats in with its map shadow. Many trips, one
    /// angel.
    /// <para>
    /// Everything is a function of <see cref="Time"/>, so one clock drives the drawing and the
    /// labels of <see cref="SplashPage"/> alike. Positions and sizes are in hundredths of the page
    /// width, the design's own unit, and the timing is the design's own, 2.7 s, from
    /// _meta/design/driver-splash/round-4.html (17 · Q). The logo has no halo: the company dropped
    /// it from the official logo (2026-10-31).
    /// </para>
    /// </summary>
    public sealed class SplashDrawable : IDrawable
    {
        /// <summary>The design's length.</summary>
        public const double Duration = 2.7;

        /// <summary>When the name starts to come in, once the logo is settled.</summary>
        public const double TextStart = 1.55;

        // Each small pin: grows in (18% of its 1.25 s), waits, then flies to just under the
        // logo's centre, halving and fading (the last 45%).
        private const double GatherLength = 1.25, GrownBy = 0.18, LeavesAt = 0.55, LandingY = 6;

        // The logo beats in: up to 108% at 60% of its 0.55 s, then back to its size.
        private const double BeatStart = 1.05, BeatLength = 0.55, BeatPeak = 0.6;
        private const double ShadowStart = 1.15, ShadowLength = 0.5;

        /// <summary>The logo is 40% of the page width; the small pins are 8%.</summary>
        private const float LogoWidth = 40, MiniWidth = 8;

        /// <summary>The logo's centre in its own units (viewBox 0 0 100 128), where it sits on the mark.</summary>
        private const float CentreX = 50, CentreY = 64;

        /// <summary>
        /// The day's trips: where each pin waits, from the logo's centre in hundredths of the page
        /// width, when it appears and its colour, as in the design. Listed in the design's order and
        /// drawn last to first, as its page stacks them; all are under the logo.
        /// </summary>
        private static readonly (float X, float Y, double Delay, Color Color)[] Trips =
        [
            (-34, -62, 0, Color.FromArgb("#318FB0")), (30, -74, .05, Color.FromArgb("#50ABC1")),
            (-40, -20, .1, Color.FromArgb("#216D92")), (38, -10, .03, Color.FromArgb("#318FB0")),
            (-28, 40, .12, Color.FromArgb("#50ABC1")), (34, 50, .08, Color.FromArgb("#216D92")),
            (-38, 98, .15, Color.FromArgb("#318FB0")), (6, 120, .2, Color.FromArgb("#50ABC1")),
            (40, 104, .1, Color.FromArgb("#216D92")), (-4, -50, .18, Color.FromArgb("#318FB0")),
        ];

        private static readonly Color PinMid = Color.FromArgb("#318FB0");
        private static readonly Color Angel = Color.FromArgb("#0A5C80");

        private static readonly LinearGradientPaint PinPaint = new()
        {
            StartPoint = new Point(0.85, 0.05),
            EndPoint = new Point(0.2, 0.95),
            GradientStops =
            [
                new PaintGradientStop(0f, Color.FromArgb("#50ABC1")),
                new PaintGradientStop(0.55f, PinMid),
                new PaintGradientStop(1f, Color.FromArgb("#216D92")),
            ],
        };

        private static readonly RadialGradientPaint ShadowPaint = new()
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5,
            GradientStops =
            [
                new PaintGradientStop(0f, Color.FromRgba(5, 63, 89, 107)),
                new PaintGradientStop(0.6f, Color.FromRgba(5, 63, 89, 36)),
                new PaintGradientStop(1f, Color.FromRgba(5, 63, 89, 0)),
            ],
        };

        private static readonly PathF Pin = PathBuilder.Build(
            "M50 121.5c-2 0-3.2-1.2-4.4-3C34 100 2 82 2 49a48 48 0 0 1 96 0c0 33-32 51-43.6 69.5c-1.2 1.8-2.4 3-4.4 3ZM50 14a34 34 0 1 0 0 68a34 34 0 1 0 0-68Z");
        private static readonly PathF WingLeft = PathBuilder.Build(
            "M44.5 50c-7-1-13.5-5-16.5-12c-2.6-6-2-13 .5-17c5 4 11 8 14 14c2.4 5 3 10 2 15Z");
        private static readonly PathF WingRight = PathBuilder.Build(
            "M55.5 50c7-1 13.5-5 16.5-12c2.6-6 2-13-.5-17c-5 4-11 8-14 14c-2.4 5-3 10-2 15Z");
        private static readonly PathF Body = PathBuilder.Build(
            "M37.5 56c0-9.5 5.6-15 12.5-15s12.5 5.5 12.5 15c0 2.6-1.2 3.6-3.4 3.6H57V86H43V59.6h-2.1c-2.2 0-3.4-1-3.4-3.6Z");
        private static readonly PathF SideLeft = PathBuilder.Build(
            "M21 86V73c0-6 4.4-9.6 10.5-9.6S42 67 42 73v13Z");
        private static readonly PathF SideRight = PathBuilder.Build(
            "M58 86V73c0-6 4.4-9.6 10.5-9.6S79 67 79 73v13Z");
        private static readonly PathF Disk = Circle(50, 48, 34);

        /// <summary>Seconds since the splash started.</summary>
        public double Time { get; set; }

        /// <summary>The centre of the logo's place over the name, in page coordinates.</summary>
        public Point Mark { get; set; }

        /// <summary>True once a frame has been drawn with the mark in place: the clock starts then.</summary>
        public bool Drawn { get; private set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var unit = dirtyRect.Width / 100;
            if (unit <= 0 || Mark == Point.Zero)
                return;

            Drawn = true;
            var t = Time;
            for (var i = Trips.Length - 1; i >= 0; i--)
                DrawTrip(canvas, Trips[i], t, unit);
            DrawLogo(canvas, t, unit);
        }

        private void DrawTrip(ICanvas canvas, (float X, float Y, double Delay, Color Color) trip, double t, float unit)
        {
            var p = (t - trip.Delay) / GatherLength;
            if (p <= 0 || p >= 1)
                return;

            double x = trip.X, y = trip.Y, scale = 1, alpha = 1;
            if (p < GrownBy)
            {
                scale = alpha = Ease.Gather(p / GrownBy);
            }
            else if (p >= LeavesAt)
            {
                var q = Ease.Gather((p - LeavesAt) / (1 - LeavesAt));
                x = Lerp(trip.X, 0, q);
                y = Lerp(trip.Y, LandingY, q);
                scale = Lerp(1, 0.5, q);
                alpha = 1 - q;
            }

            if (alpha <= 0 || scale <= 0)
                return;

            // A small pin is the logo's pin and its white disc, on a 100 x 124 canvas centred at (50, 62).
            canvas.SaveState();
            canvas.Alpha = (float)alpha;
            canvas.Translate((float)(Mark.X + x * unit), (float)(Mark.Y + y * unit));
            var k = (float)(MiniWidth * unit / 100 * scale);
            canvas.Scale(k, k);
            canvas.Translate(-50, -62);
            canvas.FillColor = trip.Color;
            canvas.FillPath(Pin, WindingMode.EvenOdd);
            canvas.FillColor = Colors.White;
            canvas.FillCircle(50, 48, 34);
            canvas.RestoreState();
        }

        private void DrawLogo(ICanvas canvas, double t, float unit)
        {
            // The beat: grows from 40% to 108% while it appears, then settles to its size. It
            // scales about the bottom of its 100 x 128 box, so it grows up from the map.
            double scale, alpha;
            var beat = (t - BeatStart) / BeatLength;
            if (beat <= 0)
                return;
            if (beat < BeatPeak)
            {
                var q = Ease.Pop(beat / BeatPeak);
                scale = Lerp(0.4, 1.08, q);
                alpha = Math.Clamp(q, 0, 1);
            }
            else
            {
                scale = Lerp(1.08, 1, Ease.Pop(Math.Min(1, (beat - BeatPeak) / (1 - BeatPeak))));
                alpha = 1;
            }

            var k = LogoWidth * unit / 100;
            canvas.SaveState();
            canvas.Alpha = (float)alpha;
            canvas.Translate((float)(Mark.X - CentreX * k), (float)(Mark.Y - CentreY * k));
            canvas.Scale(k, k);
            ScaleAbout(canvas, scale, 50, 128);

            DrawShadow(canvas, t, alpha);
            DrawMark(canvas);

            canvas.RestoreState();
        }

        /// <summary>The map shadow under the tip spreads out once the logo has landed.</summary>
        private static void DrawShadow(ICanvas canvas, double t, double alpha)
        {
            var q = Ease.Swift(Progress(t, ShadowStart, ShadowStart + ShadowLength));
            if (q <= 0)
                return;

            canvas.SaveState();
            canvas.Alpha = (float)(alpha * q);
            ScaleAbout(canvas, Lerp(0.3, 1, q), 50, 122.6f);
            var box = new RectF(31, 118.4f, 38, 8.4f);
            canvas.SetFillPaint(ShadowPaint, box);
            canvas.FillEllipse(box);
            canvas.RestoreState();
        }

        /// <summary>The official logo: the pin, its disc, the angel and the two travellers.</summary>
        private static void DrawMark(ICanvas canvas)
        {
            canvas.SaveState();

            // A gradient paint outlives a later FillColor on Android: without its own state the
            // disc and the angel came out in the pin's gradient, invisible on the pin.
            // The solid mid tone underneath covers what iOS may leave past the gradient's ends.
            canvas.FillColor = PinMid;
            canvas.FillPath(Pin, WindingMode.EvenOdd);
            canvas.SaveState();
            canvas.SetFillPaint(PinPaint, new RectF(2, 1, 96, 120.5f));
            canvas.FillPath(Pin, WindingMode.EvenOdd);
            canvas.RestoreState();

            canvas.FillColor = Colors.White;
            canvas.FillCircle(50, 48, 34);

            canvas.ClipPath(Disk);
            canvas.FillColor = Angel;
            canvas.FillPath(WingLeft);
            canvas.FillPath(WingRight);
            canvas.FillCircle(50, 31.5f, 7.6f);
            canvas.FillPath(Body);
            DrawTraveller(canvas, 31.5f, SideLeft);
            DrawTraveller(canvas, 68.5f, SideRight);

            canvas.RestoreState();
        }

        /// <summary>A white outline under the fill, so each traveller stands apart from the angel.</summary>
        private static void DrawTraveller(ICanvas canvas, float headX, PathF body)
        {
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2.4f;
            canvas.DrawCircle(headX, 58, 5.6f);
            canvas.DrawPath(body);
            canvas.FillCircle(headX, 58, 5.6f);
            canvas.FillPath(body);
        }

        private static void ScaleAbout(ICanvas canvas, double k, float x, float y)
        {
            canvas.Translate(x, y);
            canvas.Scale((float)k, (float)k);
            canvas.Translate(-x, -y);
        }

        private static PathF Circle(float x, float y, float r)
        {
            var path = new PathF();
            path.AppendCircle(x, y, r);
            return path;
        }

        /// <summary>How far <paramref name="t"/> is through the window [start, end], from 0 to 1.</summary>
        public static double Progress(double t, double start, double end) =>
            Math.Clamp((t - start) / (end - start), 0, 1);

        public static double Lerp(double from, double to, double p) => from + (to - from) * p;

        /// <summary>The CSS easing curves of the design, so the motion matches what was approved.</summary>
        public static class Ease
        {
            public static double Swift(double x) => Bezier(0.22, 1, 0.36, 1, x);

            /// <summary>The small pins' flight: a slow start and a soft landing.</summary>
            public static double Gather(double x) => Bezier(0.5, 0, 0.3, 1, x);

            /// <summary>The logo's beat: a strong overshoot.</summary>
            public static double Pop(double x) => Bezier(0.34, 1.6, 0.64, 1, x);

            /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at <paramref name="x"/>. x(t) only grows,
            /// so bisection finds t without Newton's failure cases.</summary>
            private static double Bezier(double x1, double y1, double x2, double y2, double x)
            {
                if (x <= 0) return 0;
                if (x >= 1) return 1;

                double lo = 0, hi = 1, t = x;
                for (var i = 0; i < 24; i++)
                {
                    t = (lo + hi) / 2;
                    if (Curve(x1, x2, t) < x) lo = t; else hi = t;
                }

                return Curve(y1, y2, t);
            }

            private static double Curve(double p1, double p2, double t) =>
                3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t * t * p2 + t * t * t;
        }
    }
}

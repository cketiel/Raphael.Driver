using Raphael.Driver.Services;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// The minimal outline of a van seen from one side, for the guided vehicle photos (RE-065):
    /// FRONT, BACK, RIGHT, LEFT or INTERIOR. The part to frame is drawn in the accent colour, the
    /// rest in a thin line, so the driver sees at a glance which photo comes next.
    /// </summary>
    public sealed class VehicleSilhouetteDrawable : IDrawable
    {
        public string View { get; set; } = "FRONT";

        public void Draw(ICanvas c, RectF r)
        {
            var ink = ThemeManager.GetColor("InkColor", Colors.Black);
            var accent = ThemeManager.GetColor("AccentColor", Color.FromArgb("#C2362F"));

            // Everything is drawn on a 100 x 60 board, centred and scaled to fit.
            var scale = Math.Min(r.Width / 100f, r.Height / 60f);
            c.SaveState();
            c.Translate(r.Center.X - 50 * scale, r.Center.Y - 30 * scale);
            c.Scale(scale, scale);
            c.StrokeLineJoin = LineJoin.Round;
            c.StrokeLineCap = LineCap.Round;

            switch (View)
            {
                case "FRONT": Front(c, accent, ink, rear: false); break;
                case "BACK": Front(c, accent, ink, rear: true); break;
                case "RIGHT": Side(c, accent, ink, facingRight: true); break;
                case "LEFT": Side(c, accent, ink, facingRight: false); break;
                default: Interior(c, accent, ink); break;
            }
            c.RestoreState();
        }

        /// <summary>The van head-on (or from behind): body, windscreen, lights, wheels.</summary>
        private static void Front(ICanvas c, Color accent, Color ink, bool rear)
        {
            c.StrokeColor = accent;
            c.StrokeSize = 2.4f;
            var body = new PathF();
            body.MoveTo(24, 50); body.LineTo(24, 18); body.CurveTo(24, 10, 30, 6, 38, 6);
            body.LineTo(62, 6); body.CurveTo(70, 6, 76, 10, 76, 18); body.LineTo(76, 50); body.Close();
            c.DrawPath(body);

            c.StrokeSize = 1.6f;
            if (rear)
            {
                c.DrawLine(50, 9, 50, 48);                          // the two rear doors
                c.DrawRoundedRectangle(29, 11, 18, 14, 2);
                c.DrawRoundedRectangle(53, 11, 18, 14, 2);
            }
            else
            {
                c.DrawRoundedRectangle(29, 10, 42, 16, 3);           // windscreen
                c.DrawRoundedRectangle(38, 33, 24, 7, 2);            // grille
            }

            c.FillColor = accent;
            c.FillRoundedRectangle(27, 31, 8, 5, 1.5f);              // lights
            c.FillRoundedRectangle(65, 31, 8, 5, 1.5f);

            c.StrokeColor = ink;
            c.StrokeSize = 1.2f;
            c.DrawLine(14, 56, 86, 56);                              // the ground
            c.FillColor = ink;
            c.FillRoundedRectangle(27, 50, 9, 6, 2);                 // wheels
            c.FillRoundedRectangle(64, 50, 9, 6, 2);
        }

        /// <summary>The van side-on, nose towards <paramref name="facingRight"/>.</summary>
        private static void Side(ICanvas c, Color accent, Color ink, bool facingRight)
        {
            c.SaveState();
            if (!facingRight) { c.Translate(100, 0); c.Scale(-1, 1); }

            c.StrokeColor = accent;
            c.StrokeSize = 2.4f;
            var body = new PathF();
            body.MoveTo(8, 46); body.LineTo(8, 14); body.CurveTo(8, 10, 11, 8, 15, 8);
            body.LineTo(66, 8); body.LineTo(80, 24); body.LineTo(90, 26);
            body.CurveTo(93, 27, 94, 29, 94, 32); body.LineTo(94, 46); body.Close();
            c.DrawPath(body);

            c.StrokeSize = 1.6f;
            c.DrawRoundedRectangle(14, 13, 16, 10, 2);               // windows
            c.DrawRoundedRectangle(34, 13, 16, 10, 2);
            var front = new PathF();
            front.MoveTo(55, 13); front.LineTo(66, 13); front.LineTo(76, 24); front.LineTo(55, 24); front.Close();
            c.DrawPath(front);
            c.DrawLine(53, 10, 53, 44);                              // the sliding door

            c.StrokeColor = ink;
            c.StrokeSize = 1.2f;
            c.DrawLine(2, 56, 98, 56);
            c.FillColor = ink;
            c.FillCircle(24, 48, 8);
            c.FillCircle(78, 48, 8);
            c.FillColor = ThemeManager.GetColor("PageBackgroundColor", Colors.White);
            c.FillCircle(24, 48, 3.5f);
            c.FillCircle(78, 48, 3.5f);
            c.RestoreState();
        }

        /// <summary>Inside, from the back doors: the seats and the wheelchair space.</summary>
        private static void Interior(ICanvas c, Color accent, Color ink)
        {
            c.StrokeColor = ink;
            c.StrokeSize = 1.2f;
            var shell = new PathF();
            shell.MoveTo(10, 56); shell.LineTo(22, 8); shell.LineTo(78, 8); shell.LineTo(90, 56);
            c.DrawPath(shell);
            c.DrawLine(22, 8, 30, 20); c.DrawLine(78, 8, 70, 20);
            c.DrawLine(30, 20, 70, 20);

            c.StrokeColor = accent;
            c.StrokeSize = 2.2f;
            // Two seats on each side.
            foreach (var x in new[] { 20f, 32f })
            {
                c.DrawRoundedRectangle(x, 30, 10, 12, 2);
                c.DrawRoundedRectangle(x, 24, 10, 6, 2);
            }
            foreach (var x in new[] { 58f, 70f })
            {
                c.DrawRoundedRectangle(x, 30, 10, 12, 2);
                c.DrawRoundedRectangle(x, 24, 10, 6, 2);
            }
            // The wheelchair space in the middle.
            c.StrokeSize = 1.8f;
            c.DrawCircle(50, 44, 7);
            c.DrawLine(46, 30, 46, 40); c.DrawLine(46, 34, 54, 34);
            c.DrawCircle(46, 27, 2);
        }
    }
}

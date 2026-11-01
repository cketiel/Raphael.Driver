namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>
    /// Diagonal stripes of one colour over another. The Return variant of several styles: the
    /// stripes tell Appt from Return even where the colour cannot (a grey screen, colour blindness).
    /// </summary>
    public sealed class StripesDrawable(Color stripe, Color ground, float width = 5, float gap = 4) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            canvas.FillColor = ground;
            canvas.FillRectangle(rect);
            canvas.StrokeColor = stripe;
            canvas.StrokeSize = width;
            var step = width + gap;
            for (var x = -rect.Height; x < rect.Width + rect.Height; x += step)
                canvas.DrawLine(x, rect.Height, x + rect.Height, 0);
        }
    }

    /// <summary>
    /// Style 5's road sign: a diamond for Appt, a circle for Return, a square for the garage, in
    /// the convention colour with a white rim. Drawn, not built from a rotated view: on Android a
    /// Border turned 45° lost its rim and covered the arrow laid over it.
    /// </summary>
    public sealed class SignDrawable(Color fill, EventKind kind) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var c = rect.Center;
            var r = Math.Min(rect.Width, rect.Height) / 2 - 2;
            var path = new PathF();
            switch (kind)
            {
                case EventKind.PullOut or EventKind.PullIn:
                    path.AppendRoundedRectangle(c.X - r * 0.8f, c.Y - r * 0.8f, r * 1.6f, r * 1.6f, 3);
                    break;
                case EventKind.PickupReturn or EventKind.DropoffReturn:
                    path.AppendCircle(c.X, c.Y, r * 0.92f);
                    break;
                default:
                    path.MoveTo(c.X, c.Y - r);
                    path.LineTo(c.X + r, c.Y);
                    path.LineTo(c.X, c.Y + r);
                    path.LineTo(c.X - r, c.Y);
                    path.Close();
                    break;
            }

            canvas.FillColor = fill;
            canvas.FillPath(path);
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(path);
        }
    }

    /// <summary>Style 2's folded corner: a triangle filling the top right, solid or striped.</summary>
    public sealed class CornerDrawable(Color color, bool striped) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var path = new PathF();
            path.MoveTo(0, 0);
            path.LineTo(rect.Width, 0);
            path.LineTo(rect.Width, rect.Height);
            path.Close();

            canvas.SaveState();
            canvas.ClipPath(path);
            if (striped)
                new StripesDrawable(color, EventLook.Mix(color, Colors.White, 0.45f)).Draw(canvas, rect);
            else
            {
                canvas.FillColor = color;
                canvas.FillRectangle(rect);
            }
            canvas.RestoreState();
        }
    }

    /// <summary>Style 2's stamp: a round postmark with its words around the ring and the minutes inside.</summary>
    public sealed class PostmarkDrawable(int minutes, Color ink) : IDrawable
    {
        private const string Ring = "EARLY · PLANNED WAIT · EARLY · ";

        public void Draw(ICanvas canvas, RectF rect)
        {
            var c = new PointF(rect.Center.X, rect.Center.Y);
            var r = Math.Min(rect.Width, rect.Height) / 2 - 2;
            canvas.StrokeColor = ink;
            canvas.FontColor = ink;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(c, r);
            canvas.StrokeSize = 1.2f;
            canvas.DrawCircle(c, r * 0.56f);

            canvas.FontSize = r * 0.24f;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            var step = 360f / Ring.Length;
            for (var i = 0; i < Ring.Length; i++)
            {
                canvas.SaveState();
                canvas.Translate(c.X, c.Y);
                canvas.Rotate(i * step - 90);
                canvas.Translate(r * 0.78f, 0);
                canvas.Rotate(90);
                canvas.DrawString(Ring[i].ToString(), -4, -6, 8, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
                canvas.RestoreState();
            }

            canvas.FontSize = r * 0.42f;
            canvas.DrawString(minutes.ToString(), c.X - r, c.Y - r * 0.42f, 2 * r, r * 0.6f, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.FontSize = r * 0.2f;
            canvas.DrawString("MIN", c.X - r, c.Y + r * 0.12f, 2 * r, r * 0.3f, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    /// <summary>Style 6's stamp: a clock face with the wait as a coloured slice (60 minutes to the turn).</summary>
    public sealed class ClockFaceDrawable(int minutes, Color ink) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var c = new PointF(rect.Center.X, rect.Center.Y);
            var r = Math.Min(rect.Width, rect.Height) / 2 - 1.5f;
            var sweep = Math.Min(minutes, 60) * 6f;

            var slice = new PathF();
            slice.MoveTo(c);
            slice.AddArc(c.X - r, c.Y - r, c.X + r, c.Y + r, 90, 90 - sweep, true);
            slice.Close();
            canvas.FillColor = ink.WithAlpha(0.7f);
            canvas.FillPath(slice);

            canvas.StrokeColor = ink;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(c, r);
            canvas.DrawLine(c.X, c.Y, c.X, c.Y - r + 4);
        }
    }

    /// <summary>Style 8's stamp: a timer ring filled to the wait (60 minutes to the full ring).</summary>
    public sealed class RingDrawable(int minutes, Color ink, Color track) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var r = Math.Min(rect.Width, rect.Height) / 2 - 4;
            var c = new PointF(rect.Center.X, rect.Center.Y);
            canvas.StrokeSize = 5;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeColor = track;
            canvas.DrawCircle(c, r);
            canvas.StrokeColor = ink;
            var sweep = Math.Max(6, Math.Min(minutes, 60) * 6f);
            canvas.DrawArc(c.X - r, c.Y - r, 2 * r, 2 * r, 90, 90 - sweep, true, false);
        }
    }
}

namespace Raphael.Driver.Views
{
    /// <summary>
    /// The splash's background: a street map, white streets on a pale blue-grey ground, as in
    /// design Q (_meta/design/driver-splash/round-4.html, 17). Streets run down every 22% of the
    /// width and across every 18%, 1.2% wide, from the top left corner. It is drawn apart from the
    /// pins and the logo (<see cref="SplashDrawable"/>) so that it is painted once and then reused:
    /// repainting the whole screen every frame is what a slow GPU (or the emulator's) cannot keep up with.
    /// </summary>
    public sealed class SplashMap : IDrawable
    {
        /// <summary>The ground colour, also the system's launch frame (MauiSplashScreen in the csproj)
        /// and the glow that keeps the letters readable where they cross a street.</summary>
        public static readonly Color Ground = Color.FromArgb("#E3EBEF");

        public void Draw(ICanvas canvas, RectF rect)
        {
            canvas.FillColor = Ground;
            canvas.FillRectangle(rect);

            var w = rect.Width;
            var street = 0.012f * w;
            canvas.FillColor = Colors.White;
            for (var x = 0f; x < w; x += 0.22f * w)
                canvas.FillRectangle(x, 0, street, rect.Height);
            for (var y = 0f; y < rect.Height; y += 0.18f * w)
                canvas.FillRectangle(0, y, w, street);
        }
    }
}

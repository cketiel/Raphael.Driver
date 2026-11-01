using Android.Content;
using Android.Graphics;
using APaint = Android.Graphics.Paint;
using AColor = Android.Graphics.Color;
using Android.Graphics.Drawables;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using AImageButton = Android.Widget.ImageButton;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using AView = Android.Views.View;

namespace Raphael.Driver
{
    /// <summary>
    /// The Shell as MAUI draws it, plus the V7 rounded square behind the top bar's left button
    /// (COMPONENTES.md, «Botón menú/atrás 40×40, radio 12, fondo blanco 16 %»).
    /// </summary>
    /// <remarks>
    /// The button keeps Android's own icons — the menu lines and the back arrow — at their own
    /// size; only its background changes. A custom FlyoutIcon was tried first: Android shrank it
    /// to 24 dp and, worse, showed it on every page in place of the back arrow.
    /// </remarks>
    public class RaphaelShellRenderer : ShellRenderer
    {
        protected override IShellToolbarTracker CreateTrackerForToolbar(AToolbar toolbar)
        {
            // The navigation button is created the first time the toolbar gets an icon, so it is
            // caught as it is added; one already there is styled at once.
            toolbar.ChildViewAdded += (_, e) => StyleButton(e.Child);

            for (var i = 0; i < toolbar.ChildCount; i++)
                StyleButton(toolbar.GetChildAt(i));

            return base.CreateTrackerForToolbar(toolbar);
        }

        private static void StyleButton(AView? view)
        {
            if (view is not AImageButton button || button.Background is RoundedTileLayer)
                return;

            button.Background = new RoundedTileLayer(button.Context!, button.Background);
        }

        /// <summary>The rounded square under whatever background (the touch ripple) the button had.</summary>
        private sealed class RoundedTileLayer : LayerDrawable
        {
            public RoundedTileLayer(Context context, Drawable? original)
                : base(original is null
                    ? new Drawable[] { new CenteredTileDrawable(context) }
                    : new Drawable[] { new CenteredTileDrawable(context), original })
            {
            }
        }

        /// <summary>A 40 dp square with 12 dp corners in white at 16 %, centred in its bounds.</summary>
        private sealed class CenteredTileDrawable : Drawable
        {
            private const int BaseAlpha = 41; // 16 % of 255

            private readonly APaint _paint = new(PaintFlags.AntiAlias) { Color = AColor.White };
            private readonly float _size;
            private readonly float _radius;

            public CenteredTileDrawable(Context context)
            {
                var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
                _size = 40 * density;
                _radius = 12 * density;
                _paint.Alpha = BaseAlpha;
            }

            public override void Draw(Canvas canvas)
            {
                var bounds = Bounds;
                var size = Math.Min(_size, Math.Min(bounds.Width(), bounds.Height()));
                var left = bounds.CenterX() - size / 2f;
                var top = bounds.CenterY() - size / 2f;
                canvas.DrawRoundRect(left, top, left + size, top + size, _radius, _radius, _paint);
            }

            public override void SetAlpha(int alpha) => _paint.Alpha = BaseAlpha * alpha / 255;

            public override void SetColorFilter(ColorFilter? colorFilter) => _paint.SetColorFilter(colorFilter);

            public override int Opacity => (int)Format.Translucent;
        }
    }
}

namespace InventoryMobile.Controls;

/// <summary>
/// Courbe du solde quotidien (Sparkline du web) : une ligne et un léger aplat dessous, sans
/// axes. Le résumé chiffré (entrées, sorties, solde) est affiché à côté par la page et repris
/// dans la description pour les lecteurs d'écran.
/// </summary>
public sealed class Sparkline : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values), typeof(IReadOnlyList<int>), typeof(Sparkline), Array.Empty<int>(), propertyChanged: Redraw);

    public static readonly BindableProperty LineColorProperty = BindableProperty.Create(
        nameof(LineColor), typeof(Color), typeof(Sparkline), Colors.SlateGray, propertyChanged: Redraw);

    public Sparkline()
    {
        Drawable = new SparklineDrawable(this);
        InputTransparent = true;
    }

    public IReadOnlyList<int> Values
    {
        get => (IReadOnlyList<int>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    private static void Redraw(BindableObject bindable, object oldValue, object newValue) => ((Sparkline)bindable).Invalidate();

    private sealed class SparklineDrawable(Sparkline view) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var values = view.Values;
            if (values is null || values.Count < 2)
            {
                return;
            }

            const float inset = 2;
            var min = values.Min();
            var max = values.Max();
            var span = Math.Max(1, max - min);
            var step = (rect.Width - 2 * inset) / (values.Count - 1);
            PointF At(int i) => new(
                rect.X + inset + i * step,
                rect.Y + inset + (rect.Height - 2 * inset) * (1 - (values[i] - min) / (float)span));

            var line = new PathF();
            line.MoveTo(At(0));
            for (var i = 1; i < values.Count; i++)
            {
                line.LineTo(At(i));
            }

            var area = new PathF(line);
            area.LineTo(At(values.Count - 1).X, rect.Bottom);
            area.LineTo(At(0).X, rect.Bottom);
            area.Close();
            canvas.FillColor = view.LineColor.WithAlpha(0.12f);
            canvas.FillPath(area);

            canvas.StrokeColor = view.LineColor;
            canvas.StrokeSize = 2;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(line);
        }
    }
}

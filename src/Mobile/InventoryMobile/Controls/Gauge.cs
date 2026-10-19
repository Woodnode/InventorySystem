namespace InventoryMobile.Controls;

/// <summary>
/// Barre de remplissage arrondie (BarreStock et barres de répartition du web) : la part d'un
/// total, lue d'un coup d'œil. La valeur chiffrée reste toujours affichée à côté, la barre
/// n'est jamais le seul vecteur de l'information.
/// </summary>
public sealed class Gauge : GraphicsView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(Gauge), 0d, propertyChanged: Redraw);

    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(
        nameof(FillColor), typeof(Color), typeof(Gauge), Colors.SteelBlue, propertyChanged: Redraw);

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(Gauge), Colors.LightGray, propertyChanged: Redraw);

    /// <summary>Part minimale dessinée, pour qu'une valeur faible reste visible (3 % sur le web).</summary>
    public static readonly BindableProperty MinimumFillProperty = BindableProperty.Create(
        nameof(MinimumFill), typeof(double), typeof(Gauge), 0.03, propertyChanged: Redraw);

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(Gauge), -1d, propertyChanged: Redraw);

    public Gauge()
    {
        Drawable = new GaugeDrawable(this);
        InputTransparent = true;
        HeightRequest = 8;
    }

    /// <summary>Part remplie, entre 0 et 1.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public double MinimumFill
    {
        get => (double)GetValue(MinimumFillProperty);
        set => SetValue(MinimumFillProperty, value);
    }

    /// <summary>Rayon des extrémités ; négatif : demi-hauteur (pilule).</summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static void Redraw(BindableObject bindable, object oldValue, object newValue) => ((Gauge)bindable).Invalidate();

    private sealed class GaugeDrawable(Gauge gauge) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var radius = gauge.CornerRadius < 0 ? dirtyRect.Height / 2 : (float)gauge.CornerRadius;
            canvas.FillColor = gauge.TrackColor;
            canvas.FillRoundedRectangle(dirtyRect, radius);

            var part = Math.Clamp(gauge.Value, 0, 1);
            part = Math.Max(part, gauge.MinimumFill);
            var fill = new RectF(dirtyRect.X, dirtyRect.Y, (float)(dirtyRect.Width * part), dirtyRect.Height);
            canvas.FillColor = gauge.FillColor;
            canvas.FillRoundedRectangle(fill, Math.Min(radius, fill.Width / 2));
        }
    }
}

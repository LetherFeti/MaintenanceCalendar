using System.Windows.Media.Animation;

namespace MaintenanceCalendar;

/// <summary>実データの比率を、表示サイズに追従する円弧として描画します。</summary>
internal sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(DonutChart), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public IReadOnlyList<(double Value, Brush Brush)> Segments { get; init; } = [];

    /// <summary>Windowsのアニメーション設定に合わせ、円弧を一度だけ描き進めます。</summary>
    public DonutChart()
    {
        Loaded += (_, _) =>
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            BeginAnimation(ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(850))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
    }

    /// <summary>ゼロ件では中立色の輪を表示し、各値を合計に対する比率で描画します。</summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double diameter = Math.Min(ActualWidth, ActualHeight);
        if (diameter <= 0) return;
        double thickness = diameter * .105, radius = (diameter - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen((Brush)FindResource("LineBrush"), thickness), center, radius, radius);
        double sum = Segments.Sum(x => Math.Max(0, x.Value));
        if (sum <= 0) return;
        double start = -90;
        foreach (var segment in Segments)
        {
            double sweep = Math.Max(0, segment.Value) / sum * 360;
            double shown = Math.Clamp(Progress * 360 - (start + 90), 0, sweep);
            if (shown > 0)
            {
                var pen = new Pen(segment.Brush, thickness);
                if (shown >= 359.99) dc.DrawEllipse(null, pen, center, radius, radius);
                else
                {
                    double gap = sweep > 5 ? 1.5 : 0, angle = Math.Max(0, shown - gap);
                    Point At(double degrees) => new(center.X + radius * Math.Cos(degrees * Math.PI / 180), center.Y + radius * Math.Sin(degrees * Math.PI / 180));
                    var geometry = new StreamGeometry();
                    using (var path = geometry.Open())
                    {
                        path.BeginFigure(At(start + gap / 2), false, false);
                        path.ArcTo(At(start + gap / 2 + angle), new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, true, false);
                    }
                    dc.DrawGeometry(null, pen, geometry);
                }
            }
            start += sweep;
        }
    }
}

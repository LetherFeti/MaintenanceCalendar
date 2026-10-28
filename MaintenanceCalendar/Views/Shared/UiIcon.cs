using System.Windows.Shapes;

namespace MaintenanceCalendar;

/// <summary>フォントに依存しない、用途を区別できる線画アイコンです。</summary>
internal static class UiIcon
{
    /// <summary>24単位のベクターを指定した表示サイズに拡大縮小します。</summary>
    public static Viewbox Create(string name, double size = 20)
    {
        string data = name switch
        {
            "Lock" => "M6,10 L18,10 18,21 6,21 Z M8,10 L8,6 A4,4 0 0 1 16,6 L16,10 M12,14 L12,17",
            "Exit" => "M10,4 L4,4 4,20 10,20 M9,12 L22,12 M17,7 L22,12 17,17",
            "Items" => "M3,7 L12,3 21,7 21,18 12,22 3,18 Z M3,7 L12,11 21,7 M12,11 L12,22 M7,5 L16,9",
            "Dashboard" => "M12,3 A9,9 0 1 0 21,12 L12,12 Z M15,2 L15,9 22,9 A8,8 0 0 0 15,2",
            "History" => "M4,8 A9,9 0 1 1 3,15 M4,3 L4,8 9,8 M12,7 L12,12 16,14",
            "Calendar" => "M4,5 L20,5 20,21 4,21 Z M8,3 L8,7 M16,3 L16,7 M4,9 L20,9 M8,13 L10,13 M14,13 L16,13 M8,17 L10,17",
            "Care" => "M5,20 L14,11 M14,3 A6,6 0 0 0 11,11 L18,18 A3,3 0 0 0 22,14 L15,7 M3,19 L5,21",
            "Settings" => "M9,3 L15,3 16,7 20,8 22,13 19,16 19,20 14,22 11,19 7,19 4,15 6,11 5,7 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12",
            _ => "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18"
        };
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        path.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas, Stretch = Stretch.Uniform };
    }
}

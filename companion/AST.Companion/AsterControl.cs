using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AST.Companion;

/// <summary>
/// Renders Aster from the approved concept-art sprites. State-specific motion and
/// accents stay native so the desktop pet remains light while preserving the artwork.
/// </summary>
public sealed class AsterControl : Control
{
    private readonly Bitmap _idle = Load("aster_idle.png");
    private readonly Bitmap _delivery = Load("aster_delivery.png");
    private readonly Bitmap _useful = Load("aster_useful.png");
    private readonly Bitmap _hint = Load("aster_hint.png");

    private static readonly IBrush Magic = new SolidColorBrush(Color.Parse("#4FD6C6"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E7C36A"));
    private static readonly IBrush Trap = new SolidColorBrush(Color.Parse("#C76555"));
    private static readonly IBrush Moss = new SolidColorBrush(Color.Parse("#6B7F4A"));

    public AsterState State { get; set; } = AsterState.Idle;
    public double Phase { get; set; }

    public AsterControl()
    {
        Width = 210;
        Height = 235;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bitmap = State switch
        {
            AsterState.DeliveringItem => _delivery,
            AsterState.Progression => _delivery,
            AsterState.Useful => _useful,
            AsterState.Hint => _hint,
            _ => _idle
        };

        var bob = State == AsterState.Offline ? 4 : Math.Sin(Phase) * 4;
        var shake = State == AsterState.Trap ? Math.Sin(Phase * 8) * 6 : 0;
        var scale = State == AsterState.Reconnect
            ? 1 + Math.Max(0, Math.Sin(Phase * 2.2)) * 0.035
            : 1.0;

        var availableWidth = Bounds.Width - 8;
        var availableHeight = Bounds.Height - 10;
        var ratio = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
        var height = Math.Min(availableHeight, availableWidth / ratio) * scale;
        var width = height * ratio;
        var x = (Bounds.Width - width) / 2 + shake;
        var y = (Bounds.Height - height) / 2 + bob;
        var source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var destination = new Rect(x, y, width, height);

        if (State == AsterState.Offline)
        {
            using (context.PushOpacity(0.70))
                context.DrawImage(bitmap, source, destination);
            DrawZzz(context, Bounds.Width - 48, 34);
        }
        else
        {
            context.DrawImage(bitmap, source, destination);
        }

        if (State == AsterState.Trap)
        {
            DrawExclamation(context, Bounds.Width - 47, 35);
        }
        else if (State is AsterState.Progression or AsterState.Reconnect)
        {
            DrawSparkle(context, 25, 42, Gold);
            DrawSparkle(context, Bounds.Width - 30, 62, Magic);
        }
    }

    private static Bitmap Load(string fileName)
    {
        var uri = new Uri($"avares://AST.Companion/Assets/Aster/{fileName}");
        return new Bitmap(AssetLoader.Open(uri));
    }

    private static void DrawSparkle(DrawingContext context, double x, double y, IBrush brush)
    {
        var pen = new Pen(brush, 3);
        context.DrawLine(pen, new Point(x - 7, y), new Point(x + 7, y));
        context.DrawLine(pen, new Point(x, y - 7), new Point(x, y + 7));
    }

    private static void DrawExclamation(DrawingContext context, double x, double y)
    {
        context.DrawLine(new Pen(Trap, 6), new Point(x, y - 13), new Point(x, y + 3));
        context.DrawEllipse(Trap, null, new Rect(x - 3, y + 9, 6, 6));
    }

    private static void DrawZzz(DrawingContext context, double x, double y)
    {
        var pen = new Pen(Moss, 3);
        for (var i = 0; i < 2; i++)
        {
            var ox = x + i * 14;
            var oy = y - i * 11;
            context.DrawLine(pen, new Point(ox, oy), new Point(ox + 10, oy));
            context.DrawLine(pen, new Point(ox + 10, oy), new Point(ox, oy + 10));
            context.DrawLine(pen, new Point(ox, oy + 10), new Point(ox + 10, oy + 10));
        }
    }
}

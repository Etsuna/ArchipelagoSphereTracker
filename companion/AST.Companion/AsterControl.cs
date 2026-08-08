using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AST.Companion;

public sealed class AsterControl : Control
{
    private readonly Bitmap? _aster = LoadSafe("aster_idle.png");

    private static readonly IBrush Magic = new SolidColorBrush(Color.Parse("#4FD6C6"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E7C36A"));
    private static readonly IBrush Trap = new SolidColorBrush(Color.Parse("#C76555"));
    private static readonly IBrush Moss = new SolidColorBrush(Color.Parse("#6B7F4A"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F3EAD2"));

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

        var bob = State == AsterState.Offline ? 4 : Math.Sin(Phase) * 4;
        var shake = State == AsterState.Trap ? Math.Sin(Phase * 8) * 6 : 0;
        var scale = State == AsterState.Reconnect
            ? 1 + Math.Max(0, Math.Sin(Phase * 2.2)) * 0.035
            : 1.0;

        if (_aster is not null)
        {
            var availableWidth = Bounds.Width - 8;
            var availableHeight = Bounds.Height - 10;
            var ratio = (double)_aster.PixelSize.Width / _aster.PixelSize.Height;
            var height = Math.Min(availableHeight, availableWidth / ratio) * scale;
            var width = height * ratio;
            var x = (Bounds.Width - width) / 2 + shake;
            var y = (Bounds.Height - height) / 2 + bob;
            var source = new Rect(0, 0, _aster.PixelSize.Width, _aster.PixelSize.Height);
            var destination = new Rect(x, y, width, height);

            if (State == AsterState.Offline)
            {
                using (context.PushOpacity(0.70))
                    context.DrawImage(_aster, source, destination);
            }
            else
            {
                context.DrawImage(_aster, source, destination);
            }
        }
        else
        {
            DrawFallback(context, Bounds.Width / 2 + shake, Bounds.Height / 2 + bob);
        }

        switch (State)
        {
            case AsterState.Offline:
                DrawZzz(context, Bounds.Width - 48, 34);
                break;
            case AsterState.Trap:
                DrawExclamation(context, Bounds.Width - 47, 35);
                break;
            case AsterState.Progression:
            case AsterState.Reconnect:
                DrawSparkle(context, 25, 42, Gold);
                DrawSparkle(context, Bounds.Width - 30, 62, Magic);
                break;
            case AsterState.Hint:
                DrawSparkle(context, Bounds.Width - 35, 50, Magic);
                break;
            case AsterState.Useful:
                DrawSparkle(context, 30, 55, Magic);
                break;
        }
    }

    private static Bitmap? LoadSafe(string fileName)
    {
        try
        {
            var uri = new Uri($"avares://AST.Companion/Assets/Aster/{fileName}");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var directory = Path.Combine(root, "AST.Companion");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "asset-error.log"), $"{DateTimeOffset.Now:O}\n{fileName}: {ex}\n\n");
            }
            catch { }
            return null;
        }
    }

    private static void DrawFallback(DrawingContext context, double x, double y)
    {
        context.DrawEllipse(Moss, new Pen(Gold, 3), new Rect(x - 48, y - 58, 96, 96));
        context.DrawEllipse(Cream, null, new Rect(x - 31, y - 33, 62, 53));
        context.DrawEllipse(Moss, null, new Rect(x - 24, y + 18, 48, 55));
        context.DrawEllipse(Magic, new Pen(Gold, 2), new Rect(x + 43, y - 35, 24, 24));
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

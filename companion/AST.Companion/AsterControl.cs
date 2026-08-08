using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AST.Companion;

public sealed class AsterControl : Control
{
    private const int StateColumns = 4;
    private const int StateRows = 2;

    private readonly Bitmap? _stateAtlas = LoadSafe("states.png");
    private readonly Dictionary<AsterState, int> _stateCells = new()
    {
        [AsterState.Idle] = 0,
        [AsterState.DeliveringItem] = 1,
        [AsterState.Useful] = 2,
        [AsterState.Progression] = 3,
        [AsterState.Hint] = 4,
        [AsterState.Trap] = 5,
        [AsterState.Offline] = 6,
        [AsterState.Reconnect] = 7
    };

    private static readonly IBrush Magic = new SolidColorBrush(Color.Parse("#4FD6C6"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E7C36A"));
    private static readonly IBrush Moss = new SolidColorBrush(Color.Parse("#6B7F4A"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F3EAD2"));

    private AsterState _state = AsterState.Idle;
    private double _stateStartedAt;

    public AsterState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;

            _state = value;
            _stateStartedAt = Phase;
            InvalidateVisual();
        }
    }

    public double Phase { get; set; }

    public AsterControl()
    {
        Width = 220;
        Height = 240;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_stateAtlas is null || !_stateCells.TryGetValue(State, out var cell))
        {
            DrawFallback(context, Bounds.Width / 2, Bounds.Height / 2);
            return;
        }

        var elapsed = Math.Max(0, Phase - _stateStartedAt);
        var cellWidth = _stateAtlas.PixelSize.Width / StateColumns;
        var cellHeight = _stateAtlas.PixelSize.Height / StateRows;
        var col = cell % StateColumns;
        var row = cell / StateColumns;
        var source = new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);

        var bob = 0.0;
        var shiftX = 0.0;
        var scale = 1.0;

        switch (State)
        {
            case AsterState.Idle:
                bob = Math.Sin(Phase * 1.65) * 4.0;
                scale = 1.0 + Math.Sin(Phase * 1.65 + 0.5) * 0.006;
                break;

            case AsterState.DeliveringItem:
                bob = Math.Sin(elapsed * 3.2) * 2.0;
                shiftX = Math.Sin(Math.Min(elapsed, 1.4) * Math.PI / 1.4) * 5.0;
                scale = 1.0 + Math.Sin(Math.Min(elapsed, 1.2) * Math.PI / 1.2) * 0.025;
                break;

            case AsterState.Useful:
                bob = Math.Sin(Phase * 2.2) * 3.0;
                scale = 1.0 + Math.Max(0, Math.Sin(elapsed * 4.5)) * 0.018;
                break;

            case AsterState.Progression:
                bob = -Math.Abs(Math.Sin(elapsed * 4.2)) * 8.0;
                scale = 1.0 + Math.Max(0, Math.Sin(elapsed * 4.2)) * 0.045;
                break;

            case AsterState.Hint:
                bob = Math.Sin(Phase * 1.3) * 2.0;
                shiftX = Math.Sin(Phase * 0.8) * 1.5;
                break;

            case AsterState.Trap:
                shiftX = Math.Sin(elapsed * 22.0) * Math.Max(0, 7.0 - elapsed * 2.5);
                bob = -Math.Abs(Math.Sin(elapsed * 8.0)) * Math.Max(0, 7.0 - elapsed * 2.0);
                break;

            case AsterState.Offline:
                scale = 1.0 + Math.Sin(Phase * 1.1) * 0.012;
                bob = Math.Sin(Phase * 1.1) * 1.0;
                break;

            case AsterState.Reconnect:
                bob = -Math.Abs(Math.Sin(elapsed * 3.6)) * 6.0;
                scale = 1.0 + Math.Max(0, Math.Sin(elapsed * 3.6)) * 0.04;
                break;
        }

        DrawBitmap(context, _stateAtlas, source, shiftX, bob, scale);

        if (State is AsterState.Progression or AsterState.Reconnect)
        {
            var pulse = 0.55 + Math.Abs(Math.Sin(Phase * 3.0)) * 0.45;
            DrawSparkle(context, 24, 44, Gold, pulse);
            DrawSparkle(context, Bounds.Width - 28, 62, Magic, 1.0 - pulse * 0.35);
        }
        else if (State is AsterState.Hint or AsterState.Useful)
        {
            DrawSparkle(context, Bounds.Width - 27, 55, Magic, 0.65 + Math.Abs(Math.Sin(Phase * 2.4)) * 0.35);
        }
    }

    private void DrawBitmap(DrawingContext context, Bitmap bitmap, Rect source, double shiftX, double bob, double scale)
    {
        var availableWidth = Bounds.Width - 6;
        var availableHeight = Bounds.Height - 6;
        var ratio = source.Width / source.Height;
        var height = Math.Min(availableHeight, availableWidth / ratio) * scale;
        var width = height * ratio;
        var x = (Bounds.Width - width) / 2 + shiftX;
        var y = Bounds.Height - height + bob;
        context.DrawImage(bitmap, source, new Rect(x, y, width, height));
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

    private static void DrawSparkle(DrawingContext context, double x, double y, IBrush brush, double opacity)
    {
        using var _ = context.PushOpacity(Math.Clamp(opacity, 0.2, 1.0));
        var pen = new Pen(brush, 3);
        context.DrawLine(pen, new Point(x - 7, y), new Point(x + 7, y));
        context.DrawLine(pen, new Point(x, y - 7), new Point(x, y + 7));
    }
}

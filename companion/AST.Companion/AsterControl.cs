using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AST.Companion;

public sealed class AsterControl : Control
{
    private const int FrameCount = 6;

    private readonly Dictionary<AsterState, Bitmap?> _states = new()
    {
        [AsterState.Idle] = LoadSafe("state_idle.png"),
        [AsterState.DeliveringItem] = LoadSafe("state_delivery.png"),
        [AsterState.Progression] = LoadSafe("state_progression.png"),
        [AsterState.Useful] = LoadSafe("state_useful.png"),
        [AsterState.Trap] = LoadSafe("state_trap.png"),
        [AsterState.Hint] = LoadSafe("state_hint.png"),
        [AsterState.Offline] = LoadSafe("state_sleep.png"),
        [AsterState.Reconnect] = LoadSafe("state_reconnect.png")
    };

    private readonly Dictionary<AsterState, Bitmap?> _animations = new()
    {
        [AsterState.Idle] = LoadSafe("anim_idle.png"),
        [AsterState.DeliveringItem] = LoadSafe("anim_delivery.png"),
        [AsterState.Trap] = LoadSafe("anim_trap.png"),
        [AsterState.Offline] = LoadSafe("anim_sleep.png")
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

        var statePhase = Math.Max(0, Phase - _stateStartedAt);
        var shake = State == AsterState.Trap ? Math.Sin(statePhase * 18) * Math.Max(0, 5 - statePhase) : 0;
        var scale = State is AsterState.Progression or AsterState.Reconnect
            ? 1 + Math.Max(0, Math.Sin(statePhase * 5)) * 0.045
            : 1.0;

        if (_animations.TryGetValue(State, out var strip) && strip is not null)
        {
            DrawAnimatedStrip(context, strip, statePhase, shake, scale);
        }
        else if (_states.TryGetValue(State, out var bitmap) && bitmap is not null)
        {
            var bob = State is AsterState.Progression or AsterState.Useful or AsterState.Hint or AsterState.Reconnect
                ? Math.Sin(Phase * 1.8) * 3
                : 0;
            DrawBitmap(context, bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), shake, bob, scale);
        }
        else
        {
            DrawFallback(context, Bounds.Width / 2 + shake, Bounds.Height / 2);
        }

        if (State is AsterState.Progression or AsterState.Reconnect)
        {
            DrawSparkle(context, 25, 42, Gold);
            DrawSparkle(context, Bounds.Width - 28, 58, Magic);
        }
        else if (State is AsterState.Hint or AsterState.Useful)
        {
            DrawSparkle(context, Bounds.Width - 28, 54, Magic);
        }
    }

    private void DrawAnimatedStrip(DrawingContext context, Bitmap strip, double statePhase, double shake, double scale)
    {
        // Animation sheets contain six evenly sized horizontal cells.
        var frameWidth = strip.PixelSize.Width / FrameCount;
        var frameHeight = strip.PixelSize.Height;
        var fps = State switch
        {
            AsterState.Idle => 5.0,
            AsterState.Offline => 4.0,
            AsterState.DeliveringItem => 7.0,
            AsterState.Trap => 9.0,
            _ => 6.0
        };

        int frame;
        if (State is AsterState.DeliveringItem or AsterState.Trap)
        {
            // Play reaction once, then hold its last production frame.
            frame = Math.Min(FrameCount - 1, (int)(statePhase * fps));
        }
        else
        {
            frame = (int)(Phase * fps) % FrameCount;
        }

        var source = new Rect(frame * frameWidth, 0, frameWidth, frameHeight);
        DrawBitmap(context, strip, source, shake, 0, scale);
    }

    private void DrawBitmap(DrawingContext context, Bitmap bitmap, Rect source, double shake, double bob, double scale)
    {
        var availableWidth = Bounds.Width - 6;
        var availableHeight = Bounds.Height - 6;
        var ratio = source.Width / source.Height;
        var height = Math.Min(availableHeight, availableWidth / ratio) * scale;
        var width = height * ratio;
        var x = (Bounds.Width - width) / 2 + shake;
        var y = Bounds.Height - height + bob;
        var destination = new Rect(x, y, width, height);
        context.DrawImage(bitmap, source, destination);
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
}

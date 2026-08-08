using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AST.Companion;

public sealed class AsterControl : Control
{
    private readonly IReadOnlyDictionary<AsterState, Bitmap?> _sprites = new Dictionary<AsterState, Bitmap?>
    {
        [AsterState.Idle] = LoadSafe("aster_idle.png"),
        [AsterState.DeliveringItem] = LoadSafe("aster_delivery.png"),
        [AsterState.Useful] = LoadSafe("aster_useful.png"),
        [AsterState.Progression] = LoadSafe("aster_progression.png"),
        [AsterState.Hint] = LoadSafe("aster_hint.png"),
        [AsterState.Trap] = LoadSafe("aster_trap.png"),
        [AsterState.Offline] = LoadSafe("aster_offline.png"),
        [AsterState.Reconnect] = LoadSafe("aster_reconnect.png")
    };

    private static readonly IBrush Magic = new SolidColorBrush(Color.Parse("#4FD6C6"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E7C36A"));
    private static readonly IBrush Moss = new SolidColorBrush(Color.Parse("#6B7F4A"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F3EAD2"));
    private static readonly IBrush TrapBrush = new SolidColorBrush(Color.Parse("#C35A4A"));

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

    /// <summary>Monotonic animation clock in seconds.</summary>
    public double Phase { get; set; }

    public AsterControl()
    {
        Width = 220;
        Height = 240;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!_sprites.TryGetValue(State, out var sprite) || sprite is null)
        {
            DrawFallback(context, Bounds.Width / 2, Bounds.Height / 2);
            return;
        }

        var elapsed = Math.Max(0, Phase - _stateStartedAt);
        var bob = 0.0;
        var shiftX = 0.0;
        var scaleX = 1.0;
        var scaleY = 1.0;
        var opacity = 1.0;

        switch (State)
        {
            case AsterState.Idle:
                // Gentle breathing + floating. This always moves, even when no notification is visible.
                bob = Math.Sin(Phase * 2.1) * 4.0;
                scaleX = 1.0 + Math.Sin(Phase * 2.1 + 0.7) * 0.012;
                scaleY = 1.0 + Math.Sin(Phase * 2.1) * 0.018;
                break;

            case AsterState.DeliveringItem:
                // Step forward, present the reward, settle back.
                shiftX = Math.Sin(Math.Min(elapsed, 1.15) / 1.15 * Math.PI) * 11.0;
                bob = -Math.Sin(Math.Min(elapsed, 0.9) / 0.9 * Math.PI) * 7.0;
                scaleX = 1.0 + Math.Sin(Math.Min(elapsed, 1.0) * Math.PI) * 0.035;
                scaleY = 1.0 + Math.Sin(Math.Min(elapsed, 1.0) * Math.PI) * 0.035;
                break;

            case AsterState.Useful:
                // Cheerful repeated pulse with a lively orb sparkle.
                bob = Math.Sin(Phase * 3.0) * 4.5;
                scaleX = 1.0 + Math.Max(0, Math.Sin(Phase * 4.0)) * 0.035;
                scaleY = 1.0 + Math.Max(0, Math.Sin(Phase * 4.0)) * 0.035;
                break;

            case AsterState.Progression:
                // Strong celebratory bounce / squash-and-stretch.
                bob = -Math.Abs(Math.Sin(elapsed * 5.0)) * 16.0;
                scaleX = 1.0 + Math.Max(0, Math.Sin(elapsed * 5.0)) * 0.07;
                scaleY = 1.0 - Math.Max(0, Math.Sin(elapsed * 5.0)) * 0.025;
                break;

            case AsterState.Hint:
                // Curious side-to-side motion, slower than the reward animations.
                shiftX = Math.Sin(Phase * 1.8) * 5.0;
                bob = Math.Sin(Phase * 1.35 + 0.8) * 2.5;
                scaleX = scaleY = 1.0 + Math.Sin(Phase * 1.35) * 0.012;
                break;

            case AsterState.Trap:
                // Sharp recoil and shake, strongest during the first two seconds.
                var strength = Math.Max(0.20, 1.0 - elapsed / 2.2);
                shiftX = Math.Sin(elapsed * 31.0) * 13.0 * strength;
                bob = -Math.Abs(Math.Sin(elapsed * 10.0)) * 10.0 * strength;
                scaleX = 1.0 + Math.Sin(elapsed * 18.0) * 0.04 * strength;
                scaleY = 1.0 - Math.Sin(elapsed * 18.0) * 0.025 * strength;
                break;

            case AsterState.Offline:
                // Slow sleeping-breath loop with a subtle fade, deliberately much calmer than idle.
                bob = Math.Sin(Phase * 0.9) * 1.2;
                scaleX = 1.0 + Math.Sin(Phase * 0.9) * 0.010;
                scaleY = 1.0 + Math.Sin(Phase * 0.9) * 0.025;
                opacity = 0.86 + Math.Sin(Phase * 0.9) * 0.05;
                break;

            case AsterState.Reconnect:
                // Wake-up pop: compress, jump, overshoot, then settle.
                if (elapsed < 0.20)
                {
                    scaleX = 1.12;
                    scaleY = 0.88;
                    bob = 5.0;
                }
                else if (elapsed < 0.75)
                {
                    var t = (elapsed - 0.20) / 0.55;
                    bob = -Math.Sin(t * Math.PI) * 18.0;
                    scaleX = 0.94 + t * 0.10;
                    scaleY = 1.08 - t * 0.05;
                }
                else
                {
                    bob = -Math.Abs(Math.Sin(elapsed * 4.2)) * 5.0;
                }
                break;
        }

        using (context.PushOpacity(Math.Clamp(opacity, 0.0, 1.0)))
            DrawBitmap(context, sprite, shiftX, bob, scaleX, scaleY);

        DrawStateEffects(context, elapsed);
    }

    private void DrawStateEffects(DrawingContext context, double elapsed)
    {
        if (State is AsterState.Progression or AsterState.Reconnect)
        {
            var pulse = 0.55 + Math.Abs(Math.Sin(Phase * 5.0)) * 0.45;
            DrawSparkle(context, 24, 42, Gold, pulse, 8);
            DrawSparkle(context, Bounds.Width - 25, 58, Magic, 1.0 - pulse * 0.25, 7);
            DrawSparkle(context, Bounds.Width - 48, 24, Gold, 0.5 + pulse * 0.5, 5);
        }
        else if (State is AsterState.Hint or AsterState.Useful)
        {
            DrawSparkle(context, Bounds.Width - 25, 52, Magic,
                0.55 + Math.Abs(Math.Sin(Phase * 3.6)) * 0.45, 7);
        }
        else if (State == AsterState.Trap)
        {
            var pulse = 0.55 + Math.Abs(Math.Sin(elapsed * 10.0)) * 0.45;
            DrawSparkle(context, 29, 42, TrapBrush, pulse, 9);
            DrawSparkle(context, Bounds.Width - 28, 37, Gold, pulse, 6);
        }
        else if (State == AsterState.Offline)
        {
            var pulse = 0.38 + Math.Abs(Math.Sin(Phase * 0.9)) * 0.35;
            DrawSleepMark(context, Bounds.Width - 52, 56, pulse);
            DrawSleepMark(context, Bounds.Width - 34, 38, pulse * 0.8);
        }
    }

    private void DrawBitmap(
        DrawingContext context,
        Bitmap bitmap,
        double shiftX,
        double bob,
        double scaleX,
        double scaleY)
    {
        var source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var availableWidth = Bounds.Width - 4;
        var availableHeight = Bounds.Height - 4;
        var ratio = source.Width / source.Height;
        var baseHeight = Math.Min(availableHeight, availableWidth / ratio);
        var baseWidth = baseHeight * ratio;
        var width = baseWidth * scaleX;
        var height = baseHeight * scaleY;
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
                File.AppendAllText(
                    Path.Combine(directory, "asset-error.log"),
                    $"{DateTimeOffset.Now:O}\n{fileName}: {ex}\n\n");
            }
            catch
            {
                // Aster must never crash the companion because of an art resource.
            }

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

    private static void DrawSparkle(
        DrawingContext context,
        double x,
        double y,
        IBrush brush,
        double opacity,
        double size)
    {
        using var _ = context.PushOpacity(Math.Clamp(opacity, 0.15, 1.0));
        var pen = new Pen(brush, 2.5);
        context.DrawLine(pen, new Point(x - size, y), new Point(x + size, y));
        context.DrawLine(pen, new Point(x, y - size), new Point(x, y + size));
    }

    private static void DrawSleepMark(DrawingContext context, double x, double y, double opacity)
    {
        using var _ = context.PushOpacity(Math.Clamp(opacity, 0.15, 0.8));
        var pen = new Pen(Magic, 3);
        context.DrawLine(pen, new Point(x - 5, y - 5), new Point(x + 5, y - 5));
        context.DrawLine(pen, new Point(x + 5, y - 5), new Point(x - 5, y + 5));
        context.DrawLine(pen, new Point(x - 5, y + 5), new Point(x + 5, y + 5));
    }
}

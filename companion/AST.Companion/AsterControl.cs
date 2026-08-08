using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AST.Companion;

public sealed class AsterControl : Control
{
    private static readonly IBrush Moss = new SolidColorBrush(Color.Parse("#6B7F4A"));
    private static readonly IBrush Sage = new SolidColorBrush(Color.Parse("#8BA26B"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F3EAD2"));
    private static readonly IBrush Leather = new SolidColorBrush(Color.Parse("#8B5E34"));
    private static readonly IBrush Magic = new SolidColorBrush(Color.Parse("#4FD6C6"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E7C36A"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#273126"));
    private static readonly IBrush Blush = new SolidColorBrush(Color.Parse("#E9B9A0"));
    private static readonly IBrush TrapRed = new SolidColorBrush(Color.Parse("#C76555"));
    private static readonly Pen InkPen = new(Ink, 2.2);
    private static readonly Pen GoldPen = new(Gold, 2);

    public AsterState State { get; set; } = AsterState.Idle;
    public double Phase { get; set; }

    public AsterControl()
    {
        Width = 190;
        Height = 220;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 1 || h <= 1)
            return;

        var bob = State == AsterState.Offline ? 4 : Math.Sin(Phase) * 4;
        var shake = State == AsterState.Trap ? Math.Sin(Phase * 8) * 5 : 0;
        var cx = w / 2 + shake;
        var cy = h / 2 + bob;

        DrawShadow(context, cx, h - 20);
        DrawOrb(context, cx + 68, cy - 34);

        if (State == AsterState.Offline)
        {
            DrawSleeping(context, cx, cy + 24);
            return;
        }

        DrawBody(context, cx, cy);
        DrawExpression(context, cx, cy);
        DrawStateAccent(context, cx, cy);
    }

    private static void DrawShadow(DrawingContext ctx, double cx, double y)
    {
        var shadow = new SolidColorBrush(Color.FromArgb(45, 20, 25, 20));
        ctx.DrawEllipse(shadow, null, new Rect(cx - 48, y - 7, 96, 14));
    }

    private void DrawOrb(DrawingContext ctx, double x, double y)
    {
        var pulse = 1 + Math.Sin(Phase * 1.7) * 0.08;
        var r = 15 * pulse;
        var aura = new SolidColorBrush(Color.FromArgb(55, 79, 214, 198));
        ctx.DrawEllipse(aura, null, new Rect(x - r - 6, y - r - 6, (r + 6) * 2, (r + 6) * 2));
        ctx.DrawEllipse(Magic, null, new Rect(x - r, y - r, r * 2, r * 2));
        ctx.DrawEllipse(Cream, null, new Rect(x - 5, y - 7, 7, 7));
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), null, new Rect(x + 2, y + 3, 5, 5));
        ctx.DrawEllipse(null, GoldPen, new Rect(x - r - 2, y - r - 2, (r + 2) * 2, (r + 2) * 2));
    }

    private static void DrawBody(DrawingContext ctx, double cx, double cy)
    {
        // Cape / hood silhouette.
        ctx.DrawEllipse(Moss, InkPen, new Rect(cx - 58, cy - 78, 116, 105));
        ctx.DrawEllipse(Sage, null, new Rect(cx - 48, cy - 69, 96, 82));

        // Leaf sprout.
        ctx.DrawEllipse(Sage, InkPen, new Rect(cx - 3, cy - 94, 12, 25));
        ctx.DrawEllipse(Moss, InkPen, new Rect(cx - 17, cy - 91, 18, 11));

        // Side leaf ears.
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx - 70, cy - 35, 25, 13));
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx + 45, cy - 35, 25, 13));

        // Face.
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx - 42, cy - 54, 84, 72));

        // Leaf collar.
        ctx.DrawEllipse(Moss, InkPen, new Rect(cx - 44, cy + 2, 34, 18));
        ctx.DrawEllipse(Moss, InkPen, new Rect(cx + 10, cy + 2, 34, 18));

        // Tunic.
        ctx.DrawRectangle(Cream, InkPen, new Rect(cx - 34, cy + 15, 68, 62), 18, 18);
        ctx.DrawLine(new Pen(Sage, 3), new Point(cx, cy + 36), new Point(cx, cy + 62));
        ctx.DrawLine(new Pen(Sage, 2), new Point(cx, cy + 46), new Point(cx - 10, cy + 39));
        ctx.DrawLine(new Pen(Sage, 2), new Point(cx, cy + 51), new Point(cx + 11, cy + 43));

        // Tiny feet.
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx - 29, cy + 68, 22, 15));
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx + 7, cy + 68, 22, 15));

        // Satchel strap + bag.
        ctx.DrawLine(new Pen(Leather, 6), new Point(cx - 26, cy - 2), new Point(cx + 31, cy + 64));
        ctx.DrawRectangle(Leather, InkPen, new Rect(cx + 24, cy + 42, 36, 30), 8, 8);
        ctx.DrawEllipse(Gold, null, new Rect(cx + 39, cy + 51, 7, 7));

        // Golden clasp.
        ctx.DrawEllipse(Gold, InkPen, new Rect(cx - 8, cy + 7, 16, 16));
    }

    private void DrawExpression(DrawingContext ctx, double cx, double cy)
    {
        var eyeY = cy - 27;
        if (State == AsterState.Trap)
        {
            ctx.DrawLine(InkPen, new Point(cx - 24, eyeY - 4), new Point(cx - 10, eyeY + 5));
            ctx.DrawLine(InkPen, new Point(cx - 10, eyeY - 4), new Point(cx - 24, eyeY + 5));
            ctx.DrawLine(InkPen, new Point(cx + 10, eyeY - 4), new Point(cx + 24, eyeY + 5));
            ctx.DrawLine(InkPen, new Point(cx + 24, eyeY - 4), new Point(cx + 10, eyeY + 5));
            ctx.DrawEllipse(TrapRed, InkPen, new Rect(cx - 9, cy - 8, 18, 18));
            return;
        }

        if (State == AsterState.Progression || State == AsterState.Reconnect)
        {
            ctx.DrawLine(InkPen, new Point(cx - 25, eyeY), new Point(cx - 13, eyeY + 4));
            ctx.DrawLine(InkPen, new Point(cx + 13, eyeY + 4), new Point(cx + 25, eyeY));
            ctx.DrawEllipse(Blush, null, new Rect(cx - 34, cy - 9, 12, 7));
            ctx.DrawEllipse(Blush, null, new Rect(cx + 22, cy - 9, 12, 7));
            ctx.DrawEllipse(Ink, null, new Rect(cx - 6, cy - 5, 12, 8));
            return;
        }

        if (State == AsterState.Hint)
        {
            ctx.DrawEllipse(Ink, null, new Rect(cx - 25, eyeY - 5, 11, 15));
            ctx.DrawEllipse(Ink, null, new Rect(cx + 14, eyeY - 5, 11, 15));
            ctx.DrawLine(InkPen, new Point(cx - 5, cy - 3), new Point(cx + 5, cy - 3));
            return;
        }

        ctx.DrawEllipse(Ink, null, new Rect(cx - 25, eyeY - 5, 12, 17));
        ctx.DrawEllipse(Ink, null, new Rect(cx + 13, eyeY - 5, 12, 17));
        ctx.DrawEllipse(Cream, null, new Rect(cx - 21, eyeY - 1, 4, 5));
        ctx.DrawEllipse(Cream, null, new Rect(cx + 17, eyeY - 1, 4, 5));

        if (State == AsterState.Useful || State == AsterState.DeliveringItem)
            ctx.DrawEllipse(Blush, null, new Rect(cx - 35, cy - 8, 12, 7));

        ctx.DrawLine(InkPen, new Point(cx - 5, cy - 2), new Point(cx, cy + 2));
        ctx.DrawLine(InkPen, new Point(cx, cy + 2), new Point(cx + 5, cy - 2));
    }

    private void DrawStateAccent(DrawingContext ctx, double cx, double cy)
    {
        switch (State)
        {
            case AsterState.Progression:
                DrawSparkle(ctx, cx - 65, cy - 53, Gold);
                DrawSparkle(ctx, cx + 54, cy - 73, Gold);
                DrawKey(ctx, cx - 67, cy + 7);
                break;
            case AsterState.Useful:
                DrawSparkle(ctx, cx - 59, cy - 56, Magic);
                DrawPouch(ctx, cx - 65, cy + 13);
                break;
            case AsterState.Trap:
                DrawExclamation(ctx, cx + 52, cy - 68);
                DrawSpikes(ctx, cx, cy + 86);
                break;
            case AsterState.Hint:
                DrawScroll(ctx, cx - 65, cy + 15);
                break;
            case AsterState.Reconnect:
                DrawSparkle(ctx, cx - 58, cy - 62, Gold);
                DrawSparkle(ctx, cx + 54, cy - 52, Magic);
                break;
            case AsterState.DeliveringItem:
                DrawParcel(ctx, cx - 65, cy + 13);
                break;
        }
    }

    private static void DrawSparkle(DrawingContext ctx, double x, double y, IBrush brush)
    {
        var pen = new Pen(brush, 3);
        ctx.DrawLine(pen, new Point(x - 8, y), new Point(x + 8, y));
        ctx.DrawLine(pen, new Point(x, y - 8), new Point(x, y + 8));
    }

    private static void DrawKey(DrawingContext ctx, double x, double y)
    {
        ctx.DrawEllipse(null, new Pen(Gold, 5), new Rect(x - 10, y - 26, 20, 20));
        ctx.DrawLine(new Pen(Gold, 6), new Point(x, y - 5), new Point(x, y + 25));
        ctx.DrawLine(new Pen(Gold, 5), new Point(x, y + 18), new Point(x + 12, y + 18));
    }

    private static void DrawPouch(DrawingContext ctx, double x, double y)
    {
        ctx.DrawEllipse(Leather, InkPen, new Rect(x - 15, y - 18, 30, 31));
        ctx.DrawLine(GoldPen, new Point(x - 8, y - 13), new Point(x + 8, y - 13));
        ctx.DrawEllipse(Gold, null, new Rect(x - 3, y - 2, 6, 6));
    }

    private static void DrawParcel(DrawingContext ctx, double x, double y)
    {
        ctx.DrawRectangle(Cream, InkPen, new Rect(x - 17, y - 16, 34, 30), 5, 5);
        ctx.DrawLine(new Pen(Sage, 4), new Point(x, y - 16), new Point(x, y + 14));
        ctx.DrawLine(new Pen(Sage, 4), new Point(x - 17, y - 1), new Point(x + 17, y - 1));
    }

    private static void DrawScroll(DrawingContext ctx, double x, double y)
    {
        ctx.DrawRectangle(Cream, InkPen, new Rect(x - 18, y - 20, 36, 34), 6, 6);
        ctx.DrawLine(new Pen(Sage, 2), new Point(x - 10, y - 9), new Point(x + 10, y - 9));
        ctx.DrawLine(new Pen(Sage, 2), new Point(x - 10, y), new Point(x + 7, y));
        ctx.DrawLine(new Pen(Sage, 2), new Point(x - 10, y + 8), new Point(x + 4, y + 8));
    }

    private static void DrawSpikes(DrawingContext ctx, double cx, double y)
    {
        var pen = new Pen(Ink, 3);
        for (var i = -2; i <= 2; i++)
        {
            var x = cx + i * 17;
            ctx.DrawLine(pen, new Point(x - 7, y), new Point(x, y - 15));
            ctx.DrawLine(pen, new Point(x, y - 15), new Point(x + 7, y));
        }
    }

    private static void DrawExclamation(DrawingContext ctx, double x, double y)
    {
        ctx.DrawLine(new Pen(Gold, 6), new Point(x, y - 15), new Point(x, y + 2));
        ctx.DrawEllipse(Gold, null, new Rect(x - 3, y + 8, 6, 6));
    }

    private static void DrawSleeping(DrawingContext ctx, double cx, double cy)
    {
        ctx.DrawEllipse(Moss, InkPen, new Rect(cx - 67, cy - 32, 134, 78));
        ctx.DrawEllipse(Sage, null, new Rect(cx - 57, cy - 24, 93, 57));
        ctx.DrawEllipse(Cream, InkPen, new Rect(cx - 42, cy - 32, 66, 49));
        ctx.DrawLine(InkPen, new Point(cx - 27, cy - 10), new Point(cx - 16, cy - 7));
        ctx.DrawLine(InkPen, new Point(cx + 2, cy - 7), new Point(cx + 13, cy - 10));
        ctx.DrawLine(InkPen, new Point(cx - 7, cy + 4), new Point(cx, cy + 7));
        ctx.DrawLine(InkPen, new Point(cx, cy + 7), new Point(cx + 7, cy + 4));

        var zPen = new Pen(Sage, 3);
        ctx.DrawLine(zPen, new Point(cx + 44, cy - 43), new Point(cx + 57, cy - 43));
        ctx.DrawLine(zPen, new Point(cx + 57, cy - 43), new Point(cx + 44, cy - 28));
        ctx.DrawLine(zPen, new Point(cx + 44, cy - 28), new Point(cx + 57, cy - 28));
    }
}

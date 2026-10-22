using System.Windows;

namespace FuturesTicker.Native;

/// <summary>
/// Detects the taskbar edge and computes a docking position for the widget.
/// All coordinates are in WPF device-independent units (DIPs), which match
/// <see cref="SystemParameters.WorkArea"/> directly.
/// </summary>
public static class TaskbarHelper
{
    public enum TaskbarEdge { Left, Top, Right, Bottom }

    public static TaskbarEdge GetEdge()
    {
        var work = SystemParameters.WorkArea;
        var screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        if (work.Top > screen.Top + 1) return TaskbarEdge.Top;
        if (work.Left > screen.Left + 1) return TaskbarEdge.Left;
        if (work.Bottom < screen.Bottom - 1) return TaskbarEdge.Bottom;
        return TaskbarEdge.Right;
    }

    /// <summary>
    /// Returns the top-left position to dock the given window size next to the
    /// taskbar (bottom-right by default), with a small margin. The result is
    /// clamped so the window's bottom-right corner never crosses the work area
    /// (which already excludes the taskbar).
    /// </summary>
    public static Point GetDockPosition(Size windowSize, double margin)
    {
        var work = SystemParameters.WorkArea;
        Point pos = GetEdge() switch
        {
            TaskbarEdge.Top => new Point(work.Right - windowSize.Width - margin, work.Top + margin),
            TaskbarEdge.Left => new Point(work.Left + margin, work.Bottom - windowSize.Height - margin),
            TaskbarEdge.Right => new Point(work.Right - windowSize.Width - margin, work.Bottom - windowSize.Height - margin),
            _ => new Point(work.Right - windowSize.Width - margin, work.Bottom - windowSize.Height - margin),
        };

        return ClampToWorkArea(pos, windowSize);
    }

    /// <summary>
    /// Clamps a window's top-left position so the entire window stays inside the
    /// work area (which already excludes the taskbar). Used both when docking and
    /// while the user drags the widget, so it can never leave the screen.
    /// </summary>
    public static Point ClampToWorkArea(Point position, Size windowSize)
    {
        var work = SystemParameters.WorkArea;
        double x = Math.Max(work.Left, Math.Min(position.X, work.Right - windowSize.Width));
        double y = Math.Max(work.Top, Math.Min(position.Y, work.Bottom - windowSize.Height));
        return new Point(x, y);
    }
}

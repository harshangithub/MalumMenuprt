using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MalumMenuOverlay;

/// <summary>
/// Locates the Among Us process and tracks its main window rectangle
/// so the overlay can stay perfectly aligned.
/// </summary>
public static class ProcessDetector
{
    // Among Us uses "Among Us" as the process name on Steam/Epic.
    private const string GameProcessName = "Among Us";

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

    private static Process? _cachedProcess;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>Current Among Us process, or null if not running.</summary>
    public static Process? GameProcess
    {
        get
        {
            if (DateTime.UtcNow - _lastCheck < CheckInterval)
                return _cachedProcess?.HasExited == false ? _cachedProcess : null;

            _lastCheck = DateTime.UtcNow;

            if (_cachedProcess is { HasExited: false })
                return _cachedProcess;

            try
            {
                var procs = Process.GetProcessesByName(GameProcessName);
                _cachedProcess = procs.Length > 0 ? procs[0] : null;
            }
            catch
            {
                _cachedProcess = null;
            }
            return _cachedProcess;
        }
    }

    public static bool IsGameRunning => GameProcess != null;

    public static IntPtr GameWindowHandle =>
        GameProcess?.MainWindowHandle ?? IntPtr.Zero;

    /// <summary>
    /// Returns the screen bounds of the Among Us window, or null if unavailable.
    /// </summary>
    public static Rectangle? GetGameWindowRect()
    {
        var hwnd = GameWindowHandle;
        if (hwnd == IntPtr.Zero) return null;
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd)) return null;
        if (!GetWindowRect(hwnd, out var r)) return null;
        if (r.Right - r.Left <= 0 || r.Bottom - r.Top <= 0) return null;
        return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}

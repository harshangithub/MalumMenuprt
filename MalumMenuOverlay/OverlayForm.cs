using System.Runtime.InteropServices;

namespace MalumMenuOverlay;

/// <summary>
/// Transparent, always-on-top, click-through overlay window.
///
/// • Follows the Among Us window position/size automatically.
/// • Press INSERT to show/hide the cheat menu.
/// • When the menu is hidden, all mouse input passes through to the game.
/// • When the menu is visible, mouse events are captured for menu interaction.
/// </summary>
public sealed class OverlayForm : Form
{
    // ── Win32 constants ──────────────────────────────────────────────────
    private const int GWL_EXSTYLE    = -20;
    private const int WS_EX_LAYERED  = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST  = 0x00000008;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const uint LWA_ALPHA    = 0x02;
    private const uint LWA_COLORKEY = 0x01;

    // ── Child renderers ──────────────────────────────────────────────────
    private readonly MenuRenderer    _menu    = new();
    private readonly OverlayRenderer _overlay = new();

    // ── Timers ───────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _posTimer;   // track game window
    private readonly System.Windows.Forms.Timer _drawTimer;  // repaint loop

    // ── Double-buffer bitmap ─────────────────────────────────────────────
    private Bitmap?   _backBuf;
    private Graphics? _backGfx;

    // ── Menu-visibility state ─────────────────────────────────────────────
    private bool _menuVisible;

    // ── Keybinds ─────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _keyTimer;
    private readonly Dictionary<Keys, DateTime> _keyLastFired = new();
    private const int KeyRepeatMs = 200;

    public OverlayForm()
    {
        SuspendLayout();

        // Form style
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar    = false;
        TopMost          = true;
        BackColor        = Color.Black;       // becomes transparent via colorkey
        TransparencyKey  = Color.Black;
        StartPosition    = FormStartPosition.Manual;

        // Cover the entire primary screen so the cheat menu can be dragged
        // anywhere without being clipped at the game window's boundary.
        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Location = screen.Location;
        Size     = screen.Size;
        Opacity  = 1.0;

        // Double-buffering
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint            |
                 ControlStyles.OptimizedDoubleBuffer, true);

        // Position tracker – runs every 100 ms
        _posTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _posTimer.Tick += PosTimer_Tick;
        _posTimer.Start();

        // Draw timer – ~60 fps
        _drawTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _drawTimer.Tick += (_, _) => Invalidate();
        _drawTimer.Start();

        // Key polling timer
        _keyTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _keyTimer.Tick += KeyTimer_Tick;
        _keyTimer.Start();

        ResumeLayout(false);
    }

    // ── Window creation ───────────────────────────────────────────────────
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MakeClickThrough();
    }

    private void MakeClickThrough()
    {
        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
        SetWindowLong(Handle, GWL_EXSTYLE, style);
        // LWA_COLORKEY makes black pixels transparent; LWA_ALPHA keeps the rest fully opaque.
        SetLayeredWindowAttributes(Handle, 0, 255, LWA_COLORKEY | LWA_ALPHA);
    }

    private void RestoreClickCapture()
    {
        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TOPMOST;
        style &= ~WS_EX_TRANSPARENT;
        style &= ~WS_EX_NOACTIVATE;
        SetWindowLong(Handle, GWL_EXSTYLE, style);
        SetLayeredWindowAttributes(Handle, 0, 255, LWA_COLORKEY | LWA_ALPHA);
    }

    // ── Menu visibility toggle ────────────────────────────────────────────
    private void SetMenuVisible(bool visible)
    {
        _menuVisible = visible;
        _menu.IsVisible = visible;

        if (visible)
            RestoreClickCapture();
        else
            MakeClickThrough();
    }

    // ── Position tracking ─────────────────────────────────────────────────
    private void PosTimer_Tick(object? sender, EventArgs e)
    {
        // Always keep the overlay covering the full primary screen.
        // This ensures the cheat menu can be freely dragged to any position
        // without disappearing at the edge of a windowed game boundary.
        // The game-window rectangle is still used by OnPaint to correctly
        // position ESP elements relative to the game.
        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        if (Location != screen.Location || Size != screen.Size)
        {
            Location = screen.Location;
            Size     = screen.Size;
            ResizeBackBuffer();
        }
    }

    private void ResizeBackBuffer()
    {
        _backGfx?.Dispose();
        _backBuf?.Dispose();
        _backBuf = new Bitmap(Math.Max(Width, 1), Math.Max(Height, 1));
        _backGfx = Graphics.FromImage(_backBuf);
        _backGfx.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        _backGfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

    // ── Key polling ───────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

    private bool IsKeyPressed(Keys key)
    {
        return (GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    private bool IsKeyFired(Keys key)
    {
        if (!IsKeyPressed(key)) return false;
        if (_keyLastFired.TryGetValue(key, out var last) &&
            (DateTime.UtcNow - last).TotalMilliseconds < KeyRepeatMs) return false;
        _keyLastFired[key] = DateTime.UtcNow;
        return true;
    }

    private void KeyTimer_Tick(object? sender, EventArgs e)
    {
        // INSERT – toggle menu
        if (IsKeyFired(Keys.Insert))
            SetMenuVisible(!_menuVisible);

        // DELETE – disable all cheats
        if (IsKeyFired(Keys.Delete))
            CheatState.DisableAll();

        // F10 – save profile
        if (IsKeyFired(Keys.F10))
            CheatState.SaveProfile();

        // F9 – load profile
        if (IsKeyFired(Keys.F9))
            CheatState.LoadProfile();

        // User-configured keybinds
        foreach (var (name, key) in CheatState.Keybinds)
        {
            if (key == Keys.None) continue;
            if (IsKeyFired(key)) CheatState.Toggle(name);
        }
    }

    // ── Paint ─────────────────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        if (_backBuf == null || _backGfx == null)
            ResizeBackBuffer();

        var g = _backGfx!;
        g.Clear(Color.Black);           // transparent background (colorkey)

        var gameRect = ProcessDetector.GetGameWindowRect();
        var bounds   = new Rectangle(0, 0, Width, Height);

        // Status message
        _overlay.DrawStatusMessage(g, bounds);

        // ESP / tracers (drawn relative to game window mapped onto overlay)
        var localGameRect = gameRect.HasValue
            ? new Rectangle(gameRect.Value.X - Left,
                            gameRect.Value.Y - Top,
                            gameRect.Value.Width,
                            gameRect.Value.Height)
            : bounds;

        _overlay.Render(g, localGameRect);

        // Menu
        _menu.Render(g);

        // Blit to screen
        e.Graphics.DrawImage(_backBuf!, 0, 0);
    }

    // ── Mouse forwarding to menu ──────────────────────────────────────────
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _menu.OnMouseMove(e.Location);
        if (e.Button == MouseButtons.Left) _menu.OnMouseDrag(e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _menu.OnMouseDown(e.Location, e.Button);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _menu.OnMouseUp(e.Button);
    }

    // ── Resize ────────────────────────────────────────────────────────────
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeBackBuffer();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _posTimer.Stop();   _posTimer.Dispose();
        _drawTimer.Stop();  _drawTimer.Dispose();
        _keyTimer.Stop();   _keyTimer.Dispose();
        _backGfx?.Dispose();
        _backBuf?.Dispose();
        _menu.Dispose();
        _overlay.Dispose();
        GameMemory.Dispose();
        base.OnFormClosed(e);
    }

    // Prevent the window from showing a task-bar button and keep it from
    // stealing focus via the standard window style flags.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Include WS_EX_TRANSPARENT at creation time so the window is
            // click-through from the very first moment it is displayed.
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            return cp;
        }
    }
}

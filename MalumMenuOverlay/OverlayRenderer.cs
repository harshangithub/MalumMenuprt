namespace MalumMenuOverlay;

/// <summary>
/// Draws ESP information (player info, tracers, minimap dots) onto the
/// transparent overlay surface, reading player data from <see cref="GameMemory"/>.
/// </summary>
public sealed class OverlayRenderer
{
    // ── Fonts / pens / brushes ────────────────────────────────────────────
    private readonly Font _fontInfo = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly Font _fontRole = new("Segoe UI", 8f, FontStyle.Bold);

    private readonly Pen _penCrew = new(Color.FromArgb(200, 0, 200, 255), 1.5f);
    private readonly Pen _penImp  = new(Color.FromArgb(200, 255, 50, 50),  1.5f);
    private readonly Pen _penGhost= new(Color.FromArgb(130, 200, 200, 200), 1f);
    private readonly Pen _penBody = new(Color.FromArgb(180, 200, 160, 0),   1f);
    private readonly Pen _penLocal= new(Color.FromArgb(255, 255, 255, 100), 1f);

    private readonly SolidBrush _brCrewText  = new(Color.FromArgb(220, 100, 220, 255));
    private readonly SolidBrush _brImpText   = new(Color.FromArgb(220, 255, 100, 100));
    private readonly SolidBrush _brGhostText = new(Color.FromArgb(150, 200, 200, 200));
    private readonly SolidBrush _brWhite     = new(Color.FromArgb(220, 255, 255, 255));

    // Minimap constants
    private const int MinimapX = 20;
    private const int MinimapY = 80;
    private const int MinimapW = 140;
    private const int MinimapH = 105;
    private readonly SolidBrush _brMinimapBg = new(Color.FromArgb(160, 10, 10, 20));
    private readonly Pen        _penMinimapBorder = new(Color.FromArgb(180, 80, 60, 140), 1f);

    // ── Render ────────────────────────────────────────────────────────────
    public void Render(Graphics g, Rectangle gameWindow)
    {
        if (gameWindow.IsEmpty) return;

        var players = GameMemory.ReadPlayers();

        // Centre of the game window (approximate player origin in screen space)
        var centre = new Point(gameWindow.Left + gameWindow.Width / 2,
                               gameWindow.Top  + gameWindow.Height / 2);

        DrawTracers(g, players, centre, gameWindow);
        DrawPlayerLabels(g, players, gameWindow);
        DrawMinimap(g, players, gameWindow);
    }

    // ── Tracers ───────────────────────────────────────────────────────────
    private void DrawTracers(Graphics g, List<PlayerSnapshot> players,
                             Point centre, Rectangle gameWindow)
    {
        bool anyTracers = CheatState.tracersCrew   || CheatState.tracersImps ||
                          CheatState.tracersGhosts || CheatState.tracersBodies;
        if (!anyTracers) return;

        const float Scale = 50f;

        foreach (var p in players)
        {
            if (p.IsLocalPlayer) continue;

            bool draw = (!p.IsDead && !p.IsImpostor && CheatState.tracersCrew)
                     || (!p.IsDead &&  p.IsImpostor && CheatState.tracersImps)
                     || (p.IsDead                   && CheatState.tracersGhosts);

            if (!draw) continue;

            var target = p.ToScreen(gameWindow, Scale);

            Pen pen = GetTracerPen(p);

            g.DrawLine(pen, centre, target);

            if (CheatState.distanceBasedTracers)
            {
                float dist = (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
                string distStr = $"{dist:F1}u";
                var mid = new PointF((centre.X + target.X) / 2f,
                                     (centre.Y + target.Y) / 2f);
                g.DrawString(distStr, _fontInfo, _brWhite, mid);
            }
        }
    }

    private Pen GetTracerPen(PlayerSnapshot p)
    {
        if (CheatState.colorBasedTracers)
            return new Pen(p.PlayerColor, 1.5f);

        if (CheatState.teamBasedTracers)
            return p.IsImpostor ? _penImp : _penCrew;

        if (p.IsDead)  return _penGhost;
        if (p.IsImpostor) return _penImp;
        return _penCrew;
    }

    // ── Player labels (ESP) ───────────────────────────────────────────────
    private void DrawPlayerLabels(Graphics g, List<PlayerSnapshot> players, Rectangle gameWindow)
    {
        bool anyLabel = CheatState.showPlayerInfo || CheatState.seeRoles || CheatState.seeGhosts;
        if (!anyLabel) return;

        const float Scale = 50f;

        foreach (var p in players)
        {
            if (p.IsLocalPlayer) continue;
            if (p.IsDead && !CheatState.seeGhosts) continue;

            var pos = p.ToScreen(gameWindow, Scale);

            var textBr = p.IsDead   ? _brGhostText
                       : p.IsImpostor ? _brImpText
                       : _brCrewText;

            int lineY = pos.Y - 36;

            if (CheatState.showPlayerInfo)
            {
                g.DrawString(p.Name, _fontInfo, textBr, pos.X + 4, lineY);
                lineY += 12;
            }

            if (CheatState.seeRoles)
            {
                string roleStr = p.IsImpostor ? "[Imp]" : "[Crew]";
                if (p.IsDead) roleStr = "[Ghost]";
                g.DrawString(roleStr, _fontRole, textBr, pos.X + 4, lineY);
            }
        }
    }

    // ── Minimap ───────────────────────────────────────────────────────────
    private void DrawMinimap(Graphics g, List<PlayerSnapshot> players, Rectangle gameWindow)
    {
        bool anyMap = CheatState.mapCrew || CheatState.mapImps || CheatState.mapGhosts;
        if (!anyMap) return;

        // Background panel
        var mapRect = new Rectangle(gameWindow.Right - MinimapW - 10, gameWindow.Top + 10,
                                    MinimapW, MinimapH);
        g.FillRectangle(_brMinimapBg, mapRect);
        g.DrawRectangle(_penMinimapBorder, mapRect);
        g.DrawString("Minimap", _fontRole, _brWhite, mapRect.X + 4, mapRect.Y + 2);

        float scaleX = MinimapW / 40f;   // world units → pixels (rough)
        float scaleY = MinimapH / 30f;

        foreach (var p in players)
        {
            bool show = (!p.IsDead && !p.IsImpostor && CheatState.mapCrew)
                     || (!p.IsDead &&  p.IsImpostor && CheatState.mapImps)
                     || (p.IsDead                   && CheatState.mapGhosts);
            if (!show) continue;

            int px = mapRect.Left + MinimapW / 2 + (int)(p.X * scaleX);
            int py = mapRect.Top  + MinimapH / 2 - (int)(p.Y * scaleY);

            // Clamp to minimap area
            px = Math.Clamp(px, mapRect.Left + 2, mapRect.Right  - 4);
            py = Math.Clamp(py, mapRect.Top  + 14, mapRect.Bottom - 4);

            Color dotColor = CheatState.colorBasedMap ? p.PlayerColor
                           : p.IsDead       ? Color.Gray
                           : p.IsImpostor   ? Color.Red
                           : Color.Cyan;

            using var dotBr = new SolidBrush(dotColor);
            g.FillEllipse(dotBr, px - 3, py - 3, 6, 6);

            if (CheatState.showPlayerInfo)
                g.DrawString(p.Name, _fontInfo, dotBr, px + 4, py - 6);
        }
    }

    // ── Status overlay (shown when game not running) ──────────────────────
    public void DrawStatusMessage(Graphics g, Rectangle clientBounds)
    {
        using var f = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var br = new SolidBrush(Color.FromArgb(180, 255, 200, 60));
        string msg = ProcessDetector.IsGameRunning
            ? "Among Us detected – overlay active"
            : "Waiting for Among Us…  (press INSERT to open menu)";
        var sz = g.MeasureString(msg, f);
        g.DrawString(msg, f, br,
            (clientBounds.Width - sz.Width) / 2,
            clientBounds.Height - 36);
    }

    public void Dispose()
    {
        _fontInfo.Dispose();  _fontRole.Dispose();
        _penCrew.Dispose();   _penImp.Dispose();
        _penGhost.Dispose();  _penBody.Dispose(); _penLocal.Dispose();
        _brCrewText.Dispose(); _brImpText.Dispose();
        _brGhostText.Dispose(); _brWhite.Dispose();
        _brMinimapBg.Dispose(); _penMinimapBorder.Dispose();
    }
}

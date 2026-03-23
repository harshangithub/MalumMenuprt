namespace MalumMenuOverlay;

/// <summary>
/// Defines one toggle entry shown inside the cheat menu.
/// </summary>
public sealed class MenuToggle
{
    public string Label { get; }
    public string ToggleName { get; }
    public MenuToggle(string label, string toggleName) { Label = label; ToggleName = toggleName; }
}

/// <summary>
/// A named section (submenu) of toggle items.
/// </summary>
public sealed class MenuSection
{
    public string Title { get; }
    public IReadOnlyList<MenuToggle> Toggles { get; }
    public MenuSection(string title, IEnumerable<MenuToggle> toggles)
    {
        Title = title;
        Toggles = toggles.ToList();
    }
}

/// <summary>
/// A top-level tab in the cheat menu.
/// </summary>
public sealed class MenuTab
{
    public string Name { get; }
    public IReadOnlyList<MenuToggle> Toggles { get; }
    public IReadOnlyList<MenuSection> Sections { get; }
    public MenuTab(string name, IEnumerable<MenuToggle> toggles, IEnumerable<MenuSection> sections)
    {
        Name = name;
        Toggles = toggles.ToList();
        Sections = sections.ToList();
    }
}

/// <summary>
/// Renders the interactive cheat menu onto a <see cref="Graphics"/> surface.
/// Handles mouse-click hit testing for toggle buttons.
/// </summary>
public sealed class MenuRenderer
{
    // ── Layout constants ──────────────────────────────────────────────────
    private const int WindowX = 10;
    private const int WindowY = 10;
    private const int WindowW = 680;
    private const int WindowH = 540;
    private const int TabBarW = 110;
    private const int TabH = 34;
    private const int ItemH = 24;
    private const int Pad = 8;

    // ── Fonts / brushes ───────────────────────────────────────────────────
    private readonly Font _fontTitle = new("Segoe UI", 13f, FontStyle.Bold);
    private readonly Font _fontSection = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _fontItem = new("Segoe UI", 9.5f, FontStyle.Regular);
    private readonly Font _fontTab = new("Segoe UI", 9f, FontStyle.Bold);

    private readonly SolidBrush _brBg = new(Color.FromArgb(220, 18, 18, 28));
    private readonly SolidBrush _brTabBar = new(Color.FromArgb(220, 12, 12, 22));
    private readonly SolidBrush _brTabHover = new(Color.FromArgb(180, 40, 40, 70));
    private readonly SolidBrush _brTabActive = new(Color.FromArgb(220, 80, 50, 160));
    private readonly SolidBrush _brSectionBg = new(Color.FromArgb(120, 30, 30, 50));
    private readonly SolidBrush _brOn = new(Color.FromArgb(255, 90, 200, 90));
    private readonly SolidBrush _brOff = new(Color.FromArgb(255, 55, 55, 80));
    private readonly SolidBrush _brText = new(Color.FromArgb(255, 230, 230, 255));
    private readonly SolidBrush _brTitle = new(Color.FromArgb(255, 170, 120, 255));
    private readonly SolidBrush _brSection = new(Color.FromArgb(255, 140, 170, 255));
    private readonly Pen _penBorder = new(Color.FromArgb(180, 90, 60, 180), 1f);
    private readonly Pen _penSep = new(Color.FromArgb(100, 90, 90, 120), 1f);

    // ── State ─────────────────────────────────────────────────────────────
    public bool IsVisible { get; set; } = false;
    private int _selectedTab = 0;
    private Point _mousePos;
    private bool _dragging;
    private Point _dragOffset;
    private Point _windowPos = new(WindowX, WindowY);

    // Maps screen rectangles → toggle names (rebuilt each frame)
    private readonly List<(Rectangle Rect, string ToggleName)> _hitBoxes = new();
    private readonly List<(Rectangle Rect, int TabIndex)> _tabBoxes = new();

    // RGB cycling hue
    private float _hue;

    // ── Public menu tab list ───────────────────────────────────────────────
    public readonly List<MenuTab> Tabs = new();

    public MenuRenderer() { BuildTabs(); }

    // ── Build tab/toggle structure (mirrors original MenuUI) ──────────────
    private static MenuToggle T(string label, string name) => new(label, name);
    private static MenuSection S(string title, params MenuToggle[] ts) => new(title, ts);
    private static MenuTab Tab(string name, MenuToggle[] toggles, params MenuSection[] sections)
        => new(name, toggles, sections);

    private void BuildTabs()
    {
        Tabs.Add(Tab("Player",
            new[] {
                T("NoClip",           "noClip"),
                T("Fake Revive",      "fakeRevive"),
                T("Invert Controls",  "invertControls"),
                T("Moon Walk",        "moonWalk"),
                T("Speed Boost",      "speedBoost"),
            },
            S("Teleport",
                T("Teleport to Cursor", "teleportCursor"),
                T("Teleport to Player", "teleportPlayer")),
            S("Kill / Eject",
                T("Kill Player",      "killPlayer"),
                T("Tele-Kill Player", "telekillPlayer"),
                T("Kill All",         "killAll"),
                T("Kill All Crew",    "killAllCrew"),
                T("Kill All Imps",    "killAllImps"),
                T("Eject Player",     "ejectPlayer"),
                T("Report Body",      "reportBody"))
        ));

        Tabs.Add(Tab("ESP",
            new[] {
                T("Show Player Info", "showPlayerInfo"),
                T("See Roles",        "seeRoles"),
                T("See Ghosts",       "seeGhosts"),
                T("No Shadows",       "fullBright"),
                T("Task Arrows",      "taskArrows"),
                T("Reveal Votes",     "revealVotes"),
                T("Show Lobby Info",  "showLobbyInfo"),
                T("See Disguises",    "seeDisguises"),
            },
            S("Camera",
                T("Zoom Out",  "zoomOut"),
                T("Spectate",  "spectate"),
                T("Freecam",   "freecam")),
            S("Tracers",
                T("Crewmates",       "tracersCrew"),
                T("Impostors",       "tracersImps"),
                T("Ghosts",          "tracersGhosts"),
                T("Dead Bodies",     "tracersBodies"),
                T("Color-based",     "colorBasedTracers"),
                T("Distance-based",  "distanceBasedTracers"),
                T("Team-based",      "teamBasedTracers")),
            S("Minimap",
                T("Show Crew",       "mapCrew"),
                T("Show Impostors",  "mapImps"),
                T("Show Ghosts",     "mapGhosts"),
                T("Color-based",     "colorBasedMap"))
        ));

        Tabs.Add(Tab("Roles",
            new[] {
                T("Set Fake Role",      "changeRole"),
                T("Force Role (Host)",  "forceRole"),
            },
            S("Impostor",
                T("Kill Reach",       "killReach"),
                T("Zero Kill CD",     "zeroKillCd"),
                T("Kill Anyone",      "killAnyone")),
            S("Shapeshifter",
                T("No Ss Animation",     "noShapeshiftAnim"),
                T("Endless Ss Duration", "endlessSsDuration")),
            S("Crewmate",
                T("Show Tasks Menu",     "showTasksMenu"),
                T("Complete My Tasks",   "completeMyTasks")),
            S("Other",
                T("No Vent Cooldown",    "noVentCooldown"),
                T("Endless Vent Time",   "endlessVentTime"))
        ));

        Tabs.Add(Tab("Doors",
            new[] {
                T("Open All Doors",       "openAllDoors"),
                T("Close All Doors",      "closeAllDoors"),
                T("Spam Open Doors",      "spamOpenAllDoors"),
                T("Spam Close Doors",     "spamCloseAllDoors"),
                T("Auto-Open on Use",     "autoOpenDoorsOnUse"),
            }
        ));

        Tabs.Add(Tab("Sabotage",
            new[] {
                T("Reactor",        "reactorSab"),
                T("Oxygen",         "oxygenSab"),
                T("Comms",          "commsSab"),
                T("Lights (Elec)",  "elecSab"),
                T("Unfixable Lights","unfixableLights"),
                T("Mushroom Sab",   "mushSab"),
                T("Mushroom Spore", "mushSpore"),
            }
        ));

        Tabs.Add(Tab("Meeting",
            new[] {
                T("Call Meeting",   "callMeeting"),
                T("Skip Meeting",   "skipMeeting"),
                T("Close Meeting",  "closeMeeting"),
                T("Vote Immune",    "voteImmune"),
            }
        ));

        Tabs.Add(Tab("Host",
            new[] {
                T("Force Start Game", "forceStartGame"),
                T("No Game End",      "noGameEnd"),
                T("No Options Limits","noOptionsLimits"),
            },
            S("Protect",
                T("Show Protect Menu","showProtectMenu")),
            S("Roles",
                T("Show Roles Menu",  "showRolesMenu"))
        ));

        Tabs.Add(Tab("Chat",
            new[] {
                T("Always Chat",       "alwaysChat"),
                T("Unlock Characters", "unlockCharacters"),
                T("Bypass URL Block",  "bypassUrlBlock"),
                T("Longer Messages",   "longerMessages"),
                T("Unlock Clipboard",  "unlockClipboard"),
                T("Lower Rate Limits", "lowerRateLimits"),
            }
        ));

        Tabs.Add(Tab("Vents",
            new[] {
                T("Use Vents",  "useVents"),
                T("Walk Vent",  "walkVent"),
                T("Kick Vents", "kickVents"),
            }
        ));

        Tabs.Add(Tab("Passive",
            new[] {
                T("Unlock Features",         "unlockFeatures"),
                T("Free Cosmetics",          "freeCosmetics"),
                T("Avoid Bans",              "avoidBans"),
                T("Copy Lobby Code",         "copyLobbyCodeOnDisconnect"),
                T("Stealth Mode",            "stealthMode"),
            }
        ));

        Tabs.Add(Tab("Animations",
            new[] {
                T("Shields Anim",      "animShields"),
                T("Asteroids Anim",    "animAsteroids"),
                T("Empty Garbage Anim","animEmptyGarbage"),
                T("Scan Anim",         "animScan"),
                T("Cams In Use Anim",  "animCamsInUse"),
                T("Pet Anim",          "animPet"),
            }
        ));

        Tabs.Add(Tab("Config",
            new[] {
                T("RGB Mode", "rgbMode"),
            }
        ));
    }

    // ── Mouse event forwarding (called from OverlayForm) ──────────────────
    public void OnMouseMove(Point screenPos) => _mousePos = screenPos;

    public void OnMouseDown(Point screenPos, MouseButtons btn)
    {
        if (!IsVisible) return;

        // Tab click
        foreach (var (rect, idx) in _tabBoxes)
        {
            if (rect.Contains(screenPos)) { _selectedTab = idx; return; }
        }

        // Toggle click
        foreach (var (rect, name) in _hitBoxes)
        {
            if (rect.Contains(screenPos)) { CheatState.Toggle(name); return; }
        }

        // Drag start: title bar
        var titleBar = new Rectangle(_windowPos.X, _windowPos.Y, WindowW, 24);
        if (btn == MouseButtons.Left && titleBar.Contains(screenPos))
        {
            _dragging = true;
            _dragOffset = new Point(screenPos.X - _windowPos.X, screenPos.Y - _windowPos.Y);
        }
    }

    public void OnMouseUp(MouseButtons _) => _dragging = false;

    public void OnMouseDrag(Point screenPos)
    {
        if (_dragging)
            _windowPos = new Point(screenPos.X - _dragOffset.X, screenPos.Y - _dragOffset.Y);
    }

    // ── Render ────────────────────────────────────────────────────────────
    public void Render(Graphics g)
    {
        if (!IsVisible) return;

        _hitBoxes.Clear();
        _tabBoxes.Clear();

        if (CheatState.rgbMode)
        {
            _hue = (_hue + 0.5f) % 360f;
            UpdateRgbColor(_hue);
        }

        int x = _windowPos.X, y = _windowPos.Y;

        // ── Window background ──────────────────────────────────────────────
        var wndRect = new Rectangle(x, y, WindowW, WindowH);
        g.FillRectangle(_brBg, wndRect);
        g.DrawRectangle(_penBorder, wndRect);

        // ── Title bar ──────────────────────────────────────────────────────
        var titleRect = new Rectangle(x, y, WindowW, 24);
        g.FillRectangle(_brTabActive, titleRect);
        g.DrawString("MalumMenu Overlay  v1.0  [INSERT to hide]",
            _fontSection, _brText, x + Pad, y + 4);

        y += 24;

        // ── Tab sidebar ───────────────────────────────────────────────────
        int tabY = y;
        g.FillRectangle(_brTabBar, new Rectangle(x, tabY, TabBarW, WindowH - 24));

        for (int i = 0; i < Tabs.Count; i++)
        {
            var tabRect = new Rectangle(x + 2, tabY + i * TabH + 2, TabBarW - 4, TabH - 4);
            _tabBoxes.Add((tabRect, i));

            var tabBr = i == _selectedTab ? _brTabActive
                      : tabRect.Contains(_mousePos) ? _brTabHover
                      : _brTabBar;

            g.FillRectangle(tabBr, tabRect);
            g.DrawRectangle(_penSep, tabRect);
            g.DrawString(Tabs[i].Name, _fontTab, _brText,
                tabRect.X + 4, tabRect.Y + (TabH - 14) / 2);
        }

        // ── Content area ─────────────────────────────────────────────────
        int cx = x + TabBarW + Pad;
        int cy = y + Pad;
        int contentW = WindowW - TabBarW - Pad * 2;

        if (_selectedTab >= 0 && _selectedTab < Tabs.Count)
        {
            var tab = Tabs[_selectedTab];
            g.DrawString(tab.Name, _fontTitle, _brTitle, cx, cy);
            cy += 28;

            // Draw direct toggles in two columns
            cy = DrawToggleList(g, tab.Toggles, cx, cy, contentW, out int rightColX, out int rightColY);

            // Draw sections
            foreach (var section in tab.Sections)
            {
                if (cy > y + WindowH - 30) break;
                cy = DrawSection(g, section, cx, cy, contentW / 2);
            }
        }

        // ── Status bar ────────────────────────────────────────────────────
        int statusY = wndRect.Bottom - 22;
        g.FillRectangle(_brTabBar, new Rectangle(wndRect.X, statusY, WindowW, 22));
        string status = ProcessDetector.IsGameRunning ? "● Among Us detected" : "○ Among Us not found";
        var statusColor = ProcessDetector.IsGameRunning ? _brOn : _brOff;
        g.DrawString(status, _fontItem, statusColor, x + Pad, statusY + 4);
        g.DrawString("INSERT = toggle menu  |  DEL = disable all",
            _fontItem, _brText, x + 200, statusY + 4);
    }

    private int DrawSection(Graphics g, MenuSection section, int x, int y, int w)
    {
        if (y > _windowPos.Y + WindowH - 40) return y;

        var bgRect = new Rectangle(x - 2, y, w + 4, ItemH + section.Toggles.Count * ItemH + 6);
        g.FillRectangle(_brSectionBg, bgRect);
        g.DrawRectangle(_penSep, bgRect);

        g.DrawString(section.Title, _fontSection, _brSection, x + 2, y + 3);
        y += ItemH + 2;

        foreach (var t in section.Toggles)
        {
            DrawToggleRow(g, t, x + 4, y, w - 8);
            y += ItemH;
        }
        return y + 6;
    }

    private int DrawToggleList(Graphics g, IReadOnlyList<MenuToggle> toggles,
        int x, int y, int w, out int rightX, out int rightY)
    {
        rightX = x + w / 2 + Pad;
        rightY = y;

        int half = (toggles.Count + 1) / 2;
        int leftY = y, rY = y;

        for (int i = 0; i < toggles.Count; i++)
        {
            if (i < half)
            {
                DrawToggleRow(g, toggles[i], x, leftY, w / 2 - Pad);
                leftY += ItemH;
            }
            else
            {
                DrawToggleRow(g, toggles[i], rightX, rY, w / 2 - Pad);
                rY += ItemH;
            }
        }
        rightY = rY;
        return Math.Max(leftY, rY) + 4;
    }

    private void DrawToggleRow(Graphics g, MenuToggle toggle, int x, int y, int w)
    {
        bool on = CheatState.Get(toggle.ToggleName);
        var indicator = new Rectangle(x, y + 4, 14, 14);
        var hitRect = new Rectangle(x, y, w, ItemH);

        _hitBoxes.Add((hitRect, toggle.ToggleName));

        g.FillRectangle(on ? _brOn : _brOff, indicator);
        g.DrawRectangle(_penSep, indicator);
        g.DrawString(toggle.Label, _fontItem, _brText, x + 18, y + 3);
    }

    private void UpdateRgbColor(float hue)
    {
        var c = HsvToColor(hue, 0.8f, 1f);
        _brTabActive.Color = Color.FromArgb(220, c.R, c.G, c.B);
        _penBorder.Color = Color.FromArgb(180, c.R, c.G, c.B);
    }

    private static Color HsvToColor(float h, float s, float v)
    {
        int hi = (int)(h / 60f) % 6;
        float f = h / 60f - (int)(h / 60f);
        int p = (int)(255 * v * (1 - s));
        int q = (int)(255 * v * (1 - f * s));
        int t = (int)(255 * v * (1 - (1 - f) * s));
        int vi = (int)(255 * v);
        return hi switch
        {
            0 => Color.FromArgb(vi, t, p),
            1 => Color.FromArgb(q, vi, p),
            2 => Color.FromArgb(p, vi, t),
            3 => Color.FromArgb(p, q, vi),
            4 => Color.FromArgb(t, p, vi),
            _ => Color.FromArgb(vi, p, q)
        };
    }

    public void Dispose()
    {
        _fontTitle.Dispose(); _fontSection.Dispose();
        _fontItem.Dispose();  _fontTab.Dispose();
        _brBg.Dispose(); _brTabBar.Dispose(); _brTabHover.Dispose();
        _brTabActive.Dispose(); _brSectionBg.Dispose();
        _brOn.Dispose(); _brOff.Dispose(); _brText.Dispose();
        _brTitle.Dispose(); _brSection.Dispose();
        _penBorder.Dispose(); _penSep.Dispose();
    }
}

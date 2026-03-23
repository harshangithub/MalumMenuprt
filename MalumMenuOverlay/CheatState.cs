using System.Reflection;

namespace MalumMenuOverlay;

/// <summary>
/// Centralised store for every cheat-toggle flag.
/// Mirrors the toggle set from the original BepInEx MalumMenu plugin.
/// </summary>
public static class CheatState
{
    // ── Player ─────────────────────────────────────────────────────────────
    public static bool noClip;
    public static bool speedBoost;
    public static bool teleportCursor;
    public static bool teleportPlayer;
    public static bool reportBody;
    public static bool ejectPlayer;
    public static bool killPlayer;
    public static bool telekillPlayer;
    public static bool killAll;
    public static bool killAllCrew;
    public static bool killAllImps;
    public static bool fakeRevive;
    public static bool invertControls;
    public static bool moonWalk;

    // ── Roles ──────────────────────────────────────────────────────────────
    public static bool changeRole;
    public static bool zeroKillCd;
    public static bool showTasksMenu;
    public static bool completeMyTasks;
    public static bool impostorTasks;
    public static bool killReach;
    public static bool killAnyone;
    public static bool endlessSsDuration;
    public static bool endlessBattery;
    public static bool endlessTracking;
    public static bool noTrackingCooldown;
    public static bool noTrackingDelay;
    public static bool trackReach;
    public static bool interrogateReach;
    public static bool noVitalsCooldown;
    public static bool noVentCooldown;
    public static bool endlessVentTime;
    public static bool endlessVanish;
    public static bool killVanished;
    public static bool noVanishAnim;
    public static bool noShapeshiftAnim;

    // ── ESP ────────────────────────────────────────────────────────────────
    public static bool fullBright;
    public static bool seeGhosts;
    public static bool seeRoles;
    public static bool showPlayerInfo;
    public static bool seeDisguises;
    public static bool taskArrows;
    public static bool revealVotes;
    public static bool showLobbyInfo;

    // ── Camera ─────────────────────────────────────────────────────────────
    public static bool spectate;
    public static bool zoomOut;
    public static bool freecam;

    // ── Minimap ────────────────────────────────────────────────────────────
    public static bool mapCrew;
    public static bool mapImps;
    public static bool mapGhosts;
    public static bool colorBasedMap;

    // ── Tracers ────────────────────────────────────────────────────────────
    public static bool tracersImps;
    public static bool tracersCrew;
    public static bool tracersGhosts;
    public static bool tracersBodies;
    public static bool colorBasedTracers;
    public static bool distanceBasedTracers;
    public static bool teamBasedTracers;

    // ── Chat ───────────────────────────────────────────────────────────────
    public static bool alwaysChat;
    public static bool unlockCharacters;
    public static bool bypassUrlBlock;
    public static bool longerMessages;
    public static bool unlockClipboard;
    public static bool lowerRateLimits;

    // ── Ship / Doors ───────────────────────────────────────────────────────
    public static bool closeMeeting;
    public static bool sabotageMap;
    public static bool openAllDoors;
    public static bool closeAllDoors;
    public static bool spamOpenAllDoors;
    public static bool spamCloseAllDoors;
    public static bool autoOpenDoorsOnUse;
    public static bool unfixableLights;
    public static bool commsSab;
    public static bool elecSab;
    public static bool reactorSab;
    public static bool oxygenSab;
    public static bool mushSab;
    public static bool mushSpore;
    public static bool showDoorsMenu;

    // ── Vents ──────────────────────────────────────────────────────────────
    public static bool useVents;
    public static bool walkVent;
    public static bool kickVents;

    // ── Host-Only ──────────────────────────────────────────────────────────
    public static bool voteImmune;
    public static bool forceRole;
    public static bool showRolesMenu;
    public static bool skipMeeting;
    public static bool callMeeting;
    public static bool forceStartGame;
    public static bool noGameEnd;
    public static bool showProtectMenu;
    public static bool noOptionsLimits;

    // ── Passive ────────────────────────────────────────────────────────────
    public static bool unlockFeatures;
    public static bool freeCosmetics;
    public static bool avoidBans;
    public static bool copyLobbyCodeOnDisconnect;
    public static bool stealthMode;

    // ── Animations ─────────────────────────────────────────────────────────
    public static bool animShields;
    public static bool animAsteroids;
    public static bool animEmptyGarbage;
    public static bool animScan;
    public static bool animCamsInUse;
    public static bool animPet;

    // ── Config ─────────────────────────────────────────────────────────────
    public static bool rgbMode;

    // ── Keybind map: toggle name → Windows.Forms.Keys ──────────────────────
    public static readonly Dictionary<string, Keys> Keybinds = new();

    // ── Reflection map used by profile save/load ───────────────────────────
    private static readonly Dictionary<string, FieldInfo> _fields = new();

    static CheatState()
    {
        foreach (var f in typeof(CheatState).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            if (f.FieldType != typeof(bool)) continue;
            _fields[f.Name] = f;
            Keybinds[f.Name] = Keys.None;
        }
    }

    public static IEnumerable<string> ToggleNames => _fields.Keys;

    public static bool Get(string name) =>
        _fields.TryGetValue(name, out var f) && (bool)f.GetValue(null)!;

    public static void Set(string name, bool value)
    {
        if (_fields.TryGetValue(name, out var f))
        {
            f.SetValue(null, value);
            WriteLiveState();
        }
    }

    public static void Toggle(string name) => Set(name, !Get(name));

    public static void DisableAll()
    {
        foreach (var f in _fields.Values)
            f.SetValue(null, false);
        WriteLiveState();
    }

    // ── Live-state IPC ────────────────────────────────────────────────────
    // Written on every toggle change so the BepInEx plugin running inside
    // Among Us can pick it up and apply the cheats without any injection.
    internal static readonly string LiveStatePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MalumMenu", "state.txt");

    private static void WriteLiveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(LiveStatePath);
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            // Write to a temp file then replace atomically to avoid partial reads.
            var tmp = LiveStatePath + ".tmp";
            using (var w = new StreamWriter(tmp))
                foreach (var f in _fields.Values)
                    w.WriteLine($"{f.Name} = {f.GetValue(null)}");
            File.Move(tmp, LiveStatePath, overwrite: true);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MalumMenu] Failed to write live state: {ex.Message}"); }
    }

    // ── Profile persistence ────────────────────────────────────────────────
    private static readonly string ProfilePath =
        Path.Combine(AppContext.BaseDirectory, "MalumProfile.txt");

    public static void SaveProfile()
    {
        using var w = new StreamWriter(ProfilePath);
        w.WriteLine("# MalumMenu Overlay Profile");
        w.WriteLine("# Format: ToggleName = True/False = KEY");
        foreach (var f in _fields.Values)
        {
            Keybinds.TryGetValue(f.Name, out var key);
            w.WriteLine($"{f.Name} = {f.GetValue(null)} = {key}");
        }
    }

    public static void LoadProfile()
    {
        if (!File.Exists(ProfilePath)) return;
        foreach (var line in File.ReadLines(ProfilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            var parts = trimmed.Split('=', 3);
            if (parts.Length < 2) continue;

            var name = parts[0].Trim();
            if (!_fields.TryGetValue(name, out var field)) continue;

            if (bool.TryParse(parts[1].Trim(), out var val))
                field.SetValue(null, val);

            if (parts.Length >= 3 && Enum.TryParse<Keys>(parts[2].Trim(), true, out var key))
                Keybinds[name] = key;
        }
    }
}

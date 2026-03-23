using System.Runtime.InteropServices;
using System.Text;

namespace MalumMenuOverlay;

/// <summary>
/// Low-level helpers for reading the Among Us process memory.
/// All reads are best-effort; failures return default values silently.
///
/// NOTE: Memory addresses and offsets listed here are illustrative starting
/// points and must be updated to match the actual Among Us build you target.
/// Use a tool such as Cheat Engine or dnSpy to find the correct offsets.
/// </summary>
public static class GameMemory
{
    // ── Win32 memory-reading API ───────────────────────────────────────────
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    // ── Cached process handle ─────────────────────────────────────────────
    private static IntPtr _processHandle = IntPtr.Zero;
    private static int _cachedPid = -1;

    private static bool EnsureHandle()
    {
        var proc = ProcessDetector.GameProcess;
        if (proc == null) { CloseHandleIfOpen(); return false; }

        if (proc.Id == _cachedPid && _processHandle != IntPtr.Zero)
            return true;

        CloseHandleIfOpen();
        _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, proc.Id);
        _cachedPid = _processHandle != IntPtr.Zero ? proc.Id : -1;
        return _processHandle != IntPtr.Zero;
    }

    private static void CloseHandleIfOpen()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
            _cachedPid = -1;
        }
    }

    // ── Generic read helpers ───────────────────────────────────────────────
    public static byte[] ReadBytes(IntPtr address, int count)
    {
        var buf = new byte[count];
        if (!EnsureHandle()) return buf;
        ReadProcessMemory(_processHandle, address, buf, count, out _);
        return buf;
    }

    public static int ReadInt32(IntPtr address)
    {
        var b = ReadBytes(address, 4);
        return BitConverter.ToInt32(b, 0);
    }

    public static long ReadInt64(IntPtr address)
    {
        var b = ReadBytes(address, 8);
        return BitConverter.ToInt64(b, 0);
    }

    public static float ReadFloat(IntPtr address)
    {
        var b = ReadBytes(address, 4);
        return BitConverter.ToSingle(b, 0);
    }

    public static IntPtr ReadPointer(IntPtr address)
    {
        var b = ReadBytes(address, IntPtr.Size);
        return IntPtr.Size == 8
            ? new IntPtr(BitConverter.ToInt64(b, 0))
            : new IntPtr(BitConverter.ToInt32(b, 0));
    }

    /// <summary>
    /// Follow a multi-level pointer chain starting from a module base.
    /// </summary>
    public static IntPtr ResolvePointer(IntPtr moduleBase, params int[] offsets)
    {
        var addr = moduleBase;
        foreach (var off in offsets)
        {
            addr = ReadPointer(addr);
            if (addr == IntPtr.Zero) return IntPtr.Zero;
            addr = IntPtr.Add(addr, off);
        }
        return addr;
    }

    /// <summary>Read a UTF-16 IL2Cpp String object (header = 4-byte length, then chars).</summary>
    public static string ReadIl2CppString(IntPtr strObjectAddr)
    {
        if (strObjectAddr == IntPtr.Zero) return string.Empty;
        try
        {
            // Il2Cpp string: [object header 16 bytes][int32 length][chars…]
            var len = ReadInt32(IntPtr.Add(strObjectAddr, 16));
            if (len <= 0 || len > 512) return string.Empty;
            var charBytes = ReadBytes(IntPtr.Add(strObjectAddr, 20), len * 2);
            return Encoding.Unicode.GetString(charBytes);
        }
        catch { return string.Empty; }
    }

    // ── Among Us specific reads ────────────────────────────────────────────
    // These offsets target the IL2Cpp mono runtime used by Among Us (Steam build).
    // They will need to be recalculated when the game updates.

    /// <summary>
    /// Attempts to retrieve a list of player infos from the game's
    /// GameData.Instance.AllPlayers Il2CppList.
    /// Returns an empty list if the game is not running or the data
    /// cannot be located.
    /// </summary>
    public static List<PlayerSnapshot> ReadPlayers()
    {
        var result = new List<PlayerSnapshot>();
        // Real implementation requires locating GameData static instance via
        // mono_get_root_domain / il2cpp_domain_get_assemblies / reflection calls,
        // or by scanning for known byte signatures.
        // This stub returns an empty list so the overlay compiles and runs;
        // replace with actual offset resolution for the build you target.
        return result;
    }

    // ── Shutdown ──────────────────────────────────────────────────────────
    public static void Dispose() => CloseHandleIfOpen();
}

/// <summary>
/// Lightweight snapshot of a single player read from game memory.
/// </summary>
public sealed class PlayerSnapshot
{
    public byte PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public bool IsDead { get; init; }
    public bool IsImpostor { get; init; }
    public bool IsLocalPlayer { get; init; }
    public System.Drawing.Color PlayerColor { get; init; } = System.Drawing.Color.White;

    /// <summary>Map-space coordinates converted to a screen point via the supplied transform.</summary>
    public Point ToScreen(Rectangle gameWindow, float scale = 50f, float offsetX = 0, float offsetY = 0)
    {
        int sx = gameWindow.Left + gameWindow.Width / 2 + (int)(X * scale + offsetX);
        int sy = gameWindow.Top + gameWindow.Height / 2 - (int)(Y * scale + offsetY);
        return new Point(sx, sy);
    }
}

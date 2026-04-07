using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinSpotlight;

public sealed class HotkeyManager : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── Modifier constants ────────────────────────────────────────────────────
    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_SHIFT    = 0x0004;
    public const uint MOD_WIN      = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly int _id;
    private IntPtr       _hwnd;
    private HwndSource?  _source;
    private bool         _disposed;

    public event Action? HotkeyPressed;

    public HotkeyManager(int id = 9001) => _id = id;

    /// <summary>Register the hotkey on <paramref name="window"/>.</summary>
    /// <param name="modifiers">Combination of MOD_* constants.</param>
    /// <param name="vk">Virtual-key code (e.g. 0x20 = VK_SPACE).</param>
    public bool Register(Window window, uint modifiers, uint vk)
    {
        _hwnd   = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        return RegisterHotKey(_hwnd, _id, modifiers | MOD_NOREPEAT, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterHotKey(_hwnd, _id);
        _source?.RemoveHook(WndProc);
    }
}

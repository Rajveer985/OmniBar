using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinSpotlight;

public sealed class ClipboardManager : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    // ── State ─────────────────────────────────────────────────────────────────
    private IntPtr      _hwnd;
    private HwndSource? _source;
    private bool        _disposed;
    private readonly int _maxItems;

    /// <summary>Most-recent entries first.</summary>
    public LinkedList<string> History { get; } = new();

    public ClipboardManager(Window window, int maxItems = 25)
    {
        _maxItems = maxItems;
        _hwnd     = new WindowInteropHelper(window).EnsureHandle();
        _source   = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        AddClipboardFormatListener(_hwnd);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
            TryCapture();
        return IntPtr.Zero;
    }

    private void TryCapture()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText()) return;
            var text = System.Windows.Clipboard.GetText().Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (History.Count > 0 && History.First!.Value == text) return; // no duplicate

            History.AddFirst(text);
            while (History.Count > _maxItems)
                History.RemoveLast();
        }
        catch { /* clipboard locked by another process */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RemoveClipboardFormatListener(_hwnd);
        _source?.RemoveHook(WndProc);
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using DoraSaver.Core;
using static DoraSaver.Native.NativeMethods;

namespace DoraSaver.Native;

/// <summary>
/// Watches global keyboard/mouse input with low-level hooks (the WebView2 child window lives
/// in another process, so our own forms never see its input) and raises <see cref="Wake"/> once.
/// A cursor-polling timer backs up the mouse hook in case Windows drops it.
/// </summary>
internal sealed class InputMonitor : IDisposable
{
    private readonly LowLevelHookProc _keyboardProc;
    private readonly LowLevelHookProc _mouseProc;
    private readonly WakePolicy _policy;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _pollTimer;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _woken;

    public InputMonitor()
    {
        Point? origin = GetCursorPos(out POINT start) ? new Point(start.X, start.Y) : null;
        _policy = new WakePolicy(origin, WakePolicy.DefaultMoveThresholdPixels, WakePolicy.DefaultMoveGracePeriod);

        // Delegates are kept in fields so the GC cannot collect them while the hooks are live.
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
        IntPtr module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            Log.Error($"Keyboard hook failed (Win32 error {Marshal.GetLastWin32Error()})");
        }

        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            Log.Error($"Mouse hook failed (Win32 error {Marshal.GetLastWin32Error()})");
        }

        _pollTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _pollTimer.Tick += (_, _) => PollCursor();
        _pollTimer.Start();
    }

    public event EventHandler? Wake;

    public void Dispose()
    {
        _pollTimer.Dispose();
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Only key-down: the key-up of the Enter/Space that pressed "Preview" arrives after we start.
        if (nCode >= 0 && ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
        {
            RaiseWake("key");
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = (int)wParam;
            if (message == WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                if (_policy.ShouldWakeOnMove(new Point(data.pt.X, data.pt.Y), _clock.Elapsed))
                {
                    RaiseWake("mouse move");
                }
            }
            else if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN
                     or WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
            {
                RaiseWake("mouse button");
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void PollCursor()
    {
        if (GetCursorPos(out POINT p) && _policy.ShouldWakeOnMove(new Point(p.X, p.Y), _clock.Elapsed))
        {
            RaiseWake("cursor poll");
        }
    }

    /// <summary>Wake from a source other than the hooks (form key events, the player page).</summary>
    public void RequestWake(string reason) => RaiseWake(reason);

    private void RaiseWake(string reason)
    {
        if (_woken)
        {
            return;
        }

        _woken = true;
        Log.Info($"Waking on {reason}");
        Wake?.Invoke(this, EventArgs.Empty);
    }
}

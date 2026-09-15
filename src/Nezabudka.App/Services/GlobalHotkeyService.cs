using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Nezabudka.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ToggleWindowHotkeyId = 0x4E01;
    private const int NewNoteHotkeyId = 0x4E02;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;
    private nint _windowHandle;
    private bool _registered;

    public event Action? ToggleWindowRequested;

    public event Action? NewNoteRequested;

    public void Attach(Window window)
    {
        if (_source is not null)
        {
            return;
        }

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_windowHandle)
                  ?? throw new InvalidOperationException("Cannot attach the global hotkey service.");
        _source.AddHook(WindowHook);
    }

    public bool SetEnabled(bool enabled)
    {
        Unregister();
        if (!enabled)
        {
            return true;
        }

        if (_windowHandle == nint.Zero)
        {
            return false;
        }

        var modifiers = ModControl | ModShift | ModNoRepeat;
        var toggleRegistered = RegisterHotKey(
            _windowHandle,
            ToggleWindowHotkeyId,
            modifiers,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space));
        var noteRegistered = RegisterHotKey(
            _windowHandle,
            NewNoteHotkeyId,
            modifiers,
            (uint)KeyInterop.VirtualKeyFromKey(Key.N));

        if (!toggleRegistered || !noteRegistered)
        {
            if (toggleRegistered)
            {
                UnregisterHotKey(_windowHandle, ToggleWindowHotkeyId);
            }

            if (noteRegistered)
            {
                UnregisterHotKey(_windowHandle, NewNoteHotkeyId);
            }

            return false;
        }

        _registered = true;
        return true;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WindowHook);
        _source = null;
        _windowHandle = nint.Zero;
    }

    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmHotkey)
        {
            return nint.Zero;
        }

        switch (wParam.ToInt32())
        {
            case ToggleWindowHotkeyId:
                ToggleWindowRequested?.Invoke();
                handled = true;
                break;
            case NewNoteHotkeyId:
                NewNoteRequested?.Invoke();
                handled = true;
                break;
        }

        return nint.Zero;
    }

    private void Unregister()
    {
        if (!_registered || _windowHandle == nint.Zero)
        {
            return;
        }

        UnregisterHotKey(_windowHandle, ToggleWindowHotkeyId);
        UnregisterHotKey(_windowHandle, NewNoteHotkeyId);
        _registered = false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenClipRecorder.Services;

public sealed class HotKeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002, ModShift = 0x0004;
    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    public event Action<int>? Pressed;

    public HotKeyService(Window window)
    {
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle) ?? throw new InvalidOperationException("창 핸들을 만들 수 없습니다.");
        _source.AddHook(WndProc);
        RegisterHotKey(_handle, 1, ModControl | ModShift, 0x52);
        RegisterHotKey(_handle, 2, ModControl | ModShift, 0x50);
        RegisterHotKey(_handle, 3, ModControl | ModShift, 0x53);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey) { handled = true; Pressed?.Invoke(wParam.ToInt32()); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        for (var id = 1; id <= 3; id++) UnregisterHotKey(_handle, id);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}

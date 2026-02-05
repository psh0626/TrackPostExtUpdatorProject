using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrackPostExtUpdator;

internal static class Invisibler
{
    private static IntPtr _windowHandle = IntPtr.Zero;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    public static void MakeInvisible()
    {
        _windowHandle = Process.GetCurrentProcess().MainWindowHandle;

        if (_windowHandle == IntPtr.Zero)
            _windowHandle = GetConsoleWindow();

        ShowWindow(_windowHandle, SW_HIDE);
    }

    public static void MakeVisible()
    {
        if (_windowHandle == IntPtr.Zero)
            _windowHandle = GetConsoleWindow();

        ShowWindow(_windowHandle, SW_SHOW);
    }
}

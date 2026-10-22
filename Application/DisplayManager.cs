using System.Runtime.InteropServices;
using Raylib_cs;

namespace CCM.Application;

public readonly record struct Resolution(int Width, int Height)
{
    public override string ToString() => $"{Width} x {Height}";
}
public sealed class DisplayManager
{
    private readonly Dictionary<int, Resolution> desktopSizes = [];
    public DisplaySettings Current => new() {
        Width = Raylib.GetScreenWidth(), Height = Raylib.GetScreenHeight(), Fullscreen = Raylib.IsWindowFullscreen()
    };
    public List<Resolution> Available(bool fullscreen)
    {
        int monitor = Raylib.GetCurrentMonitor();
        if (!Raylib.IsWindowFullscreen() || !desktopSizes.ContainsKey(monitor))
            desktopSizes[monitor] = new(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
        var native = desktopSizes[monitor];
        var modes = fullscreen ? NativeModes() : new List<Resolution> {
            new(640,480), new(800,600), new(1024,768), new(1280,720), new(1366,768),
            new(1600,900), new(1920,1080), new(2560,1440), new(3840,2160)
        };
        modes.Add(native);
        if (!fullscreen) modes.Add(new(Current.Width, Current.Height));
        return modes.Where(r => r.Width >= 640 && r.Height >= 480 &&
            (fullscreen || r.Width <= native.Width && r.Height <= native.Height ||
                r == new Resolution(Current.Width, Current.Height)))
            .Distinct().OrderBy(r => r.Width).ThenBy(r => r.Height).ToList();
    }
    public void Apply(DisplaySettings requested)
    {
        requested.Validate();
        var valid = Available(requested.Fullscreen);
        var size = new Resolution(requested.Width, requested.Height);
        if (!valid.Contains(size)) size = valid.Last();
        // Always leave exclusive mode before resizing; Raylib restores the desktop mode.
        if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
        Raylib.SetWindowSize(size.Width, size.Height);
        if (requested.Fullscreen) Raylib.ToggleFullscreen();
        else
        {
            int monitor = Raylib.GetCurrentMonitor();
            var origin = Raylib.GetMonitorPosition(monitor);
            Raylib.SetWindowPosition((int)origin.X + Math.Max(0, (Raylib.GetMonitorWidth(monitor) - size.Width) / 2),
                (int)origin.Y + Math.Max(30, (Raylib.GetMonitorHeight(monitor) - size.Height) / 2));
        }
    }
    private static List<Resolution> NativeModes()
    {
        var result = new List<Resolution>();
        if (!OperatingSystem.IsWindows()) return result; // Native resolution is the portable safe fallback.
        var position = Raylib.GetWindowPosition();
        var point = new Point { X = (int)position.X + Raylib.GetScreenWidth()/2,
            Y = (int)position.Y + Raylib.GetScreenHeight()/2 };
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
        if (!GetMonitorInfo(MonitorFromPoint(point, 2), ref info)) return result;
        IntPtr mode = Marshal.AllocHGlobal(220);
        try
        {
            for (int index = 0; ; index++)
            {
                Marshal.Copy(new byte[220], 0, mode, 220);
                Marshal.WriteInt16(mode, 68, 220); // DEVMODEW.dmSize
                if (!EnumDisplaySettings(info.Device, index, mode)) break;
                if (Marshal.ReadInt32(mode, 168) >= 24)
                    result.Add(new(Marshal.ReadInt32(mode, 172), Marshal.ReadInt32(mode, 176)));
            }
        }
        finally { Marshal.FreeHGlobal(mode); }
        return result;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public int Left, Top, Right, Bottom, WorkLeft, WorkTop, WorkRight, WorkBottom, Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string device, int mode, IntPtr settings);
}

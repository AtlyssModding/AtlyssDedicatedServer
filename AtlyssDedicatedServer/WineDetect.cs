using System.Runtime.InteropServices;

namespace AtlyssDedicatedServer;

public static class WineDetect
{
    [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetModuleHandleA(string moduleName);

    [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr module, string procName);

    private static bool _checkDone = false;
    private static bool _isRunningInWine = false;
    private static string _systemName = "";
    private static string _systemVersion = "";
    private static string _wineVersion = "";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr WineGetVersion();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate void WineGetHostVersion(out string sysName, out string version);

    public static bool IsRunningInWine
    {
        get
        {
            Check();
            return _isRunningInWine;
        }
    }

    public static string SystemName
    {
        get
        {
            Check();
            return _systemName;
        }
    }

    public static string SystemVersion
    {
        get
        {
            Check();
            return _systemVersion;
        }
    }

    public static string WineVersion
    {
        get
        {
            Check();
            return _wineVersion;
        }
    }

    public static void Check()
    {
        if (_checkDone)
            return;

        _checkDone = true;

        var ntdll = GetModuleHandleA("ntdll.dll");

        if (ntdll == IntPtr.Zero)
            return;

        var wine_get_version = GetProcAddress(ntdll, "wine_get_version");
        var wine_get_host_version = GetProcAddress(ntdll, "wine_get_host_version");

        if (wine_get_version == IntPtr.Zero || wine_get_host_version == IntPtr.Zero)
            return;

        var wineVersionPtr = Marshal.GetDelegateForFunctionPointer<WineGetVersion>(wine_get_version)();
        var wineVersion = Marshal.PtrToStringAnsi(wineVersionPtr);

        Marshal.GetDelegateForFunctionPointer<WineGetHostVersion>(wine_get_host_version)(out var sysName, out var version);

        _isRunningInWine = true;
        _systemName = sysName;
        _systemVersion = version;
        _wineVersion = wineVersion;
    }
}
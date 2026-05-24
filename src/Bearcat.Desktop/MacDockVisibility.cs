using System;
using System.Runtime.InteropServices;

namespace Bearcat.Desktop;

public static class MacDockVisibility
{
    private const int RegularActivationPolicy = 0;
    private const int AccessoryActivationPolicy = 1;

    public static void Show()
    {
        SetActivationPolicy(RegularActivationPolicy);
        Activate();
    }

    public static void Hide()
    {
        SetActivationPolicy(AccessoryActivationPolicy);
    }

    private static void SetActivationPolicy(int policy)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var app = GetSharedApplication();
        objc_msgSend(app, sel_registerName("setActivationPolicy:"), policy);
    }

    private static void Activate()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var app = GetSharedApplication();
        objc_msgSend(app, sel_registerName("activateIgnoringOtherApps:"), true);
    }

    private static IntPtr GetSharedApplication()
    {
        var nsApplication = objc_getClass("NSApplication");
        return objc_msgSend(nsApplication, sel_registerName("sharedApplication"));
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, int argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, bool argument);
}

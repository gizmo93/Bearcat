using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Bearcat.Desktop;

public static class MacDockIcon
{
    public static void Set(string relativeIconPath)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, relativeIconPath);
        if (!File.Exists(iconPath))
        {
            return;
        }

        var nsApplication = objc_getClass("NSApplication");
        var nsImage = objc_getClass("NSImage");
        var nsString = objc_getClass("NSString");

        var app = objc_msgSend(nsApplication, sel_registerName("sharedApplication"));
        var pathString = objc_msgSend(nsString, sel_registerName("alloc"));
        pathString = objc_msgSend(pathString, sel_registerName("initWithUTF8String:"), iconPath);

        var image = objc_msgSend(nsImage, sel_registerName("alloc"));
        image = objc_msgSendIntPtr(image, sel_registerName("initWithContentsOfFile:"), pathString);

        if (image != IntPtr.Zero)
        {
            objc_msgSend(app, sel_registerName("setApplicationIconImage:"), image);
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", BestFitMapping = false)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", BestFitMapping = false)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend", BestFitMapping = false)]
    private static extern IntPtr objc_msgSend(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string argument
    );

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSendIntPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr argument
    );
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Bearcat.Infrastructure.FileSystem;

[SupportedOSPlatform("windows")]
internal static partial class WindowsDiskFreeSpace
{
    public static long GetAvailableFreeSpaceBytes(string directoryPath)
    {
        var directoryPathWithTrailingSeparator = Path.EndsInDirectorySeparator(directoryPath)
            ? directoryPath
            : directoryPath + Path.DirectorySeparatorChar;

        if (
            !GetDiskFreeSpaceEx(
                directoryName: directoryPathWithTrailingSeparator,
                freeBytesAvailableToCaller: out var freeBytesAvailableToCaller,
                totalNumberOfBytes: out _,
                totalNumberOfFreeBytes: out _
            )
        )
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return (long)freeBytesAvailableToCaller;
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetDiskFreeSpaceExW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailableToCaller,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes
    );
}

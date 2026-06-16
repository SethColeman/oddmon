using System.Runtime.InteropServices;

namespace Oddmon.App;

internal static class NativeMethods
{
    // Frees the GDI icon handle returned by Bitmap.GetHicon to avoid leaking it.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr handle);
}

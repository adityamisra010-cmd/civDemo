using System.Runtime.InteropServices;
using System.Text;

namespace Sim.Ui;

/// <summary>
/// M5 playtest blocker (D-052): a native error dialog for a refusal that happens BEFORE any window opens (a
/// <c>--resume</c> this build cannot honour, <see cref="ResumeLaunch"/>). Sim.Ui is a GUI-subsystem exe, so on Windows
/// its stderr reaches no one when it is launched from Explorer, the zip's launcher or cmd.exe; a dialog does.
///
/// SDL2's <c>SDL_ShowSimpleMessageBox</c> (SDL_MESSAGEBOX_ERROR, no parent window — legal before <c>SDL_Init</c>), from
/// the SDL2 library MonoGame.Framework.DesktopGL already ships beside the exe (MonoGame.Library.SDL 2.32.10.2:
/// runtimes/win-x64/native/SDL2.dll, linux-x64 libSDL2-2.0.so.0, osx libSDL2-2.0.0.dylib), loaded the way
/// <see cref="SimUiGame"/> reads the display DPI. STRICTLY BEST EFFORT: no library, no export, no display, or any
/// exception at all, and nothing happens — the caller's stderr report stands. It blocks until the player dismisses the
/// dialog, so it is never called from a test or from --smoke (<see cref="ResumeLaunch.NoDialogVariable"/>).
/// </summary>
public static class NativeErrorDialog
{
    /// <summary>SDL_MESSAGEBOX_ERROR.</summary>
    private const uint SdlMessageBoxError = 0x10;

    /// <summary>Shows <paramref name="message"/> under <paramref name="title"/> in a modal error dialog, if it can.</summary>
    public static unsafe void Show(string title, string message)
    {
        try
        {
            foreach (string name in new[] { "SDL2", "libSDL2-2.0.so.0", "libSDL2.so", "libSDL2-2.0.0.dylib" })
            {
                if (!NativeLibrary.TryLoad(name, typeof(NativeErrorDialog).Assembly, null, out IntPtr lib)) continue;
                if (!NativeLibrary.TryGetExport(lib, "SDL_ShowSimpleMessageBox", out IntPtr fn)) continue;
                var show = (delegate* unmanaged[Cdecl]<uint, byte*, byte*, IntPtr, int>)fn;
                byte[] t = Utf8z(title), m = Utf8z(message);
                fixed (byte* tp = t)
                fixed (byte* mp = m)
                    show(SdlMessageBoxError, tp, mp, IntPtr.Zero);   // -1 (no display, …) is ignored: best effort
                return;
            }
        }
        catch (Exception) { }
    }

    /// <summary>UTF-8 bytes with a terminating NUL, as SDL takes strings.</summary>
    private static byte[] Utf8z(string s)
    {
        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
        return bytes;
    }
}

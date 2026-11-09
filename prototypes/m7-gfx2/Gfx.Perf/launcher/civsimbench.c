/*
 * CivSimBench.exe - discrete-GPU launcher for the CivSim G2 benchmark (Windows x64).
 *
 * On Optimus / PowerXpress laptops the NVIDIA and AMD drivers read two exported symbols from the PROCESS EXE to
 * decide whether an OpenGL/D3D context goes to the discrete GPU. A .NET apphost (Gfx.Demo.exe) cannot export
 * data, so this tiny native exe exports them and then runs Gfx.Demo.dll IN THIS PROCESS through the .NET hosting
 * API (hostfxr_main_startupinfo, the same entry the .NET apphost uses), passing every argument through. The
 * benchmark records whether the export was seen (machine.exports_NvOptimusEnablement) and which GPU it got.
 *
 * Built without the C runtime (kernel32 + shell32 only): see scripts/build-launcher.sh. No global state is
 * changed: nothing is written to the registry or the disk.
 */
#define WIN32_LEAN_AND_MEAN
#define UNICODE
#include <windows.h>
#include <shellapi.h>

__declspec(dllexport) DWORD NvOptimusEnablement = 0x00000001;
__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;

typedef int (__cdecl *hostfxr_main_startupinfo_fn)(int argc, const wchar_t **argv, const wchar_t *host_path,
                                                    const wchar_t *dotnet_root, const wchar_t *app_path);

#define CAP 32768
static wchar_t g_exe[CAP], g_dir[CAP], g_fxr[CAP + 32], g_app[CAP + 32];
static char g_utf8[4 * 1024];

static void say(const wchar_t *s)
{
    HANDLE h = GetStdHandle(STD_ERROR_HANDLE);
    DWORD w = 0;
    int n = lstrlenW(s);
    if (h == NULL || h == INVALID_HANDLE_VALUE) return;
    if (!WriteConsoleW(h, s, (DWORD)n, &w, NULL))
    {   /* redirected to a file or pipe: write UTF-8 */
        int m = WideCharToMultiByte(CP_UTF8, 0, s, n, g_utf8, (int)sizeof g_utf8, NULL, NULL);
        if (m > 0) WriteFile(h, g_utf8, (DWORD)m, &w, NULL);
    }
}

static void fail(const wchar_t *what, const wchar_t *path)
{
    say(L"CivSimBench: ");
    say(what);
    if (path) { say(L" "); say(path); }
    say(L"\r\n");
}

void __stdcall entry(void)
{
    int argc = 0;
    LPWSTR *argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    DWORD n = GetModuleFileNameW(NULL, g_exe, CAP);
    int i, cut = -1;
    if (argv == NULL || n == 0 || n >= CAP) { fail(L"cannot read the command line or the exe path", NULL); ExitProcess(1); }
    lstrcpyW(g_dir, g_exe);
    for (i = 0; g_dir[i]; i++) if (g_dir[i] == L'\\' || g_dir[i] == L'/') cut = i;
    if (cut < 0) { fail(L"unexpected exe path", g_exe); ExitProcess(1); }
    g_dir[cut] = 0;
    lstrcpyW(g_fxr, g_dir); lstrcatW(g_fxr, L"\\hostfxr.dll");
    lstrcpyW(g_app, g_dir); lstrcatW(g_app, L"\\Gfx.Demo.dll");
    if (GetFileAttributesW(g_app) == INVALID_FILE_ATTRIBUTES) { fail(L"Gfx.Demo.dll not found next to the launcher:", g_app); ExitProcess(1); }
    HMODULE fxr = LoadLibraryExW(g_fxr, NULL, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (fxr == NULL) { fail(L"cannot load the .NET host (self-contained package expected):", g_fxr); ExitProcess(1); }
    hostfxr_main_startupinfo_fn run = (hostfxr_main_startupinfo_fn)(void *)GetProcAddress(fxr, "hostfxr_main_startupinfo");
    if (run == NULL) { fail(L"hostfxr_main_startupinfo not exported by", g_fxr); ExitProcess(1); }
    ExitProcess((UINT)run(argc, (const wchar_t **)argv, g_exe, g_dir, g_app));
}

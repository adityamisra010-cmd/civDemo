@echo off
setlocal
rem CivSim benchmark: ask Windows to give CivSimBench.exe and Gfx.Demo.exe the high-performance GPU.
rem This is the per-user setting behind Settings > System > Display > Graphics. It writes ONLY to
rem HKEY_CURRENT_USER (no administrator rights) and only these two values. Undo-GPU-Preference.cmd removes them.
cd /d "%~dp0"
set "KEY=HKCU\Software\Microsoft\DirectX\UserGpuPreferences"
for %%E in ("%~dp0CivSimBench.exe" "%~dp0Gfx.Demo.exe") do (
  reg add "%KEY%" /v "%%~fE" /t REG_SZ /d "GpuPreference=2;" /f >nul
  if errorlevel 1 (echo Could not set the preference for %%~fE) else (echo High performance GPU requested for %%~fE)
)
echo.
echo Done. You can check it in Settings - System - Display - Graphics.
echo If the GPU check still shows Intel, also use the NVIDIA Control Panel steps in README-VICTUS.txt.
pause

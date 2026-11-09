@echo off
setlocal
rem CivSim benchmark: remove the two per-user GPU preference values that Set-GPU-Preference-HighPerformance.cmd
rem wrote (HKEY_CURRENT_USER only). Nothing else is touched.
cd /d "%~dp0"
set "KEY=HKCU\Software\Microsoft\DirectX\UserGpuPreferences"
for %%E in ("%~dp0CivSimBench.exe" "%~dp0Gfx.Demo.exe") do (
  reg delete "%KEY%" /v "%%~fE" /f >nul 2>&1
  if errorlevel 1 (echo No GPU preference was set for %%~fE) else (echo Removed the GPU preference for %%~fE)
)
pause

@echo off
setlocal EnableExtensions EnableDelayedExpansion
rem ==============================================================================================
rem CivSim G2 benchmark: two GPU checks, the benchmark suite, dxdiag, then one zip to send back.
rem   run-benchmark.bat            standard suite: 39 configurations x 3 rounds (estimate: 15-40 minutes on the RTX 4050)
rem   run-benchmark.bat quick      5 configurations, a few minutes (a first look)
rem   run-benchmark.bat smoke      5 short runs, about 1 minute (checks that everything starts)
rem   run-benchmark.bat SUITE anyway   benchmark even when the RTX 4050 is not in use (results say INVALID)
rem Nothing outside this folder is changed. Send back the victus-results-N.zip written next to this file.
rem ==============================================================================================
cd /d "%~dp0"
set "SUITE=%~1"
set "ANYWAY=%~2"
set /a RETRIES=0
if "%SUITE%"=="" set "SUITE=standard"
if /i "%SUITE%"=="standard" goto :suite_ok
if /i "%SUITE%"=="quick" goto :suite_ok
if /i "%SUITE%"=="smoke" goto :suite_ok
echo Unknown suite "%SUITE%". Use: run-benchmark.bat [standard^|quick^|smoke]
exit /b 2
:suite_ok
if not exist results mkdir results
set "LOG=results\bench-log.txt"
set "RC_BENCH=not run"
>>"%LOG%" echo.
>>"%LOG%" echo ===== run-benchmark.bat %SUITE% started %DATE% %TIME% =====
ver >>"%LOG%" 2>&1
echo.
echo CivSim G2 benchmark - suite %SUITE%
echo Results go to "%CD%\results" and are zipped at the end.
echo.

:check_a
echo [1/5] GPU check A: Gfx.Demo.exe, the plain exe ...
"%~dp0Gfx.Demo.exe" --gpu-selftest --require-nvidia --out results\selftest-plain-exe --log "%LOG%" >nul 2>&1
set "RC_A=!ERRORLEVEL!"
call :victus results\selftest-plain-exe.txt !RC_A! GPU_A
call :renderer results\selftest-plain-exe.txt R_A
echo       exit code !RC_A!, GL_RENDERER: !R_A!  [!GPU_A!]

echo [2/5] GPU check B: CivSimBench.exe, the launcher that asks for the NVIDIA GPU ...
"%~dp0CivSimBench.exe" --gpu-selftest --require-nvidia --out results\selftest-launcher --log "%LOG%" >nul 2>&1
set "RC_B=!ERRORLEVEL!"
call :victus results\selftest-launcher.txt !RC_B! GPU_B
call :renderer results\selftest-launcher.txt R_B
echo       exit code !RC_B!, GL_RENDERER: !R_B!  [!GPU_B!]

set "EXE="
if "!GPU_B!"=="VALID" set "EXE=CivSimBench.exe"
if not defined EXE if "!GPU_A!"=="VALID" set "EXE=Gfx.Demo.exe"
if defined EXE goto :gpu_ok

echo.
echo ##############################################################################
echo ##                                                                          ##
echo ##   WARNING: THE NVIDIA RTX 4050 IS NOT IN USE                             ##
echo ##                                                                          ##
echo ##   Results on this GPU are NOT Victus evidence - files say INVALID.       ##
echo ##   Fix: README-VICTUS.txt step 3 - Set-GPU-Preference-HighPerformance.cmd ##
echo ##   and NVIDIA Control Panel - Manage 3D settings - Program Settings -     ##
echo ##   CivSimBench.exe - High-performance NVIDIA processor. Then press R.     ##
echo ##                                                                          ##
echo ##############################################################################
echo   check A got: !R_A!
echo   check B got: !R_B!
>>"%LOG%" echo WARNING: RTX 4050 not in use. A: !R_A! / B: !R_B!
echo.
if /i "%ANYWAY%"=="anyway" goto :invalid_anyway
choice /c RYN /n /t 300 /d N /m "R = check again, Y = benchmark anyway (INVALID results), N = stop. [R/Y/N]  (N after 5 minutes) "
set "ANS=!ERRORLEVEL!"
if "!ANS!"=="2" goto :invalid_anyway
if not "!ANS!"=="1" goto :stopped
set /a RETRIES+=1
if !RETRIES! GEQ 5 goto :stopped
goto :check_a

:invalid_anyway
set "EXE=CivSimBench.exe"
>>"%LOG%" echo user chose to benchmark on the non-RTX GPU: results are INVALID_NOT_RTX4050
goto :bench

:gpu_ok
echo       using %EXE% - it got the RTX 4050.
findstr /c:"ON BATTERY" results\selftest-launcher.txt results\selftest-plain-exe.txt >nul 2>&1
if errorlevel 1 goto :bench
echo.
echo WARNING: the laptop is ON BATTERY. Please plug in the charger: on battery the GPU runs much slower.
choice /c YN /n /t 120 /d Y /m "Continue anyway? [Y/N]  (Y after 2 minutes) "
set "ANS=!ERRORLEVEL!"
if "!ANS!"=="2" goto :stopped

:bench
echo.
echo [3/5] Benchmark: %EXE% --suite %SUITE%
echo       Keep the benchmark window in front and do not use the laptop until it finishes.
echo       started %TIME%
"%~dp0%EXE%" --suite %SUITE% --out results --log "%LOG%"
set "RC_BENCH=!ERRORLEVEL!"
echo       finished %TIME% with exit code !RC_BENCH!
if "!RC_BENCH!"=="4" echo       The benchmark window was closed early: the runs finished so far are kept.
if "!RC_BENCH!"=="1" echo       The benchmark reported an error - the log and the partial results are still collected.
goto :collect

:stopped
echo Stopped: nothing was benchmarked. The GPU checks and dxdiag are still collected for diagnosis.
>>"%LOG%" echo stopped at the GPU check or the battery warning

:collect
echo [4/5] dxdiag and nvidia-smi - about 10-30 seconds ...
if exist results\dxdiag.txt del /q results\dxdiag.txt
start "" /wait dxdiag /whql:off /t "%CD%\results\dxdiag.txt"
set /a WAITED=0
:dxwait
if exist results\dxdiag.txt goto :dxdone
if !WAITED! GEQ 90 goto :dxdone
ping -n 3 127.0.0.1 >nul
set /a WAITED+=2
goto :dxwait
:dxdone
if not exist results\dxdiag.txt echo       dxdiag did not write its report - continuing without it.
where nvidia-smi >nul 2>&1
if not errorlevel 1 nvidia-smi -q >results\nvidia-smi-q.txt 2>&1

echo [5/5] Packing the results ...
set "ZIP="
for /l %%N in (1,1,999) do if not defined ZIP if not exist "victus-results-%%N.zip" set "ZIP=victus-results-%%N.zip"
>>"%LOG%" echo ===== run-benchmark.bat %SUITE% finished %DATE% %TIME%, benchmark exit !RC_BENCH!, zip %ZIP% =====
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'results' -DestinationPath '%ZIP%' -CompressionLevel Optimal" >nul 2>&1
if not exist "%ZIP%" tar -a -c -f "%ZIP%" results >nul 2>&1
echo.
if exist "%ZIP%" goto :zipped
echo Could not create the zip. Please zip the "results" folder by hand and send it back.
goto :end
:zipped
echo ==============================================================================
echo   DONE. Please send back this file:
echo   %CD%\%ZIP%
echo ==============================================================================
:end
echo.
pause
if "!RC_BENCH!"=="not run" exit /b 3
exit /b !RC_BENCH!

:victus
rem %1 = self-test report, %2 = its exit code (3 = not an NVIDIA GPU), %3 = variable to set VALID or INVALID.
rem The label is read on its own line: no "&&" chains, which some command interpreters mis-run after redirections.
set "%~3=INVALID"
if "%~2"=="3" goto :eof
if not exist "%~1" goto :eof
findstr /c:"VICTUS_CHECK=VALID_VICTUS_RTX4050" "%~1" >nul 2>&1
if not errorlevel 1 set "%~3=VALID"
goto :eof

:renderer
set "%~2=not reported"
if not exist "%~1" goto :eof
for /f "tokens=1,* delims= " %%a in ('findstr /b /c:"GL_RENDERER" "%~1"') do set "%~2=%%b"
goto :eof

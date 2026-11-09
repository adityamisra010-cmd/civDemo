CivSim G2 benchmark - BEFORE package @PACKAGE@ (source commit @COMMIT@, built @BUILT@)
======================================================================================

WHAT THIS IS
  A Windows build of the M7 graphics prototype G2 at its starting point: the G1 renderer and the G1
  placeholder scenes (low-poly soldiers, procedural terrain, parametric avatars), plus a benchmark harness.
  It measures how the CURRENT placeholder graphics run on your HP Victus (NVIDIA RTX 4050 Laptop GPU).
  This is the BEFORE measurement; later G2 builds will be measured the same way and compared with it.
  Results only count as Victus evidence when the benchmark ran on the RTX 4050; files say VALID_VICTUS_RTX4050
  then, and INVALID_NOT_RTX4050 on any other GPU (for example the Intel Iris Xe).

HOW LONG
  run-benchmark.bat (standard suite, 39 configurations x 3 rounds = 117 runs): an ESTIMATE of 15-40 minutes on
  the RTX 4050 (nobody has run it on that GPU yet; each run is a scene load, at least 2 seconds of warm-up and
  600 measured frames), plus about 1 minute of checks and 30 seconds for dxdiag. On the Intel GPU it takes
  several times longer. "run-benchmark.bat quick" takes a few minutes and "run-benchmark.bat smoke" about one;
  please send the standard one if you can. The window title shows the run number (run N/117).

STEPS
  1. Unzip this folder to a short local path, for example C:\CivSimBench (avoid OneDrive folders).
  2. Plug in the charger. In OMEN Gaming Hub choose the Performance mode and note which mode you chose.
     Close other programs (browser, games, video calls). Windows Settings > System > Power: Best performance.
  3. Make sure the benchmark gets the NVIDIA GPU (the script checks this and warns loudly if not):
     a. Double-click Set-GPU-Preference-HighPerformance.cmd. It sets the per-user Windows graphics
        preference for CivSimBench.exe and Gfx.Demo.exe (no administrator rights; Undo-GPU-Preference.cmd
        removes it again).
     b. NVIDIA Control Panel > Manage 3D settings > Program Settings > Add > Browse... > choose
        CivSimBench.exe in this folder > "Select the preferred graphics processor for this program":
        High-performance NVIDIA processor > Apply. In the same page keep Vertical sync = "Use the 3D
        application setting" and Max Frame Rate = Off.
     c. If OMEN Gaming Hub offers a graphics switcher (MUX), "Hybrid" is fine; "Discrete" also works
        (it needs a restart).
  4. Double-click run-benchmark.bat. It:
       - runs GPU check A with Gfx.Demo.exe and GPU check B with CivSimBench.exe (a small launcher that
         asks the NVIDIA driver for the RTX 4050);
       - shows a big WARNING if neither got the RTX 4050: fix step 3, then press R to check again;
       - runs the benchmark in a window: leave that window in front and do not use the laptop meanwhile;
       - runs dxdiag (and nvidia-smi), then packs everything into victus-results-1.zip next to the script.
  5. Send back the victus-results-N.zip it names at the end, and tell us the OMEN mode you used.
     If Windows SmartScreen says "Windows protected your PC" (the files are not signed): More info > Run anyway.

WHAT TO SEND BACK
  victus-results-N.zip. It contains the folder results\ with:
    perf-<date>-<time>-VALID_VICTUS_RTX4050.json / .csv / -summary.txt  (all measurements; INVALID_... if not
                                                                       on the RTX 4050)
    captures\*.jpg                      one picture per scene, so we see what was rendered
    selftest-plain-exe.txt/.json/.jpg   GPU check A
    selftest-launcher.txt/.json/.jpg    GPU check B
    bench-log.txt                       everything the scripts printed
    dxdiag.txt, nvidia-smi-q.txt        hardware and driver report (dxdiag includes the computer name)
  Nothing else on the laptop is read or changed.

WHAT IS MEASURED
  Five scene slots: A soldier close-up, B formation of 1,000, C large battlefield, D cinematic,
  E campaign environment. They are bound to the G1 placeholder scenes for this BEFORE measurement.
  Per configuration: frame time (p50/p95/p99, frame-rate distribution), CPU and GPU frame time, GPU time per
  render pass, CPU time per section, memory, VRAM, draw calls, triangles, instances, soldier LOD counts,
  load time, and (through NVIDIA's NVML) GPU clocks, temperature, power and throttle reasons.
  Sweeps: resolution 1280x720 / 1920x1080 / 2560x1440 (rendered offscreen at that size; the window shows a
  scaled preview), LOD distance x0.25..x4, shadows off/1024/default/4096, and crowd size 500..16,000.
  Each run is flagged when conditions were not clean: ON_BATTERY, WINDOW_NOT_FOCUSED,
  THERMAL_OR_POWER_THROTTLE, VSYNC_OR_FRAME_CAP_SUSPECTED, GPU_TIMER_FRAMES_DROPPED, GEN2_GC_DURING_MEASUREMENT.

IF SOMETHING GOES WRONG
  - The benchmark window was closed early: the runs finished so far are kept (exit code 4). Run again, or send
    what you have.
  - The GPU checks keep showing Intel: repeat step 3b, restart Windows, try again. If it still does not work,
    send the zip anyway: the two selftest files tell us why.
  - "run-benchmark.bat standard anyway" benchmarks without asking even when the RTX 4050 is not in use (for
    example to measure the Intel GPU on purpose); those files say INVALID_NOT_RTX4050.
  - Exit codes: 0 finished, 1 error, 2 bad options, 3 refused/not run, 4 window closed early.
  - Command line (optional): Gfx.Demo.exe --perf-help lists every option.
  - THIRD-PARTY-NOTICES.txt lists the redistributed runtime libraries and their licences.

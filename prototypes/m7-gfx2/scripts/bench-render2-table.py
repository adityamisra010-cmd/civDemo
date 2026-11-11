#!/usr/bin/env python3
"""Summarises the raw logs of scripts/bench-render2.sh into one TSV row per (run, variant).

Usage: bench-render2-table.py RAW_DIR > table.tsv
Columns: run, scene, shot, variant, p50/p95 ms, draws, tris, crowd figures (drawn, per level, culled, shadow), crowd CPU,
occlusion build ms, and every [pass] figure of --profile runs. All numbers come from llvmpipe when run without a GPU:
not Victus evidence.
"""
import os
import re
import sys

raw = sys.argv[1]
cols = ["run", "scene", "shot", "variant", "p50_ms", "p95_ms", "draws", "tris", "crowd_drawn", "lod1", "lod2", "lod3",
        "infade", "culled_frustum", "culled_occlusion", "crowd_tris", "shadow_instances", "shadow_tris",
        "shadow_culled_cascade", "cpu_set_agents_ms", "cpu_cull_level_ms", "cpu_shadow_select_ms", "occlusion_build_ms",
        "occluder_tris", "passes"]
print("\t".join(cols))
for name in sorted(os.listdir(raw)):
    if not name.endswith(".log"):
        continue
    text = open(os.path.join(raw, name), encoding="utf-8").read()
    # the bench prints "[variant] NAME" before each variant's figures: pair each such line with the text after it
    parts = re.split(r"^\[variant\] (.*)$", text, flags=re.M)
    fixed = [(parts[i].strip(), parts[i + 1]) for i in range(1, len(parts) - 1, 2)]
    for v, body in fixed:
        def g(pat, default=""):
            m = re.search(pat, body)
            return m.group(1) if m else default
        row = {
            "run": name[:-4], "variant": v,
            "scene": g(r"\[bench\] scene=(\S+)"), "shot": g(r" shot=(\S+)"),
            "p50_ms": g(r" p50=([0-9.]+)"), "p95_ms": g(r" p95=([0-9.]+)"),
            "draws": g(r"\| draws=([0-9]+)"), "tris": g(r"\| draws=[0-9]+ tris=([0-9]+)"),
            "crowd_drawn": g(r"\[crowd\] agents=[0-9]+ drawn=([0-9]+)"),
            "lod1": g(r" lod1=([0-9]+)"), "lod2": g(r" lod2=([0-9]+)"), "lod3": g(r" lod3=([0-9]+)"),
            "infade": g(r" infade=([0-9]+)"), "culled_frustum": g(r" culled-frustum=([0-9]+)"),
            "culled_occlusion": g(r" culled-occlusion=([0-9]+)"), "crowd_tris": g(r"\[crowd\].* draws=[0-9]+ tris=([0-9]+)"),
            "shadow_instances": g(r" shadow-instances=([0-9]+)"), "shadow_tris": g(r" shadow-tris=([0-9]+)"),
            "shadow_culled_cascade": g(r" shadow-culled-cascade=([0-9]+)"),
            "cpu_set_agents_ms": g(r" set-agents=([0-9.]+) ms"), "cpu_cull_level_ms": g(r" cull\+level=([0-9.]+) ms"),
            "cpu_shadow_select_ms": g(r" shadow-select=([0-9.]+) ms"),
            "occlusion_build_ms": g(r" occlusion-build=([0-9.]+) ms"), "occluder_tris": g(r" occluder-tris=([0-9]+)"),
            "passes": ";".join(f"{n}={ms}" for n, ms in re.findall(r"^\[pass\] (\S+)\s+([0-9.]+) ms/frame", body, flags=re.M)),
        }
        print("\t".join(row[c] for c in cols))

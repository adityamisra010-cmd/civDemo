#!/usr/bin/env python3
"""Compare two G2 benchmark result files (BEFORE vs AFTER), matched by configuration VALUES, not by row order.

    python3 -I scripts/compare-perf.py BEFORE.json|csv AFTER.json|csv [--metric frame_ms_p50]

Reads civsim-perf/1 JSON (perf-*-<LABEL>.json) or the matching CSV. A configuration key is
(slot, width, height, lod_mult, shadows, crowd, camera_path), so results from packages whose configuration ids or
row order differ still line up. Repetitions (rounds) are reduced to their median. Prints one row per configuration
present in both, plus the configurations present in only one file. Never mixes labels silently: the label of each
file (VALID_VICTUS_RTX4050 / INVALID_NOT_RTX4050) is printed first, and a comparison across different GPUs is
flagged.
"""
import csv
import json
import statistics
import sys

KEY_FIELDS = ("slot", "width", "height", "lod_mult", "shadows", "crowd", "camera_path")
METRICS = ("frame_ms_p50", "frame_ms_p95", "frame_ms_p99", "gpu_ms_p50", "cpu_ms_p50", "draws_mean", "tris_mean")


def _num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def load(path):
    """Returns (label, renderer, {key: {metric: [values per round]}})."""
    rows = []
    if path.endswith(".json"):
        with open(path, encoding="utf-8") as f:
            d = json.load(f)
        label, renderer = d.get("label", "?"), d.get("gpu", {}).get("renderer", "?")
        for r in d.get("runs", []):
            if r.get("status") != "ok":
                continue
            c = r["config"]
            rows.append({
                "slot": c["slot"], "width": c["width"], "height": c["height"], "lod_mult": c["lod_mult"],
                "shadows": c["shadows"], "crowd": c.get("crowd"), "camera_path": c["camera_path"],
                "frame_ms_p50": r["frame_ms"]["p50"], "frame_ms_p95": r["frame_ms"]["p95"], "frame_ms_p99": r["frame_ms"]["p99"],
                "gpu_ms_p50": r["gpu_ms"]["p50"], "cpu_ms_p50": r["cpu_ms"]["p50"],
                "draws_mean": r["counters"]["draws_mean"], "tris_mean": r["counters"]["triangles_mean"],
            })
    else:
        with open(path, encoding="utf-8-sig", newline="") as f:
            rd = csv.DictReader(f)
            missing = [k for k in KEY_FIELDS + ("frame_ms_p50",) if k not in (rd.fieldnames or [])]
            if missing:
                sys.exit(f"{path}: not a civsim-perf CSV (missing columns {missing})")
            label, renderer = "?", "?"
            for r in rd:
                if r.get("status", "ok") != "ok":
                    continue
                label, renderer = r.get("label", label), r.get("renderer", renderer)
                rows.append({**{k: r[k] for k in KEY_FIELDS}, **{m: r.get(m) for m in METRICS}})
    table = {}
    for r in rows:
        key = (str(r["slot"]), int(float(r["width"])), int(float(r["height"])), round(float(r["lod_mult"]), 3),
               str(r["shadows"]), "" if r["crowd"] in (None, "") else str(int(float(r["crowd"]))), str(r["camera_path"]))
        t = table.setdefault(key, {m: [] for m in METRICS})
        for m in METRICS:
            v = _num(r.get(m))
            if v is not None:
                t[m].append(v)
    return label, renderer, table


def fmt_key(k):
    slot, w, h, lod, shd, crowd, path = k
    return f"{slot} {w}x{h} lod x{lod:g} shd {shd}" + (f" crowd {crowd}" if crowd else "") + f" [{path}]"


def main(argv):
    if len(argv) < 3 or argv[1] in ("-h", "--help"):
        print(__doc__)
        return 2
    metric = "frame_ms_p50"
    if "--metric" in argv:
        metric = argv[argv.index("--metric") + 1]
        if metric not in METRICS:
            sys.exit(f"--metric must be one of {METRICS}")
    la, ra, a = load(argv[1])
    lb, rb, b = load(argv[2])
    print(f"BEFORE {argv[1]}: {la} ({ra})")
    print(f"AFTER  {argv[2]}: {lb} ({rb})")
    if ra != rb:
        print("WARNING: different GPUs/renderers - this compares machines, not code")
    print(f"\nmetric {metric}: median over rounds; delta = AFTER - BEFORE")
    print(f"{'configuration':<52} {'before':>10} {'after':>10} {'delta':>10} {'ratio':>7}")
    for k in sorted(set(a) & set(b)):
        va, vb = a[k][metric], b[k][metric]
        if not va or not vb:
            continue
        ma, mb = statistics.median(va), statistics.median(vb)
        ratio = f"{mb / ma:6.2f}x" if ma else "-"
        print(f"{fmt_key(k):<52} {ma:10.3f} {mb:10.3f} {mb - ma:+10.3f} {ratio:>7}")
    for name, only in (("BEFORE", set(a) - set(b)), ("AFTER", set(b) - set(a))):
        for k in sorted(only):
            print(f"only in {name}: {fmt_key(k)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

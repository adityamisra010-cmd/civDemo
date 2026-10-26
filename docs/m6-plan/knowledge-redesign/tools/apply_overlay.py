"""Write a research OVERLAY into a research.json — STAGED for a future Director ruling; never run on the shipped file
without one (the research content hash is folded into every world hash, so every golden and CI hash pin moves).

Same semantics as Sim.Tests ResearchPacingRunner.ApplyOverlay: tuning members replace the shipped members of the same
name ("_doc" skipped); a node patch {"magnitudeStep": s} moves the node's magnitude by s (down: breadth then novelty to
0.5; up: difficulty to 3.5 then novelty to 2) and its cost to U x 2^magnitude rounded to tens (finalization ruling 1).

Usage: python3 -I tools/apply_overlay.py <research.json in> <research.json out> <overlay.json>...
       (out may differ from in: write a candidate file, diff it, run the suites against it, then decide)
"""
import json
import sys


def apply(doc, overlay):
    for k, v in (overlay.get("tuning") or {}).items():
        if k != "_doc":
            doc["tuning"][k] = v
    nodes = overlay.get("nodes") or {}
    patched = 0
    for tree in ("technologies", "civics"):
        for n in doc[tree]:
            if n["id"] not in nodes:
                continue
            step = float(nodes[n["id"]]["magnitudeStep"])
            cr, f = n["costRationale"], n["costRationale"]["factors"]
            rest = step
            if rest < 0:
                take = min(-rest, f["breadth"]); f["breadth"] -= take; rest += take
                take = min(-rest, f["novelty"] - 0.5); f["novelty"] -= take; rest += take
            elif rest > 0:
                give = min(rest, 3.5 - f["difficulty"]); f["difficulty"] += give; rest -= give
                give = min(rest, 2.0 - f["novelty"]); f["novelty"] += give; rest -= give
            if abs(rest) > 1e-12:
                raise SystemExit(f"{n['id']}: cannot move {step} within the factor ranges")
            cr["magnitude"] += step
            cost = float(int(92.4 * 2.0 ** cr["magnitude"] / 10.0 + 0.5) * 10)   # away from zero for positive values
            cr["content_cost"] = cost
            n["cost"] = cost
            patched += 1
    if patched != len(nodes):
        raise SystemExit(f"{len(nodes) - patched} node ids not found")


def main(argv):
    src, dst, overlays = argv[0], argv[1], argv[2:]
    doc = json.load(open(src, encoding="utf-8"))
    for p in overlays:
        apply(doc, json.load(open(p, encoding="utf-8")))
    with open(dst, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1, ensure_ascii=True)
        fh.write("\n")
    print(f"wrote {dst} ({len(overlays)} overlay(s))")


if __name__ == "__main__":
    main(sys.argv[1:])

"""Reduce a session telemetry JSONL (telemetry/v4) to a per-turn, per-controller population CSV.

For telemetry row t (the step t-1 -> t), settlements[].controller is EmpireQuery.TryGetController on NEXT
(state t; -1 when uncontrolled), population.closing is the settlement's population in state t and
population.opening its population in state t-1. Output columns:
  turn, controller, settlements, population_closing, population_opening
(one row per controller present that turn, controller ascending). Only reads; writes one CSV.
"""
import csv
import json
import sys


def main(src, dst):
    with open(src, "r", encoding="utf-8") as f, open(dst, "w", newline="") as g:
        w = csv.writer(g, lineterminator="\n")
        w.writerow(["turn", "controller", "settlements", "population_closing", "population_opening"])
        for line in f:
            d = json.loads(line)
            turn = d["turn"]["turn"]
            agg = {}
            for s in d["settlements"]:
                c = s["controller"]
                a = agg.setdefault(c, [0, 0, 0])
                a[0] += 1
                a[1] += s["population"]["closing"]
                a[2] += s["population"]["opening"]
            for c in sorted(agg):
                w.writerow([turn, c] + agg[c])


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])

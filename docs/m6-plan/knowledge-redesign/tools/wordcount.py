#!/usr/bin/env python3
"""Word-count self-check for corpus files (the same counter the validator uses).

Usage: python3 -I tools/wordcount.py corpus/<topic>.json [more.json ...]
Prints one line per entry outside 50-60 words and a summary per file.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402


def main(argv):
    bad_total = 0
    for p in argv:
        entries = kr.load_json(p)
        counts = []
        for e in entries:
            n = kr.word_count(e.get("description"))
            counts.append(n)
            if not 50 <= n <= 60:
                flag = "ALLOWED(45-65)" if 45 <= n <= 65 else "OUTSIDE"
                print(f"{p}: {e['id']}: {n} words {flag}")
                bad_total += 0 if 45 <= n <= 65 else 1
        if counts:
            print(f"{p}: {len(counts)} entries, min {min(counts)}, max {max(counts)}, "
                  f"mean {sum(counts) / len(counts):.1f}, in 50-60: {sum(1 for c in counts if 50 <= c <= 60)}")
    return 1 if bad_total else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

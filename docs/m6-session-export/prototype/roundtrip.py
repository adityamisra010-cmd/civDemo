"""roundtrip.py: encode every line of every corpus file, decode, compare. Writes results.json."""
import json, os, sys, statistics, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import codec

HERE = os.path.dirname(os.path.abspath(__file__))
CORPUS = os.path.join(HERE, '..', 'corpus')
FILES = {'A': 'a/telemetry-c12cf174e3e0cbb8.jsonl', 'B': 'b/telemetry-18d9b62399adc5b2.jsonl',
         'C': 'c/telemetry-c12cf174e3e0cbb8.jsonl'}
LIMIT = 32767
BAD_START = set('=+-@')


def pyval(x):
    """Parse JSON to plain Python values (doubles as float) for value-level comparison."""
    return json.loads(x, parse_constant=lambda c: c)


def same(a, b):
    if type(a) is not type(b):
        return False
    if isinstance(a, dict):
        return list(a) == list(b) and all(same(a[k], b[k]) for k in a)
    if isinstance(a, list):
        return len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    if isinstance(a, float):
        return a == b or (math.isnan(a) and math.isnan(b))  # no NaN numbers in JSON; kept for safety
    return a == b


def pct(v, q):
    s = sorted(v)
    k = (len(s) - 1) * q
    f = math.floor(k)
    c = math.ceil(k)
    return s[f] if f == c else s[f] + (s[c] - s[f]) * (k - f)


res = {}
for lab, rel in FILES.items():
    path = os.path.join(CORPUS, rel)
    outdir = os.path.join(HERE, 'out')
    os.makedirs(outdir, exist_ok=True)
    cells_f = open(os.path.join(outdir, lab + '.cells.txt'), 'w', newline='\n')
    sizes, ratios, nS, mism, byte_ok, val_ok, n = [], [], [], [], 0, 0, 0
    bad_start = 0
    chars = set()
    for ln, line in enumerate(open(path, encoding='utf-8'), 1):
        line = line.rstrip('\n')
        if not line.strip():
            continue
        n += 1
        try:
            cell = codec.encode_line(line)
        except Exception as e:
            mism.append({'line': ln, 'stage': 'encode', 'err': repr(e)[:300]})
            continue
        cells_f.write(cell + '\n')
        sizes.append((len(cell), ln))
        ratios.append(len(cell) / len(line))
        nS.append(len(json.loads(line)['settlements']))
        if cell[0] in BAD_START:
            bad_start += 1
        chars |= set(cell)
        try:
            back = codec.decode_to_line(cell)
        except Exception as e:
            mism.append({'line': ln, 'stage': 'decode', 'err': repr(e)[:300]})
            continue
        if back == line:
            byte_ok += 1
        if same(pyval(back), pyval(line)):
            val_ok += 1
        else:
            mism.append({'line': ln, 'stage': 'compare'})
    cells_f.close()
    vals = [s for s, _ in sizes]
    mx = max(sizes)
    res[lab] = {
        'file': path, 'lines': n, 'encoded': len(sizes), 'byte_identical': byte_ok, 'value_equal': val_ok,
        'mismatches': mism[:50], 'mismatch_count': len(mism),
        'max': mx[0], 'max_turn_line': mx[1], 'median': statistics.median(vals), 'p95': pct(vals, 0.95),
        'min': min(vals), 'total_chars': sum(vals),
        'ratio_median': statistics.median(ratios), 'ratio_max': max(ratios), 'ratio_min': min(ratios),
        'over_limit': sum(1 for v in vals if v > LIMIT), 'bad_start': bad_start,
        'charset': ''.join(sorted(chars)),
        'sizes_by_S': {},
    }
    byS = {}
    for (sz, ln), s in zip(sizes, nS):
        byS.setdefault(s, []).append(sz)
    res[lab]['sizes_by_S'] = {s: [min(v), max(v), len(v)] for s, v in sorted(byS.items())}
    # least squares size ~ S
    xs, ys = nS, vals
    mxs, mys = sum(xs) / len(xs), sum(ys) / len(ys)
    sxx = sum((x - mxs) ** 2 for x in xs)
    sxy = sum((x - mxs) * (y - mys) for x, y in zip(xs, ys))
    b = sxy / sxx
    a = mys - b * mxs
    ss_res = sum((y - (a + b * x)) ** 2 for x, y in zip(xs, ys))
    ss_tot = sum((y - mys) ** 2 for y in ys)
    res[lab]['fit_S'] = {'intercept': a, 'slope': b, 'r2': 1 - ss_res / ss_tot}
    # per-turn fit
    ts = [ln for _, ln in sizes]
    mt = sum(ts) / len(ts)
    stt = sum((t - mt) ** 2 for t in ts)
    sty = sum((t - mt) * (y - mys) for t, y in zip(ts, ys))
    bt = sty / stt
    at = mys - bt * mt
    ss_res_t = sum((y - (at + bt * t)) ** 2 for t, y in zip(ts, ys))
    res[lab]['fit_turn'] = {'intercept': at, 'slope': bt, 'r2': 1 - ss_res_t / ss_tot}
    print(lab, {k: v for k, v in res[lab].items() if k not in ('mismatches', 'sizes_by_S')})
json.dump(res, open(os.path.join(HERE, 'results.json'), 'w'), indent=1)

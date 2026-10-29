"""tuple codec: telemetry JSONL line (v2-v5)  <->  one Excel cell of positional tuples.

Codebook: v4 for telemetry/v2, v3 and v4 lines (cell head v2/v3/v4); v5 for telemetry/v5
(cell head v5; D-066 A15). The codebook in force for a call is _CB, chosen from the line's
schema tag (encode) or the cell's first token (decode).

Cell grammar (see codebook.tsv for every code):
  cell     := turnSeg ( '|' settSeg )*
  turnSeg  := schemaCode ( ' ' section )*             e.g.  v4 t:289,2695,5 k:... A:...
  settSeg  := '#' tuple ( ' ' section )*              e.g.  #5,,3 P:1311,1307,... Eg:...
  section  := code ':' payload  |  code '~'           ('~' = object absent / table empty)
  tuple    := field ( ',' field )*                    trailing empty fields are dropped
  table    := row ( '/' row )*  |  '*' N              ('*N' = N rows, every field default)
  row      := field ( ',' field )*
  field    := ''  (= the codebook default for that position)
            | int | double | 'N' | 'I' | '-I' | '1'/'0' (bool) | enumCode
            | dbl ( ';' dbl )*  (double array)  | '~' (empty array / empty legs)
            | leg ( ';' leg )*  (stock legs: reason['+'srcUnits]['-'sinkUnits])
            | '!' JSON-literal  (escape: anything the codebook cannot express)
  doubles: the writer's own shortest round-trip text, with a leading '0.' written as '.'
           ('-0.' as '-.'); exponent form 1E-05 / 1E+15 kept as written.
  A section left out = every field in it is at its default.
"""
import json
import re

import spec as SP

_CB = SP.V4   # the codebook in force (set by encode_line / decode_cell)
ABSENT_LEAF = ('absent', None)   # an OPTIONAL key that is not in the line (codebook v5)

INT_RE = re.compile(r'-?\d+$')
LEG_RE = re.compile(r'^(-?\d+)(?:\+(-?\d+))?(?:-(-?\d+))?$')
SEC_RE = re.compile(r'^(#|[A-Za-z]+)(:|~)?(.*)$', re.S)

SEP_ESC = {',': '\\u002c', ';': '\\u003b', '/': '\\u002f', '|': '\\u007c', ' ': '\\u0020',
           '~': '\\u007e', '*': '\\u002a', '_': '\\u005f'}


class EncodeError(Exception):
    pass


# ---------------------------------------------------------------- raw JSON with typed leaves
def _num(t):
    return ('n', t)


def parse_json(text):
    """Parse JSON keeping number tokens verbatim; leaves become typed tuples."""
    def conv(v):
        if isinstance(v, dict):
            return {k: conv(x) for k, x in v.items()}
        if isinstance(v, list):
            return [conv(x) for x in v]
        if isinstance(v, tuple):
            return v
        if isinstance(v, bool):
            return ('b', v)
        if isinstance(v, str):
            return ('s', v)
        if v is None:
            return ('null', None)
        raise TypeError(type(v))
    raw = json.loads(text, parse_int=_num, parse_float=_num,
                     parse_constant=lambda c: (_ for _ in ()).throw(ValueError(c)))
    return conv(raw)


_HTML = set('<>&\'+`"')


def enc_str(s):
    out = ['"']
    for ch in s:
        o = ord(ch)
        if ch == '\\':
            out.append('\\\\')
        elif ch in _HTML:
            out.append('\\u%04X' % o)
        elif ch == '\n':
            out.append('\\n')
        elif ch == '\r':
            out.append('\\r')
        elif ch == '\t':
            out.append('\\t')
        elif ch == '\b':
            out.append('\\b')
        elif ch == '\f':
            out.append('\\f')
        elif o < 0x20 or o > 0x7E:
            if o > 0xFFFF:
                o -= 0x10000
                out.append('\\u%04X\\u%04X' % (0xD800 + (o >> 10), 0xDC00 + (o & 0x3FF)))
            else:
                out.append('\\u%04X' % o)
        else:
            out.append(ch)
    out.append('"')
    return ''.join(out)


def to_json(v):
    """Serialize typed JSON the way Utf8JsonWriter (compact, default encoder) does."""
    if isinstance(v, dict):
        return '{' + ','.join(enc_str(k) + ':' + to_json(x) for k, x in v.items()) + '}'
    if isinstance(v, list):
        return '[' + ','.join(to_json(x) for x in v) + ']'
    kind, x = v
    if kind == 'n':
        return x
    if kind == 's':
        return enc_str(x)
    if kind == 'b':
        return 'true' if x else 'false'
    if kind == 'null':
        return 'null'
    raise TypeError(v)


def _scan(s, fn):
    """Apply fn(ch, in_string) to each char of JSON text, tracking string state."""
    out = []
    in_str = False
    prev_bs = False
    for ch in s:
        out.append(fn(ch, in_str))
        if ch == '"' and not prev_bs:
            in_str = not in_str
        prev_bs = (ch == '\\' and not prev_bs) if in_str else False
    return ''.join(out)


def bang(v):
    """'!' escape: a JSON literal. Inside strings the cell separators are \\u-escaped;
    outside strings the structural ',' is written as ';' (JSON never has ';' outside strings)."""
    return '!' + _scan(to_json(v), lambda ch, ins: SEP_ESC.get(ch, ch) if ins else (';' if ch == ',' else ch))


def unbang(text):
    return parse_json(_scan(text[1:], lambda ch, ins: ',' if (ch == ';' and not ins) else ch))


# ---------------------------------------------------------------- value text
def enc_double(leaf):
    kind, x = leaf
    if kind == 'n':
        if x.startswith('0.'):
            return x[1:]
        if x.startswith('-0.'):
            return '-' + x[2:]
        return x
    if kind == 's' and x in SP.NONFINITE:
        return SP.NONFINITE[x]
    return None


NONFINITE_REV = {v: k for k, v in SP.NONFINITE.items()}


def dec_double(t):
    if t in NONFINITE_REV:
        return ('s', NONFINITE_REV[t])
    if t.startswith('.'):
        return ('n', '0' + t)
    if t.startswith('-.'):
        return ('n', '-0' + t[1:])
    return ('n', t)


def enc_legs(src, snk):
    if not isinstance(src, list) or not isinstance(snk, list) or len(src) != len(snk):
        return None
    if not src:
        return '~'
    legs = []
    for a, b in zip(src, snk):
        for row in (a, b):
            if not isinstance(row, dict) or list(row) != ['reason', 'name', 'units']:
                return None
            if row['reason'][0] != 'n' or not INT_RE.match(row['reason'][1]):
                return None
            if row['units'][0] != 'n' or not INT_RE.match(row['units'][1]):
                return None
            if row['name'] != ('s', name_of('reason', int(row['reason'][1]))):
                return None
        if a['reason'] != b['reason']:
            return None
        t = a['reason'][1]
        if a['units'][1] != '0':
            t += '+' + a['units'][1]
        if b['units'][1] != '0':
            t += '-' + b['units'][1]
        legs.append(t)
    return ';'.join(legs)


def dec_legs(t):
    if t == '~':
        return [], []
    src, snk = [], []
    for leg in t.split(';'):
        m = LEG_RE.match(leg)
        if not m:
            raise ValueError('bad leg ' + leg)
        r = m.group(1)
        nm = ('s', name_of('reason', int(r)))
        src.append({'reason': ('n', r), 'name': nm, 'units': ('n', m.group(2) or '0')})
        snk.append({'reason': ('n', r), 'name': nm, 'units': ('n', m.group(3) or '0')})
    return src, snk


def name_of(mapname, ident):
    m, fallback = _CB.NAME_MAPS[mapname]
    if ident in m:
        return m[ident]
    if fallback:
        return fallback % ident
    return None


def enc_val(typ, v):
    """Explicit text of a value; falls back to the '!' JSON escape."""
    t = None
    if typ == 'i':
        if isinstance(v, tuple) and v[0] == 'n' and INT_RE.match(v[1]):
            t = v[1]
    elif typ == 'd':
        if isinstance(v, tuple):
            t = enc_double(v)
    elif typ == 'b':
        if isinstance(v, tuple) and v[0] == 'b':
            t = '1' if v[1] else '0'
    elif typ.startswith('e:'):
        if isinstance(v, tuple) and v[0] == 's':
            t = _CB.ENUMS[typ[2:]].get(v[1])
    elif typ == 'da':
        if isinstance(v, list):
            parts = [enc_double(x) if isinstance(x, tuple) else None for x in v]
            if None not in parts:
                t = ';'.join(parts) if parts else '~'
    elif typ == 'legs':
        t = enc_legs(*v)
        if t is None:
            return bang(list(v))
        return t
    # 'c' and 'n:*' have no short form: default or escape
    if t is None:
        return bang(v)
    return t


def dec_val(typ, t):
    if t.startswith('!'):
        v = unbang(t)
        if typ == 'legs':
            return (v[0], v[1])
        return v
    if typ == 'i':
        if not INT_RE.match(t):
            raise ValueError('bad int ' + t)
        return ('n', t)
    if typ == 'd':
        return dec_double(t)
    if typ == 'b':
        return ('b', t == '1')
    if typ.startswith('e:'):
        rev = {c: n for n, c in _CB.ENUMS[typ[2:]].items()}
        return ('s', rev[t])
    if typ == 'da':
        return [] if t == '~' else [dec_double(x) for x in t.split(';')]
    if typ == 'legs':
        return dec_legs(t)
    raise ValueError('no short form for type %s: %r' % (typ, t))


# ---------------------------------------------------------------- defaults
class Ctx:
    def __init__(self, T=None, S=None, R=None, r=None):
        self.T, self.S, self.R, self.r = T, S, R, r


def get_path(ctx, path, resolve):
    def sub(m):
        e = m.group(1)
        return str(ctx.r + (int(e[2:]) if e.startswith('r+') else 0))
    path = re.sub(r'\{(r(?:\+\d+)?)\}', sub, path)
    parts = path.split('.')
    cur = {'T': ctx.T, 'S': ctx.S, 'R': ctx.R}[parts[0]]
    for p in parts[1:]:
        if cur is None:
            return None
        cur = resolve(cur)
        if isinstance(cur, dict):
            cur = cur.get(p)
        elif isinstance(cur, list):
            i = int(p)
            cur = cur[i] if 0 <= i < len(cur) else None
        else:
            return None
    if cur is None:
        return None
    return resolve(cur)


def default_value(dspec, ctx, resolve):
    """Evaluate a default spec. resolve() turns pending placeholders into values (decoder)."""
    if dspec is None:
        return None
    k = dspec[0]
    if k == 'k':
        return dspec[1]
    if k == 'r':
        return get_path(ctx, dspec[1], resolve)
    if k == 'pos':
        lst = dspec[1]
        return lst[ctx.r] if ctx.r is not None and ctx.r < len(lst) else None
    if k == 'idx':
        return ('n', str(ctx.r))
    if k == 'nm':
        idv = get_path(ctx, 'R.' + dspec[1], resolve)
        if not (isinstance(idv, tuple) and idv[0] == 'n' and INT_RE.match(idv[1])):
            return None
        n = name_of(dspec[2], int(idv[1]))
        return None if n is None else ('s', n)
    if k == 'rowd':
        sub = dspec[1].get(ctx.r, dspec[2])
        return default_value(sub, ctx, resolve)
    if k == 'elems':
        out = []
        for e in dspec[1]:
            v = default_value(e, ctx, resolve)
            if v is None:
                return None
            out.append(v)
        return out
    if k == 'absent':
        return ABSENT_LEAF
    if k == 'own_attr':
        sid = get_path(ctx, 'S.settlement', resolve)
        lst = get_path(ctx, 'S.migration.allAttractiveness', resolve)
        if not isinstance(lst, list):
            return None
        for row in lst:
            row = resolve(row)
            if resolve(row.get('settlement')) == sid:
                return resolve(row.get('value'))
        return None
    raise ValueError(dspec)


def ident(x):
    return x


# ---------------------------------------------------------------- encoder
def _check_keys(obj, keys, where):
    if not isinstance(obj, dict) or list(obj.keys()) != list(keys):
        raise EncodeError('key order/shape differs at %s: %r' % (
            where, list(obj.keys()) if isinstance(obj, dict) else type(obj).__name__))


def _fields_text(fields, getter, ctx):
    texts = []
    for key, typ, dspec in fields:
        v = getter(key)
        d = default_value(dspec, ctx, ident)
        if d is not None and v == d:
            texts.append('')
        else:
            texts.append(enc_val(typ, v))
    while texts and texts[-1] == '':
        texts.pop()
    return ','.join(texts)


def _row_getter(row):
    def g(key):
        if isinstance(key, tuple):
            return tuple(row[k] for k in key)
        return row[key]
    return g


def enc_table(code, secs, arr, ctx, table_default):
    _, rowkeys, fields, _tdef = secs[code]
    if not isinstance(arr, list):
        raise EncodeError('not an array: ' + code)
    if table_default is not None and arr == table_default:
        return None
    if not arr:
        return code + '~'   # empty, but the default was not empty
    rows = []
    for r, row in enumerate(arr):
        _check_keys(row, rowkeys, code + '[%d]' % r)
        rows.append(_fields_text(fields, _row_getter(row), Ctx(ctx.T, ctx.S, row, r)))
    if all(x == '' for x in rows):
        return code + ':*' + str(len(rows))
    return code + ':' + '/'.join(rows)


def _collect(template, obj, where, leaves, tables, optional):
    """Walk a JSON object against its template; gather leaves per section."""
    keys = [e[0] for e in template]
    _check_keys(obj, keys, where)
    for e in template:
        key = e[0]
        if len(e) == 2 and isinstance(e[1], str):
            leaves.setdefault(e[1], {})[key] = obj[key]
        elif len(e) == 2:
            _collect(e[1], obj[key], where + '.' + key, leaves, tables, optional)
        elif e[1] == '@':
            tables[e[2]] = obj[key]
        elif e[1] == '?':
            raise AssertionError
    return leaves


def _collect_opt(template, obj, where, leaves, tables, optional):
    # like _collect but allows '?' optional objects (absent key)
    tkeys = [e[0] for e in template]
    present = [k for k in tkeys if not (_is_opt(template, k) and k not in obj)]
    _check_keys(obj, present, where)
    for e in template:
        key = e[0]
        if len(e) == 2 and isinstance(e[1], str):
            leaves.setdefault(e[1], {})[key] = obj[key]
        elif len(e) == 3 and e[1] == 'opt':
            leaves.setdefault(e[2], {})[key] = obj.get(key, ABSENT_LEAF)
        elif len(e) == 2:
            _collect_opt(e[1], obj[key], where + '.' + key, leaves, tables, optional)
        elif e[1] == '@':
            tables[e[2]] = obj[key]
        elif e[1] == '?':
            if key in obj:
                optional[e[2]] = True
                _collect_opt(e[3], obj[key], where + '.' + key, leaves, tables, optional)
            else:
                optional[e[2]] = False


def _is_opt(template, key):
    for e in template:
        if e[0] == key:
            return (len(e) == 4 and e[1] == '?') or (len(e) == 3 and e[1] == 'opt')
    return False


def enc_segment(template, secs, order, obj, ctx, head, table_defaults):
    leaves, tables, optional = {}, {}, {}
    _collect_opt(template, obj, head, leaves, tables, optional)
    out = []
    for code in order:
        if code not in secs:
            continue
        sec = secs[code]
        if sec[0] == 'tuple':
            if optional.get(code) is False:
                out.append(code + '~')
                continue
            vals = leaves.get(code, {})
            txt = _fields_text(sec[1], lambda k: vals[k], ctx)
            if code == '#':
                out.append('#' + txt)
            elif txt != '':
                out.append(code + ':' + txt)
        else:
            t = enc_table(code, secs, tables[code], ctx, table_defaults.get(code, []))
            if t is not None:
                out.append(t)
    return out


def encode_line(line):
    global _CB
    doc = parse_json(line)
    _check_keys(doc, ['schema', 'turn', 'settlements'], 'line')
    sch = doc['schema']
    cb = SP.codebook_for_schema(sch[1]) if sch[0] == 's' else None
    _CB = cb or SP.V4
    if cb is not None:
        head = cb.SCHEMA_CODES[sch[1]]
    else:
        head = bang(sch)
    T = doc['turn']
    setts = doc['settlements']
    if not isinstance(setts, list):
        raise EncodeError('settlements not a list')
    # line-level attractiveness table: the most common per-record list
    lineA = []
    cand = {}
    for s in setts:
        try:
            lst = s['migration']['allAttractiveness']
        except (KeyError, TypeError):
            continue
        key = to_json(lst)
        if key not in cand:
            cand[key] = [0, lst]
        cand[key][0] += 1
    if cand:
        best = max(cand.values(), key=lambda c: c[0])  # first max wins (dict order)
        lineA = best[1]
    secs_t = dict(_CB.TURN_SECTIONS)
    tctx = Ctx(T=T)
    parts = [head] + enc_segment(_CB.TURN_TEMPLATE, secs_t, _CB.TURN_SECTION_ORDER, T, tctx, 'turn', {})
    if lineA:
        secs_a = {'A': ('table',) + _CB.ATTR_ROW + ('empty',)}
        a = enc_table('A', secs_a, lineA, tctx, [])
        if a is not None:
            parts.append(a)
    segs = [' '.join(parts)]
    t_orders = T['orders']
    for i, s in enumerate(setts):
        ctx = Ctx(T=T, S=s)
        sid = s.get('settlement') if isinstance(s, dict) else None
        mine = [o for o in t_orders if isinstance(o, dict) and o.get('settlement') == sid]
        tdefs = {'Ma': lineA, 'O': mine}
        segs.append(' '.join(enc_segment(_CB.SETTLEMENT_TEMPLATE, _CB.SETTLEMENT_SECTIONS,
                                         _CB.SETTLEMENT_SECTION_ORDER, s, ctx, 'settlements[%d]' % i,
                                         tdefs)))
    return '|'.join(segs)


# ---------------------------------------------------------------- decoder
class Pending:
    __slots__ = ('dspec', 'ctx', 'busy', 'val')

    def __init__(self, dspec, ctx):
        self.dspec, self.ctx, self.busy, self.val = dspec, ctx, False, None


def resolve(x):
    if isinstance(x, Pending):
        if x.val is not None:
            return x.val
        if x.busy:
            raise ValueError('cyclic default')
        x.busy = True
        v = default_value(x.dspec, x.ctx, resolve)
        if v is None:
            raise ValueError('default unavailable: %r' % (x.dspec,))
        x.val = v
        return v
    return x


def deep_resolve(x):
    x = resolve(x)
    if isinstance(x, dict):
        for k in list(x):
            x[k] = deep_resolve(x[k])
        return x
    if isinstance(x, list):
        for i in range(len(x)):
            x[i] = deep_resolve(x[i])
        return x
    return x


def strip_absent(x):
    """Drop OPTIONAL keys whose field was empty (the key was not in the line)."""
    if isinstance(x, dict):
        for k in [k for k, v in x.items() if v == ABSENT_LEAF]:
            del x[k]
        for v in x.values():
            strip_absent(v)
    elif isinstance(x, list):
        for v in x:
            strip_absent(v)
    return x


def deep_copy(x):
    if isinstance(x, dict):
        return {k: deep_copy(v) for k, v in x.items()}
    if isinstance(x, list):
        return [deep_copy(v) for v in x]
    return x


def split_fields(payload):
    return payload.split(',') if payload != '' else []


def dec_tuple_fields(fields, payload, ctx, target):
    texts = split_fields(payload)
    if len(texts) > len(fields):
        raise ValueError('too many fields')
    texts += [''] * (len(fields) - len(texts))
    for (key, typ, dspec), t in zip(fields, texts):
        if t == '':
            if dspec is None:
                raise ValueError('empty field without default: %s' % (key,))
            target[key] = Pending(dspec, ctx)
        else:
            v = dec_val(typ, t)
            if isinstance(key, tuple):
                for kk, vv in zip(key, v):
                    target[kk] = vv
            else:
                target[key] = v


def dec_table(sec, payload, ctx):
    _, rowkeys, fields, _ = sec
    if payload.startswith('*'):
        rows_txt = [''] * int(payload[1:])
    else:
        rows_txt = payload.split('/')
    rows = []
    for r, rt in enumerate(rows_txt):
        vals = {}
        row = {}
        rctx = Ctx(ctx.T, ctx.S, row, r)
        dec_tuple_fields(fields, rt, rctx, vals)
        for k in rowkeys:
            row[k] = vals[k]
        rows.append(row)
    return rows


def parse_segment(seg):
    secs = {}
    toks = seg.split(' ')
    for tok in toks:
        m = SEC_RE.match(tok)
        if not m:
            raise ValueError('bad section ' + tok[:20])
        code, mark, payload = m.group(1), m.group(2), m.group(3)
        if code == '#':
            secs['#'] = ('val', (mark or '') + payload)
        elif mark == '~':
            secs[code] = ('absent', None)
        else:
            secs[code] = ('val', payload)
    return secs


def build(template, secs_spec, parsed, ctx, table_pending, holder, obj=None):
    if obj is None:
        obj = {}
    for e in template:
        key = e[0]
        if (len(e) == 2 and isinstance(e[1], str)) or (len(e) == 3 and e[1] == 'opt'):
            code = e[1] if len(e) == 2 else e[2]
            vals = holder.setdefault(code, None)
            if vals is None:
                vals = {}
                holder[code] = vals
                p = parsed.get(code)
                payload = p[1] if p and p[0] == 'val' else ''
                dec_tuple_fields(secs_spec[code][1], payload, ctx, vals)
            obj[key] = vals[key]
        elif len(e) == 2:
            obj[key] = build(e[1], secs_spec, parsed, ctx, table_pending, holder)
        elif e[1] == '@':
            code = e[2]
            p = parsed.get(code)
            if p is None:
                obj[key] = []
                table_pending.append((obj, key, code))
            elif p[0] == 'absent':
                obj[key] = []
            else:
                obj[key] = dec_table(secs_spec[code], p[1], ctx)
        elif e[1] == '?':
            p = parsed.get(e[2])
            if p is not None and p[0] == 'absent':
                continue
            obj[key] = build(e[3], secs_spec, parsed, ctx, table_pending, holder)
    return obj


def decode_cell(cell):
    global _CB
    segs = cell.split('|')
    turn_toks = segs[0].split(' ', 1)
    head = turn_toks[0]
    cb = None if head.startswith('!') else SP.codebook_for_head(head)
    if cb is None and not head.startswith('!'):
        raise ValueError('unknown cell head ' + head[:20])
    _CB = cb or SP.V4
    rev = {v: k for k, v in _CB.SCHEMA_CODES.items()}
    schema = unbang(head) if head.startswith('!') else ('s', rev[head])
    tparsed = parse_segment(turn_toks[1]) if len(turn_toks) > 1 else {}
    T = {}
    tctx = Ctx(T=T)
    tp = []
    build(_CB.TURN_TEMPLATE, _CB.TURN_SECTIONS, tparsed, tctx, tp, {}, T)
    deep_resolve(T)
    lineA = []
    if 'A' in tparsed and tparsed['A'][0] == 'val':
        lineA = dec_table(('table',) + _CB.ATTR_ROW + ('empty',), tparsed['A'][1], tctx)
        deep_resolve(lineA)
    setts = []
    for seg in segs[1:]:
        parsed = parse_segment(seg)
        S = {}
        ctx = Ctx(T=T, S=S)
        tp = []
        build(_CB.SETTLEMENT_TEMPLATE, _CB.SETTLEMENT_SECTIONS, parsed, ctx, tp, {}, S)
        sid = resolve(S['settlement'])
        for obj, key, code in tp:
            if code == 'Ma':
                obj[key] = deep_copy(lineA)
            elif code == 'O':
                obj[key] = [deep_copy(o) for o in T['orders'] if o['settlement'] == sid]
        deep_resolve(S)
        setts.append(S)
    strip_absent(T)
    for S in setts:
        strip_absent(S)
    return {'schema': schema, 'turn': T, 'settlements': setts}


def decode_to_line(cell):
    return to_json(decode_cell(cell))


# ---------------------------------------------------------------- continuation cells (D-066 A17)
def split_cell(cell, limit=SP.CELL_LIMIT):
    """Split a coded cell into workbook cells of at most `limit` characters, at '|' only.
    The first part is the cell's head; every further part is a CONTINUATION: the marker '>' and
    then the cut '|'. Greedy: each part takes as many whole segments as fit. A single segment
    longer than the limit cannot be split and is refused (never truncated)."""
    segs = cell.split('|')
    cur = segs[0]
    if len(cur) > limit:
        raise EncodeError('the turn segment is %d characters, over the cell limit %d' % (len(cur), limit))
    parts = []
    for seg in segs[1:]:
        if len(cur) + 1 + len(seg) <= limit:
            cur += '|' + seg
        else:
            parts.append(cur)
            cur = SP.CONTINUATION_MARK + '|' + seg
            if len(cur) > limit:
                raise EncodeError('a settlement segment is %d characters, over the cell limit %d'
                                  % (len(seg), limit))
    parts.append(cur)
    return parts


def join_cells(parts):
    """The inverse of split_cell: drop each continuation's marker and append it to the cell above."""
    out = parts[0]
    for p in parts[1:]:
        if not p.startswith(SP.CONTINUATION_MARK):
            raise ValueError('not a continuation cell: ' + p[:20])
        out += p[len(SP.CONTINUATION_MARK):]
    return out

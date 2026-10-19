"""Generate codebook.tsv (the workbook's codebook sheet) from spec.py."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import spec as SP

HERE = os.path.dirname(os.path.abspath(__file__))
CODE_OF_PATH = {}


def section_paths(template, prefix, out):
    for e in template:
        key = e[0]
        if len(e) == 2 and isinstance(e[1], str):
            out.setdefault(e[1], prefix.rstrip('.'))
        elif len(e) == 2:
            section_paths(e[1], prefix + key + '.', out)
        elif e[1] == '@':
            out[e[2]] = prefix + key + '[]'
        elif e[1] == '?':
            out[e[2]] = prefix + key + ' (OPTIONAL object)'
            section_paths(e[3], prefix + key + '.', out)
    return out


TPATH = section_paths(SP.TURN_TEMPLATE, 'turn.', {})
SPATH = section_paths(SP.SETTLEMENT_TEMPLATE, 'settlements[].', {})
SPATH['#'] = 'settlements[] (identity)'
TPATH['t'] = 'turn'


def leaf_txt(v):
    k, x = v
    if k == 'n':
        return x
    if k == 'b':
        return '1' if x else '0'
    if k == 's':
        if len(x) > 40:
            for i, c in enumerate((SP.C1, SP.C2, SP.C3, SP.C4, SP.C5), 1):
                if x == c:
                    return 'constant C%d' % i
        return x
    return str(x)


def dtxt(d, codes):
    if d is None:
        return '(always written)'
    k = d[0]
    if k == 'k':
        return leaf_txt(d[1])
    if k == 'r':
        return '= ' + pretty_ref(d[1], codes)
    if k == 'pos':
        return 'by row: ' + ' '.join(leaf_txt(v) for v in d[1])
    if k == 'idx':
        return 'row number (0,1,2...)'
    if k == 'nm':
        return 'name of the %s id (Names table)' % d[2]
    if k == 'rowd':
        parts = ['row %d: %s' % (r, dtxt(s, codes)) for r, s in sorted(d[1].items())]
        return '; '.join(parts) + '; other rows: ' + dtxt(d[2], codes)
    if k == 'elems':
        return '[' + ';'.join(dtxt(e, codes) for e in d[1]) + ']'
    if k == 'own_attr':
        return "this settlement's value in its attractiveness list (Ma / A)"
    return str(d)


def pretty_ref(p, codes):
    p = p.replace('{r+3}', '(row+3)').replace('{r}', '(row)')
    if p.startswith('R.'):
        return 'same row: ' + p[2:]
    if p.startswith('T.'):
        return 'turn.' + p[2:]
    if p.startswith('S.'):
        return 'this settlement: ' + p[2:]
    return p


TYPES = {'i': 'integer', 'd': 'double', 'b': 'bool 1/0', 'c': 'constant text', 'da': 'doubles joined by ;',
         'legs': 'ledger legs', 'e:kind': 'order kind code', 'e:state': 'food state code',
         'e:famine': 'famine reason code', 'n:quantity': 'name', 'n:good': 'name', 'n:class': 'name',
         'n:need': 'name', 'n:reason': 'name'}

rows = [('sheet', 'code', 'pos', 'json field', 'type', 'default when the field is EMPTY', 'notes')]
R = rows.append

R(('grammar', '', '', '', '', '', 'A cell is one telemetry line (one turn). Segments are split by |. Segment 1 is the turn; '
   'each further segment is one settlement record, in file order.'))
R(('grammar', '', '', '', '', '', 'Inside a segment, sections are split by a space. A section is CODE:payload. '
   'CODE~ means that optional object is absent (St, X, Y) or that the array is empty (tables).'))
R(('grammar', '', '', '', '', '', 'A section that is missing entirely = every field in it has its default '
   '(for a table with no default rows: an empty array).'))
R(('grammar', '', '', '', '', '', 'Tuple payload: values split by , in the "pos" order below. An EMPTY value means '
   'the default. Trailing empty values are dropped.'))
R(('grammar', '', '', '', '', '', 'Table payload: rows split by /, values inside a row split by ,. *N = N rows that '
   'are all defaults.'))
R(('grammar', '', '', '', '', '', 'Numbers are the writer\'s exact text; a leading 0. is written as . (and -0. as -.). '
   'N = NaN, I = Infinity, -I = -Infinity. Bools: 1 true, 0 false.'))
R(('grammar', '', '', '', '', '', 'Double arrays: values split by ; (~ = empty array).'))
R(('grammar', '', '', '', '', '', 'Ledger legs (k table): reason[+sourceUnits][-sinkUnits] split by ; '
   '(missing units = 0; ~ = no legs). Each leg is one sources[] row and one sinks[] row with that reason.'))
R(('grammar', '', '', '', '', '', '!JSON = escape: the value is a JSON literal (used only when the codebook cannot '
   'express it; separators inside strings appear as \\u escapes).'))
R(('grammar', 'v4', '', 'schema', '', '', 'first token of the cell: v4 = telemetry/v4 (v3, v2 likewise)'))
R(('grammar', 'A', '', 'settlements[].migration.allAttractiveness (line level)', '', '',
   'written once per turn segment; each record\'s Ma table defaults to it. Rows: value,settlement'))


def emit(scope, sections, paths, order):
    for code in order:
        if code not in sections:
            continue
        sec = sections[code]
        if sec[0] == 'tuple':
            fields = sec[1]
            R((scope, code, '', paths.get(code, ''), 'tuple', '', ''))
        else:
            fields = sec[2]
            tdef = sec[3]
            note = {'empty': 'default: empty array',
                    'lineA': 'default: the A table of the turn segment',
                    'myorders': 'default: the rows of turn.orders (o) whose settlement = this settlement'}[tdef]
            R((scope, code, '', paths.get(code, ''), 'table', note, 'row JSON key order: ' + ','.join(sec[1])))
        for i, (key, typ, d) in enumerate(fields, 1):
            k = '+'.join(key) if isinstance(key, tuple) else key
            R((scope, code, str(i), k, TYPES.get(typ, typ), dtxt(d, None), ''))


emit('turn', SP.TURN_SECTIONS, TPATH, SP.TURN_SECTION_ORDER)
emit('turn', {'A': ('table',) + SP.ATTR_ROW + ('empty',)}, {'A': 'line-level attractiveness'}, ['A'])
emit('settlement', SP.SETTLEMENT_SECTIONS, SPATH, SP.SETTLEMENT_SECTION_ORDER)

for mapname, (m, fb) in SP.NAME_MAPS.items():
    for k, v in sorted(m.items()):
        R(('names', mapname, str(k), v, '', '', ''))
    if fb:
        R(('names', mapname, 'other', fb.replace('%d', 'N'), '', '', 'fallback'))
for en, m in SP.ENUMS.items():
    for name, code in m.items():
        R(('enums', en, code, name, '', '', ''))
for i, c in enumerate((SP.C1, SP.C2, SP.C3, SP.C4, SP.C5), 1):
    R(('constants', 'C%d' % i, '', c, '', '', ''))

with open(os.path.join(HERE, 'codebook.tsv'), 'w', encoding='utf-8', newline='\n') as f:
    for r in rows:
        f.write('\t'.join(x.replace('\t', ' ') for x in r) + '\n')
print(len(rows) - 1, 'rows', os.path.getsize(os.path.join(HERE, 'codebook.tsv')), 'bytes')

"""Generate the codebooks (the workbook's Codebook sheet, and its machine-readable twin) from spec.py.

    python3 -I make_codebook.py            # writes ../codebook-telemetry-v4.tsv, -v5.tsv, -v4.json, -v5.json
    python3 -I make_codebook.py --check    # exit 1 if any of the four files differs from what spec.py gives

spec.py is the single source. The TSV is the table a reader (or a reading AI) decodes a cell with;
the JSON carries the same codebook for a program (the C# exporter embeds both, ADR-059). The v4 TSV
is byte-identical to the one first published on 2026-10-19.
"""
import json
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import spec as SP

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..'))


def section_paths(template, prefix, out):
    for e in template:
        key = e[0]
        if len(e) == 2 and isinstance(e[1], str):
            out.setdefault(e[1], prefix.rstrip('.'))
        elif len(e) == 2:
            section_paths(e[1], prefix + key + '.', out)
        elif e[1] == '@':
            out[e[2]] = prefix + key + '[]'
        elif e[1] == 'opt':
            out.setdefault(e[2], prefix.rstrip('.'))
        elif e[1] == '?':
            out[e[2]] = prefix + key + ' (OPTIONAL object)'
            section_paths(e[3], prefix + key + '.', out)
    return out


def leaf_txt(v, constants=None):
    k, x = v
    if k == 'n':
        return x
    if k == 'b':
        return '1' if x else '0'
    if k == 's':
        if len(x) > 40:
            for i, c in enumerate(constants or (SP.C1, SP.C2, SP.C3, SP.C4, SP.C5), 1):
                if x == c:
                    return 'constant C%d' % i
        return x
    return str(x)


def dtxt(d, codes):
    if d is None:
        return '(always written)'
    k = d[0]
    if k == 'k':
        return leaf_txt(d[1], codes)
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
    if k == 'absent':
        return '(key absent from the line)'
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


def tsv_rows(cb):
    """The Codebook sheet of one codebook, as rows of 7 text cells."""
    tpath = section_paths(cb.TURN_TEMPLATE, 'turn.', {})
    spath = section_paths(cb.SETTLEMENT_TEMPLATE, 'settlements[].', {})
    spath['#'] = 'settlements[] (identity)'
    tpath['t'] = 'turn'
    v5 = cb.version == 'v5'
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
    if v5:
        R(('grammar', '', '', '', '', '', 'OPTIONAL keys (default "(key absent from the line)"): an EMPTY value means the '
           'key is not in the line; a value means the key is there with that value.'))
        R(('grammar', '', '', '', '', '', 'Continuation (workbook, D-066 A17): a turn longer than 32767 characters is split '
           'at a | into further rows. A continuation row has an EMPTY column A and its cell starts with > followed by the '
           '| that was cut. Join: drop the > and append the rest to the cell above (repeat for each continuation row).'))
        R(('grammar', 'v5', '', 'schema', '', '', 'first token of the cell: v5 = telemetry/v5 (cells v2, v3 and v4 are '
           'read with codebook v4)'))
    else:
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
                R((scope, code, str(i), k, TYPES.get(typ, typ), dtxt(d, cb.CONSTANTS), ''))

    emit('turn', cb.TURN_SECTIONS, tpath, cb.TURN_SECTION_ORDER)
    emit('turn', {'A': ('table',) + cb.ATTR_ROW + ('empty',)}, {'A': 'line-level attractiveness'}, ['A'])
    emit('settlement', cb.SETTLEMENT_SECTIONS, spath, cb.SETTLEMENT_SECTION_ORDER)

    for mapname, (m, fb) in cb.NAME_MAPS.items():
        for k, v in sorted(m.items()):
            R(('names', mapname, str(k), v, '', '', ''))
        if fb:
            R(('names', mapname, 'other', fb.replace('%d', 'N'), '', '', 'fallback'))
    for en, m in cb.ENUMS.items():
        for name, code in m.items():
            R(('enums', en, code, name, '', '', ''))
    for i, c in enumerate(cb.CONSTANTS, 1):
        R(('constants', 'C%d' % i, '', c, '', '', ''))
    return rows


def tsv_text(cb):
    return ''.join('\t'.join(x.replace('\t', ' ') for x in r) + '\n' for r in tsv_rows(cb))


# ---------------------------------------------------------------- the machine-readable twin
def j_leaf(v):
    return [v[0], v[1]]


def j_default(d):
    if d is None:
        return None
    k = d[0]
    if k == 'k':
        return ['k', j_leaf(d[1])]
    if k == 'r':
        return ['r', d[1]]
    if k == 'pos':
        return ['pos', [j_leaf(v) for v in d[1]]]
    if k in ('idx', 'own_attr', 'absent'):
        return [k]
    if k == 'nm':
        return ['nm', d[1], d[2]]
    if k == 'rowd':
        return ['rowd', [[r, j_default(s)] for r, s in sorted(d[1].items())], j_default(d[2])]
    if k == 'elems':
        return ['elems', [j_default(e) for e in d[1]]]
    raise ValueError(d)


def j_fields(fields):
    return [[list(key) if isinstance(key, tuple) else key, typ, j_default(d)] for key, typ, d in fields]


def j_section(code, sec):
    if sec[0] == 'tuple':
        return {'code': code, 'kind': 'tuple', 'fields': j_fields(sec[1])}
    return {'code': code, 'kind': 'table', 'rowKeys': list(sec[1]), 'fields': j_fields(sec[2]),
            'tableDefault': sec[3]}


def j_template(template):
    out = []
    for e in template:
        key = e[0]
        if len(e) == 2 and isinstance(e[1], str):
            out.append(['leaf', key, e[1]])
        elif len(e) == 2:
            out.append(['obj', key, j_template(e[1])])
        elif e[1] == '@':
            out.append(['table', key, e[2]])
        elif e[1] == 'opt':
            out.append(['optleaf', key, e[2]])
        elif e[1] == '?':
            out.append(['opt', key, e[2], j_template(e[3])])
        else:
            raise ValueError(e)
    return out


def json_text(cb):
    doc = {
        'codebook': cb.version,
        'source': 'docs/m6-session-export/prototype/spec.py (generated by make_codebook.py; do not edit)',
        'schemaCodes': [[k, v] for k, v in cb.SCHEMA_CODES.items()],
        'nonFinite': [[k, v] for k, v in cb.NONFINITE.items()],
        'names': [{'map': name, 'fallback': fb, 'ids': [[k, v] for k, v in sorted(m.items())]}
                  for name, (m, fb) in cb.NAME_MAPS.items()],
        'enums': [{'enum': en, 'codes': [[n, c] for n, c in m.items()]} for en, m in cb.ENUMS.items()],
        'turnSections': [j_section(c, cb.TURN_SECTIONS[c]) for c in cb.TURN_SECTION_ORDER if c in cb.TURN_SECTIONS],
        'attrRow': {'code': 'A', 'kind': 'table', 'rowKeys': list(cb.ATTR_ROW[0]), 'fields': j_fields(cb.ATTR_ROW[1]),
                    'tableDefault': 'empty'},
        'settlementSections': [j_section(c, cb.SETTLEMENT_SECTIONS[c]) for c in cb.SETTLEMENT_SECTION_ORDER],
        'turnTemplate': j_template(cb.TURN_TEMPLATE),
        'settlementTemplate': j_template(cb.SETTLEMENT_TEMPLATE),
        'turnSectionOrder': list(cb.TURN_SECTION_ORDER),
        'settlementSectionOrder': list(cb.SETTLEMENT_SECTION_ORDER),
        'cellLimit': SP.CELL_LIMIT,
        'continuationMark': SP.CONTINUATION_MARK,
    }
    return json.dumps(doc, indent=1, ensure_ascii=True) + '\n'


def outputs():
    out = []
    for v in ('v4', 'v5'):
        cb = SP.CODEBOOKS[v]
        out.append(('codebook-telemetry-%s.tsv' % v, tsv_text(cb)))
        out.append(('codebook-telemetry-%s.json' % v, json_text(cb)))
    return out


def main(argv):
    check = '--check' in argv
    stale = []
    for name, text in outputs():
        path = os.path.join(OUT, name)
        data = text.encode('utf-8')
        if check:
            try:
                with open(path, 'rb') as f:
                    if f.read() != data:
                        stale.append(name)
            except FileNotFoundError:
                stale.append(name)
        else:
            with open(path, 'wb') as f:
                f.write(data)
            print(name, len(data), 'bytes')
    if check:
        if stale:
            print('STALE (run make_codebook.py):', ', '.join(stale))
            return 1
        print('codebooks current: v4, v5 (TSV + JSON)')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

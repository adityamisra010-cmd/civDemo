"""Consistency checks for the M7 unit and building charters (docs only). Exit 1 on any FAIL.

Usage: python3 -I docs/m7-plan/tools/check_charters.py <repo-root>
Checks: every backticked id exists (research nodes/entities, unit families/identities, baseline rows) or is a named
proposed family; the building charter §9 register lists every building/infra/project/inst entity exactly once;
identity keys cited as `id` (key) match unit-families.json; identity prerequisites name no node of a later Age;
relative links and anchors resolve; all 12 existing families are reconciled.
"""
import json, re, sys, os
root = sys.argv[1]
U = os.path.join(root, 'docs/m7-plan/unit-catalogue-and-progression.md')
B = os.path.join(root, 'docs/m7-plan/building-and-infrastructure-charter.md')
r = json.load(open(os.path.join(root, 'Sim.Data/content/research.json')))
uf = json.load(open(os.path.join(root, 'Sim.Data/content/unit-families.json')))
nodes = {n['id']: int(n['age'][1:]) for t in ('technologies', 'civics') for n in r[t]}
ents = {e['id'] for e in r['entities']}
fams = {f['id']: f['key'] for f in uf['families']}
idents = {i['id']: (i['key'], i['age'], f['id']) for f in uf['families'] for i in f['line']}
ikeys = {v[0]: (k, v[1], v[2]) for k, v in idents.items()}
baseline = {l.split('`')[1] for l in open(os.path.join(root, 'docs/research-gameplay-unlock-audit.md')) if l.startswith('| `baseline.')}
fails = []
proposed = set('combat_engineers air_defence military_supply field_medical naval_raiders submarines carriers rotary_wing unmanned_aircraft reconnaissance outriders'.split())
texts = {p: open(p).read() for p in (U, B)}
# 1. backticked tokens that look like ids
allowed_prefix = ('docs/', 'Sim.', 'sim.json', 'goods.json', 'research.json', 'unit-families.json', 'ages.json')
known_other = set('''MilitaryUnits UnitConversions ConstructionProgress Ledger.Flow Ledger.Transfer RngRegistry WorldState Structures Dictionary HashSet GetHashCode long dtYears
AiOrders AiConstructionPolicy AiFoundingPolicy FutureEffectsTests TreeClosureBaselineTests realizedBy requires noSuccessor lineEndsAfterAge age name
colonization.orderPartyFraction transport.riverCostFactor governance.authorityDecayCostUnits depositChannel sim.json granaries farming.preCultivation sim.json colonization
sim.json roads.classes sim.json trade WorldFounding.FoundInitialFormations MilitaryQuery.PlanFor NotableRow 5382b85 b769933 6e51a3e 997824b main
army_formation mobile_agent special_person cultural_group population_state asset UnitModernizationTests Sim.Core/State/WorldState.cs
entities Family historicalEmergence livestock dwellings orderPartyFraction deferred-entity-realization-plan milestones.md building-and-infrastructure-charter.md — ++ + 0 – ––
unit.* building.* infra.* project.* inst.* branch branches[]'''.split())
for p, t in texts.items():
    for tok in re.findall(r'`([^`\n]+)`', t):
        tok = tok.strip()
        if tok in known_other or tok.startswith(allowed_prefix) or ' ' in tok or '/' in tok or '(' in tok or '=' in tok or '→' in tok:
            continue
        if tok in proposed or tok in nodes or tok in ents or tok in fams or tok in idents or tok in baseline:
            continue
        fails.append(f'{os.path.basename(p)}: unknown id `{tok}`')
# 2. register completeness (building charter §9)
reg = texts[B].split('## 9. Entity status register')[1].split('## 10.')[0]
reg_ids = re.findall(r'^\| `((?:building|infra|project|inst)\.[a-z_]+)` \|', reg, re.M)
want = sorted(e for e in ents if e.split('.')[0] in ('building', 'infra', 'project', 'inst'))
dups = sorted({x for x in reg_ids if reg_ids.count(x) > 1})
miss = sorted(set(want) - set(reg_ids)); extra = sorted(set(reg_ids) - set(want))
if dups or miss or extra: fails.append(f'register: dups={dups} missing={miss} extra={extra}')
print(f'register: {len(reg_ids)} rows for {len(want)} entities')
# 3. identity keys cited as `id` (key)
for p, t in texts.items():
    for idt, key in re.findall(r'`([a-z_]+)` \((\d{3,4})\)', t):
        if idt in idents and idents[idt][0] != int(key):
            fails.append(f'{os.path.basename(p)}: `{idt}` cited as {key}, data says {idents[idt][0]}')
# 4. prerequisite ages: in unit charter Prerequisites rows, "KEY node-expr" segments
for row in re.findall(r'^\| Prerequisites \| (.*)$', texts[U], re.M):
    for seg in re.split(r'(?=\b\d{3,4}\b)', row):
        m = re.match(r'(\d{3,4})\b(.*)', seg, re.S)
        if not m: continue
        k = int(m.group(1))
        if k not in ikeys: continue
        ida, idage = ikeys[k][0], ikeys[k][1]
        body = re.split(r'\. [A-Z][^:.]*:', m.group(2))[0]
        for nd in re.findall(r'`([a-z_]+)`', body):
            if nd in nodes and nodes[nd] > idage:
                fails.append(f'prereq age: {ida} (A{idage}) gated on `{nd}` (A{nodes[nd]})')
# 5. links and anchors
def anchors(t):
    out = set()
    for h in re.findall(r'^#{1,4} (.+)$', t, re.M):
        a = h.strip().lower()
        a = re.sub(r'[^\w\- ]', '', a).replace(' ', '-')
        out.add(a)
    return out
for p, t in texts.items():
    for target in re.findall(r'\]\(([^)]+)\)', t):
        if target.startswith('http'): continue
        f, _, anc = target.partition('#')
        tp = os.path.normpath(os.path.join(os.path.dirname(p), f)) if f else p
        if not os.path.exists(tp): fails.append(f'{os.path.basename(p)}: broken link {target}'); continue
        if anc and tp in texts and anc not in anchors(texts[tp]):
            fails.append(f'{os.path.basename(p)}: missing anchor #{anc}')
# 6. existing family keys and the 12 families all present in the reconciliation table
for fid, k in fams.items():
    if f'`{fid}`' not in texts[U]: fails.append(f'family {fid} not reconciled')
print('\n'.join(fails) if fails else 'ALL CHECKS PASS')
sys.exit(1 if fails else 0)

"""Technology Age 9 throughput model (per-turn-cp-recalibration.md §5; ADR-054 calibration, 2026-10-27).

From a ResearchPacingRunner run (<label>-s<seed>-nodes.csv and -turns.csv) and the content it ran on: starting when the
last finite Age-8 technology completed, replays the remaining finite Age-9 technologies under the run's own per-turn RP
pool and the run's completion turns for every other node, with the content's prerequisites (availability from the
previous turn, partial progress carried) in two modes — 'one': one node a turn (the cheapest remaining available); 'all':
every available node the turn's RP can pay, cheapest first. A model, not the engine: it measures what the prerequisite
structure and the RP allow, against what the run did.

Usage: python3 -I tools/a9_throughput_model.py <run-dir> <research.json the run used> <label>
"""
import csv, json, sys
sys.path.insert(0,'.')
from kr_lib import parse, holds
S, content, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
d=json.load(open(content))
scale=d['tuning']['pacingScale']['technology'][8]
nodes={}
for tree in ('technologies','civics'):
    for n in d[tree]: nodes[n['id']]=(tree,n)
ast={i:parse(n.get('prereq')) for i,(t,n) in nodes.items()}
age={i:int(n['age'][1]) for i,(t,n) in nodes.items()}
rep={i:bool(n.get('repeatable')) for i,(t,n) in nodes.items()}
a9=[i for i,(t,n) in nodes.items() if t=='technologies' and age[i]==9 and not rep[i]]
cost={i:nodes[i][1]['cost']*scale for i in a9}
for s in (1,2,42,7):
    ct={r['id']:int(r['completedTurn']) for r in csv.DictReader(open(f"{S}/{prefix}-s{s}-nodes.csv"))}
    pool=[0.0]+[float(r['pool']) for r in csv.DictReader(open(f"{S}/{prefix}-s{s}-turns.csv"))]
    a8done=max(ct[i] for i,(t,n) in nodes.items() if age[i]==8 and t=='technologies' and not rep[i])
    actual=max(ct[i] for i in a9)
    res=[]
    for mode in ('one','all'):
        comp={i for i,t in ct.items() if 0<=t<a8done}
        prog={i:0.0 for i in a9}
        T=a8done
        while any(i not in comp for i in a9) and T<1700:
            comp |= {i for i,t in ct.items() if 0<=t<T and i not in a9}
            avail=sorted([i for i in a9 if i not in comp and holds(ast[i], comp)], key=lambda i:(cost[i]-prog[i],i))
            budget=pool[min(T,len(pool)-1)]
            done=[]
            for i in avail[:1] if mode=='one' else avail:
                pay=min(budget,cost[i]-prog[i]); prog[i]+=pay; budget-=pay
                if prog[i]>=cost[i]-1e-9: done.append(i)
                if budget<=0: break
            comp|=set(done); T+=1
        res.append(T-1)
    print(f"seed {s}: A8 tech done {a8done}; actual A9 tech done {actual} (+{actual-a8done}); model one-node-per-turn {res[0]} (+{res[0]-a8done}); model every-available-node {res[1]} (+{res[1]-a8done})")

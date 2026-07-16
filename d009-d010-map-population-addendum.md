# D-009 + D-010 CLOSED — CONTINUOUS WORLD & POLITICAL POPULATION (Architecture Addendum)

---

## D-009 — THE MAP: NO CELLS, ANYWHERE THE EYE OR THE DESIGN CAN SEE

**Ruling recorded:** continuous AoE-style terrain. No tiles, no hexes. Cities grow organically and are connected by a real, built transport web — paths, roads, highways, bridges, underpasses, underwater tunnels, railways, air routes. Infrastructure is the differentiator vs Civ.

**Translation (the part that makes it buildable):** "continuous" cannot mean "structureless" — nothing can be simulated on pure pixels. The world becomes a **three-layer model**, none of which is a tile grid:

1. **Terrain as continuous fields.** Elevation, water, soil, moisture, temperature, vegetation stored as raster layers (world texture, not game tiles), rendered as seamless landscape. Sim systems *sample* the fields; nothing iterates "tiles." Battle maps (D-012) sample the same fields at the contact point — the battlefield IS that piece of the world, free.
2. **Infrastructure as a vector graph.** One multi-modal network: nodes (junctions, city gates, ports, stations, airports) and edges (path → road → highway; canal; rail; air corridor) with per-edge mode, capacity, speed, condition, and construction cost. Bridges, underpasses, and underwater tunnels are **edge types on the same graph** — expensive, era-gated, terrain-crossing edges, not special systems. Every link is a *built object with a history*: someone chose its route, paid for it, and maintains it. This is the architecture Civ cannot have, because tiles make geography a board; here geography is a landscape and infrastructure is authored by history.
3. **Places as settlements + catchments.** The unit of "where" is a settlement and its hinterland — the area within travel-time reach on the actual network (isochrone catchments, recomputed as the network grows). Population, production, markets, and unrest attach to settlements/catchments. Build a rail line and a town's economic reach physically expands — the map *is* the mechanism.

**City sprawl:** settlement footprints are organic blobs that grow along the network and terrain suitability (Manor-Lords-like silhouette), consuming real farmland as they expand (the food-ceiling tension made visible). Districts inside remain abstracted (v3 position holds).

**Trade = the network.** The separate "region graph" from the Scale Charter is deleted; settlements are the nodes, transport edges are the routes, edge cost/capacity price the flows. One object, three jobs (movement, trade, military logistics).

**Revised scale charter:** world raster ~1024–2048² per layer; settlements ~50 (ancient) → 300–800 (late); network 1k → ~30k edges late game; armies and trade path on the graph (A* + caching — trivial at this scale). Raster-wide updates (climate, vegetation) run chunked or every-N-turns, never full-raster every turn.

**Cost honesty:** this is the largest complexity add since the project began — organic sprawl, dynamic catchments, and network construction are real work. It is absorbed by era staging: **M1 ships one settlement, raster terrain, and a dirt path.** Highways, rail, and underwater tunnels are late-era *data* on an architecture that exists from week one. The ambition is front-loaded into the design, not the schedule.

---

## D-010 — POPULATION: TROPICO'S SOUL AT CIVILIZATION SCALE

**Ruling recorded:** population modeled Tropico-like — classed citizens with political temperature: riot potential, overthrow potential, and when overthrow risk fires, parts of government freeze until the player picks remedial measures from presented options.

**Translation:** Tropico simulates ~2,000 individuals on one island; this world holds tens of millions across 6,000 years — one-agent-per-person is impossible at scale. The hybrid delivers the same *soul*: **buckets** (settlement × culture × religion × class × age-band) carry the political psychology; **notables** put faces on it when it matters — the demagogue who leads the uprising emerges *from* the aggrieved bucket, named, with traits. Click any city and see who lives there, what they do, what they believe, what they're angry about.

**Class taxonomy v1 (era-evolving, data-driven; finalize at M2 spec):** slaves/serfs · peasants · laborers · artisans → industrial workers · merchants → capitalists · clergy · soldiers · bureaucrats · intelligentsia · elites/aristocracy. Classes differ in needs profile, income source, political weight, and *what they do when angry* (peasants revolt, elites coup, workers strike, soldiers mutiny).

**Per-bucket political state (computed, never assigned — Law 5):**
- **Needs ladder** (Tropico's inheritance): food, shelter, safety, faith, health, leisure, liberty, prospects — era-weighted; satisfaction from actual goods, services, and institutions.
- **Grievance stocks** — accumulated from unmet needs, taxation pain, repression memory, ethnic/religious discrimination, war exhaustion; decay slowly (grudges outlive their causes).
- **Riot propensity** = f(grievance, density, unemployment, food price shock, agitator notables present, policing).
- **Overthrow propensity** = f(elite grievance, military loyalty, legitimacy, treasury health, recent humiliations, foreign backing).

## THE UNREST LADDER (systemic crisis class; escalation, not events)

discontent → **protest** (production drag) → **riot** (local: damage, strikes, casualties) → **uprising** (regional: parallel authority, tax loss) → **revolution / coup attempt** (national).

**Government paralysis — director's mechanic, adopted as core design:** when overthrow risk crosses threshold, a **legitimacy crisis** state opens: a defined subset of verbs FREEZES (no new taxes, no conscription if the army wavers, no long-term projects) and the authority budget collapses to crisis-only actions. The player is presented an **option menu of remedial measures** — concede reforms · repress (military loyalty check — can backfire into the coup it feared) · buy off leaders (treasury) · scapegoat/purge ministers (notables) · call elections/council (institution-dependent) · emergency powers (legitimacy debt). Each unfreezes different verbs at different costs; resolution or collapse follows. This is Law 11 — *control is simulated* — made mechanical, and it plugs directly into the existing authority/bandwidth economy and crisis framework. Failure remains playable (v3 position): the successor state after the revolution is a continuation, not a game over.

---

## MILESTONE & DOC IMPACT

- **M1 (walking skeleton) redefined:** raster terrain render + one organic settlement + dirt-path edge + pop/food loop. No cell graph anywhere.
- **M2:** class taxonomy + needs ladder land with demographics.
- **M4:** armies march and supply on the network graph (same object as trade).
- **M5 (governing loop):** unrest-lite ships — grievance, protest, riot — because taxation without pushback isn't governing.
- **M8 (politics):** full ladder — uprising, revolution, coup, paralysis mechanic, remediation menus.
- **Scale Charter + system inventory rows updated per above; v3 "region graph" clause superseded.**

## DECISIONS OPENED (close at the named spec)

- **D-015** raster resolution & world physical size (M1) — rec: 1024² at ~10 km/px class.
- **D-016** catchment recompute policy (M1) — rec: incremental, on network change only.
- **D-017** settlement sprawl model (M3) — rec: suitability-field blob growth along edges.
- **D-018** final class list & needs weights (M2 data files).
- **D-019** verb-freeze matrix per crisis type (M8) — which actions lock under which legitimacy states.

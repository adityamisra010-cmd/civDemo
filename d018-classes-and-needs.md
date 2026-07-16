# D-018 CLOSED (BY DELEGATION) — CLASS ROSTER & NEEDS MODEL
### Director delegated the call. Committed below; veto individual lines rather than reopening the frame. Lands as M2 data files.

---

## 1. DESIGN PRINCIPLES

- **Classes emerge, they are not unlocked.** Every class has a computed emergence predicate (Law 4). A Stone-Age village instantiates 2–3 classes; class *differentiation itself* is a simulation output of surplus and specialization.
- **A class earns its slot only if it differs in all four:** income source, needs signature, political weight basis, and — decisive for D-010 — *what it does when angry*. Anger must route through a different system per class (economy, army, legitimacy, state capacity), never a generic unrest bar.
- **Engineering ceiling:** bucket cap (D-010) permits ~12 class slots. Committed: **11 classes + 1 reserved slot.**
- **No era weight tables.** "Modern people want liberty" is not a calendar fact — it is a computed one (see §4, rising expectations).

## 2. THE CLASS ROSTER (11)

| Class | Income | Emerges when (computed predicate) | Angry behavior (system it hits) |
|---|---|---|---|
| **Enslaved/Bonded** | none (owned) | unfree-labor institution active | flight, sabotage, servile revolt (economy, military) |
| **Peasants** | subsistence | always (baseline class) | tax evasion, flight to frontier, jacquerie (revenue, order) |
| **Laborers** | wage | wage employment exists; *becomes the proletariat when factory share grows — same class, new workplace* | strikes, riots, unionization via movement framework (production, order) |
| **Artisans** | skilled wage/own-shop | craft specialization share > threshold | guild pressure, machine-breaking under automation displacement (production) |
| **Merchants** | trade profit | market + trade-route volume > threshold | capital flight, tax strikes, funding opposition (treasury, politics) |
| **Capitalists** | capital profit | private ownership of firms at scale | capital flight, lockouts, coup finance, regulatory capture (economy, politics) |
| **Clergy** | stipend/tithe | organized religion institutionalized | legitimacy withdrawal, sedition preaching, schism (legitimacy — they *produce* it) |
| **Soldiers** | stipend | standing professional force exists (levies = armed peasants, not this class) | desertion, mutiny, **coup** — the praetorian threat (military, regime survival) |
| **Bureaucrats** | stipend | administrative institutions > threshold | obstruction (state capacity silently drops), corruption, defection (state capacity) |
| **Intelligentsia** | mixed | literacy share + education institutions > threshold | agitation (multiplies others' grievance), ideology production, movement leadership (opinion) |
| **Aristocracy** | land rent | hereditary landed elite institution | coup, secession, private retinues, foreign intrigue (regime, territory) |

Unemployment/marginality is a **state flag on buckets**, not a class — people cross it too fast for class identity, and it is the riot accelerant wherever it accumulates.

## 3. THE NEEDS LADDER (8)

| Need | Satisfied by (real supply, Law 2) |
|---|---|
| **Sustenance** | food goods vs caloric requirement; shortfall → mortality + maximal grievance |
| **Shelter** | housing stock quality per settlement |
| **Safety** | policing/defense institutions, absence of raids/war on home ground, crime level |
| **Health** | sanitation infrastructure, healthcare institutions, ambient disease burden (Part 19) |
| **Belonging/Faith** | religious & cultural access (temples, festivals), identity respect — discrimination strikes here |
| **Comfort** | consumer goods above basics — the era ladder of pots → textiles → furniture → radios → devices |
| **Dignity/Liberty** | legal rights, absence of arbitrary repression, freedom of practice (structurally crushed for Enslaved — their grievance engine) |
| **Prospects** | education access, mobility openness, growth trend — *children's outlook* |

## 4. WEIGHTING MODEL (the actual call)

- **Tier A — gate needs.** Sustenance, Shelter, Safety dominate when unmet, for every class: a starving intelligentsia riots over bread, not press freedom. Implemented as need-satisfaction floors that override signature weights below threshold.
- **Tier B — class signatures.** Baseline weight vectors per class on the upper five (Aristocracy → Dignity/status; Intelligentsia → Liberty+Prospects; Clergy → Faith; Merchants/Capitalists → Prospects+Comfort; Peasants → Faith+Safety). Shipped as data, all `TUNE`.
- **Rising expectations (replaces era tables).** Salience of Comfort, Liberty, and Prospects scales with the bucket's **literacy, urbanization, and media exposure** — computed state (Law 5). Consequence, historically true and desired: development *raises* unrest potential before satisfying it; revolutions cluster in modernizing societies, not stagnant ones. Registered as an emergence-test candidate.
- **Habituation ratchet.** Expectation baselines drift toward recent consumption: yesterday's luxury is today's floor. Losing accustomed comfort generates more grievance than never having had it.
- **Relative deprivation.** Grievance also accrues from *visible* inequality: own satisfaction vs elite satisfaction, weighted by proximity and media reach. Gini becomes flammable only when seen.

**Math shape (M2 spec finalizes):** per need n: satisfaction sₙ ∈ [0,1] from supply vs class-expected basket; grievance stock G += Σ wₙ·(expectationₙ − sₙ)⁺ · dt − decay·G·dt, with the Tier-A override, ratcheting expectations, and a relative-deprivation term. All coefficients `TUNE`-registered.

## 5. POLITICAL WEIGHT & MOBILITY (sketches; owning specs M5/M8)

- **Influence** = f(class wealth share, institutional enfranchisement under current regime, organization level, armed capacity). Soldiers always carry latent weight — guns are enfranchisement of last resort. Aristocratic regimes weight aristocracy; broad-franchise regimes weight headcount; every regime module (Part 15) publishes its enfranchisement vector.
- **Mobility flows** (Ledger transfers, conserved): Peasant→Laborer via urbanization pull; Laborer→Artisan via training; Artisan→Merchant; Merchant→Capitalist; →Clergy/Soldiers/Bureaucrats via institutional recruitment; →Intelligentsia via education access; Aristocracy nearly closed (marriage/ennoblement trickle); Enslaved→free only through manumission/abolition institutions. Rates driven by destination demand and access — mobility openness itself feeds the Prospects need (closed societies choke it and pay in grievance).

## 6. DATA SCHEMA SHAPE (agents implement loaders at M2)

`classes.json`: `{ id, display, emergence: <predicate>, income_type, tierB_weights: {need: w}, anger_repertoire: [behavior_ids], mobility_to: [class_ids], enfranchisement_tags }`
`needs.json`: `{ id, gate: bool, satisfiers: [{good|service|institution, conversion}], expectation_params }`

**D-020 opened:** emergence/threshold predicates need a format — recommendation: a tiny comparison DSL (`"factory_employment_share > 0.02 && wage_share > 0.3"`), parsed and validated at load; no scripting engine (mod platform stays deleted). Close at M2 spec.

## 7. WHAT THIS FEEDS

M2 demographics (buckets carry class + needs), M3 markets (class consumption baskets are demand), M5 unrest-lite (grievance → protest → riot), M8 full ladder (class-specific overthrow behaviors + paralysis mechanic), calibration battery (rising-expectations emergence test).

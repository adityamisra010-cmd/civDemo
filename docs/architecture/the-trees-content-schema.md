# THE TREES + AGES — EDITABLE CONTENT SCHEMA

The Trees and Age screens are generated from five JSON files in `Sim.Ui/UiContent/trees/`,
copied beside the executable as `ui-content/trees/` on every build.

- **In the game**, the overlay reads the copy beside the executable. Edit that copy and press
  **Reload** in the overlay's state-source panel, or edit the source files and rebuild.
- **For a preview without a build**, run `sim-ui --trees-preview out --trees-content
  Sim.Ui/UiContent/trees`. It paints straight from the source files.

No C# change is needed for any of the edits described here. Background: [`the-trees-ui.md`](the-trees-ui.md) and
[`ages-ui.md`](ages-ui.md).

**General rules** (enforced by `TreesContentLoader`; each problem is reported with the file and a
JSON path, shown on screen instead of crashing):

- **Strict fields.** An unknown field is an error, so a misspelt field cannot silently drop an
  edit. A key written twice in one object is an error, and `null` inside a list is an error.
  Comments (`//`, `/* */`) and trailing commas are allowed.
- **Names, not numbers.** Enum-valued fields accept the member name only. `"1"` and flag
  combinations such as `"A, B"` are rejected.
- **Ids.** Every id is unique within its list. References use ids.
- **Glyph names.** They are the C# enum member names: `base` from `GlyphBase`, `mark` from
  `GlyphDomain`, `era` / `register` from `EraRegister`, `glyphState` from `GlyphState`. The
  diagnostic lists the valid names. See `glyph-sheet-v1.png` for how each looks.
- **Kebab-case vocabularies.** Words like `research-progress` are listed per field below.
- **Placeholder flags.** `placeholder: true` marks demonstration content. The screens draw a
  DEMO or PLACEHOLDER tag for it.

---

## §1 `the-trees.json` — the graph (schema `civ-sim/the-trees@1`)

| top-level field | type | notes |
| --- | --- | --- |
| `schema` | string | must be `civ-sim/the-trees@1` |
| `placeholder`, `notice` | bool, string | file-level flag and banner text |
| `lenses[]` | see §1.1 | the player-facing lenses; at least one |
| `states[]` | §1.2 | the state vocabulary |
| `stateSets[]` | §1.3 | which states a node type may occupy |
| `nodeTypes[]` | §1.4 | the node taxonomy |
| `relationKinds[]` | §1.5 | the relationship taxonomy |
| `nodes[]` | §1.6 | the graph's nodes |
| `edges[]` | §1.7 | relationships written as a list |

### 1.1 `lenses[]`

`id` (e.g. `KNOWLEDGE`), `name`, `order` (navigation and band order), `mark` (GlyphDomain for the
emblem), `shortDescription`. A lens with no nodes is a warning, not an error.

### 1.2 `states[]`

| field | values | meaning |
| --- | --- | --- |
| `id`, `name`, `legend` | strings | the legend shows `name` and uses `legend` as its tooltip |
| `track` | `research` · `realization` · `milestone` | which axis the state describes |
| `glyphState` | `Locked` · `Discovered` · `Available` · `InProgress` · `Complete` · `Stalled` · `Decayed` | the ring |
| `stage` | 0..4 | realization pips |
| `wash` | `none` · `full` · `research-progress` · `realization-progress` · `milestone-progress` | the fill |
| `researchRole` | `none` · `researchable` · `researching` · `completed` | how the research UI counts it |

### 1.3 `stateSets[]`

`id`, `description`, `states` (ordered ids, no duplicates). The **first** state is what a node
shows when the source reports nothing.

### 1.4 `nodeTypes[]`

`id`, `name`, `base` (GlyphBase), `mark` (GlyphDomain, optional), `stateSet` (id),
`description`.

### 1.5 `relationKinds[]`

| field | values | meaning |
| --- | --- | --- |
| `id`, `name`, `description` | strings | |
| `verb` | string | the sentence "From *verb* To" in the details panel |
| `flow` | `forward` · `reverse` · `none` | reverse: "A requires B" flows from B to A; none: undirected |
| `layering` | bool | orders the columns; layering kinds must be acyclic, and `none` flow cannot layer |
| `stroke` | `solid` · `dashed` · `dotted` | |
| `ink` | `InkPrimary` · `InkSoft` · `Verdigris` · `IronRed` · `GoldLeaf` · `River` | palette token |
| `arrow` | bool | |

### 1.6 `nodes[]` — the node data model (task §8)

| field | type | notes |
| --- | --- | --- |
| `id` | string | e.g. `knowledge.mathematics` |
| `domain` | lens id | the primary lens (its band) |
| `alsoIn` | lens ids | other lenses the node appears in (each once) |
| `type` | node-type id | decides base, default mark and state set |
| `name`, `shortDescription`, `longDescription` | strings | |
| `icon` | `{ "base"?, "mark"? }` | overrides the type's glyph |
| `visualStyle` | `{ "era"?, "note"? }` | drawing register (EraRegister) |
| `ageRange` | `{ "from"?, "to"? }` Age ids | `to` absent means open-ended; `to` must not precede `from` |
| `ageRefs` | Age ids | extra Age associations |
| `researchCost` | `{ "points"?: integer ≥ 0, "note"? }` | content only; null or absent shows "cost TBD" |
| `prerequisites` | node ids | writes edges *X prerequisite self* |
| `enables` | node ids | writes *self enables X* |
| `dependsOn` | node ids | writes *self dependsOn X* (reverse flow) |
| `feedsInto` | node ids | writes *self feeds X* |
| `relatedInstitutions`, `relatedInfrastructure`, `relatedIndustry`, `relatedMilitary`, `relatedApplications` | node ids | write undirected `relatedTo` edges; a warning if the target is not in that lens |
| `historicalReferences` | strings | |
| `directorNotes` | string | shown under DIRECTOR NOTES |
| `milestoneRef` | milestone id | for milestone nodes |
| `column` | integer 0..256 | minimum layout column (a hint) |
| `placeholder` | bool | default true |

**Reserved ids.** The inline fields write the kinds `prerequisite`, `enables`, `dependsOn`,
`feeds` and `relatedTo`, so those five ids must exist in `relationKinds[]` whenever a node uses
the matching field. The `related*` lists check their targets against the lens ids `INSTITUTIONS`,
`INFRASTRUCTURE`, `INDUSTRY`, `MILITARY` and `APPLICATIONS`, as a warning only. Renaming one of
these ids is a schema change, not a content edit.

**`state`, `progress` and `researchProgress` are not content.** They are runtime values from the
state source (§5 below). The node's `NodeStatus` carries them as `StateId`, `RealizationProgress`
(the "progress" of the task's model) and `ResearchProgress`.

### 1.7 `edges[]`

`{ "from": node id, "to": node id, "kind": relation-kind id, "note"?: string }`. The loader
rejects the following:

- a node relating to itself;
- an unknown id;
- a cycle among layering kinds. The error names the cycle; move the feedback edge to
  `improves` or `supports`.

A duplicate (same from, to and kind) is a warning and is kept once.

## §2 How to…

- **Add a node.** Append to `nodes[]` with `id`, `domain`, `type` and `name`. Link it inline or
  in `edges[]`.
- **Add a relationship kind.** Append to `relationKinds[]`, then use its id in `edges[]`.
- **Add a state.** Append to `states[]`, then add its id to the state sets that should allow it.
- **Change what a node type may show.** Edit its `stateSet`, or add a new state set.
- **Add a lens.** Append to `lenses[]`. The navigation, layout band and legend follow.
- **Set a research cost.** Set `researchCost.points`. The UI shows it; nothing computes with it.

## §3 `ages.json` — Ages and milestones (schema `civ-sim/ages@1`)

`milestoneCategories[]`: `id`, `name`, `provisional` (the checklist marks provisional categories
with `*`).

`ages[]`:

| field | notes |
| --- | --- |
| `id`, `displayName`, `order` | `order` unique; strictly ordered |
| `shortDescription`, `historicalContext` | placeholders today |
| `dateRange` | `{ startLabel, endLabel, startYear?, endYear?, note }`; display only, never a gate |
| `milestones` | ordered milestone ids; each milestone listed by exactly one Age |
| `visualTheme` | `{ register (EraRegister), accent (palette token), motif }` |
| `icon` | `{ base, mark }`, default Star + Hourglass |
| `transitionEffect` | `{ animation (id in animations.json), note }` |
| `deltaT` | `{ placeholder, yearsPerTurn? (> 0), note }`; NOT implemented |
| `catchUp` | `{ placeholder, pathwayBased, note }`; NOT implemented |
| `placeholder` | bool |

`milestones[]`:

- `id`, `age`, `name`, `description`;
- `category`, which must name a milestone category;
- `mandatory`;
- `prerequisites` (milestone ids, acyclic);
- `evidenceNotes`, `historicalSource`;
- `nodeRefs` (node ids in the graph, validated);
- `placeholder`.

A milestone that no Age lists and that has no `age` is an error. A milestone that no Age lists
but that names its `age` is a warning.

## §4 `gallery.json` — building and unit placeholders (schema `civ-sim/gallery@1`)

| list | fields |
| --- | --- |
| `maturityStages[]` | `id` (NEW, DEVELOPING, ESTABLISHED, MATURE), `name`, `stage` 1..4, `legend` |
| `operationalStatuses[]` | `id`, `name`, `glyphState`; the building's ring by status |
| `unitStates[]` | `id`, `name`, `glyphState`, `legend` |
| `veterancyLevels[]` | `id`, `name`, `chevrons` 0..3 |
| `buildings[]` | `id`, `name`, `base`, `mark`, `era`?, `category`, `placeholder` |
| `units[]` | `id`, `name`, `base` (Standard), `mark` (an object), `era`?, `role`, `placeholder` |

## §5 `demo-state.json` — the PLACEHOLDER state source (schema `civ-sim/trees-demo-state@1`)

The script stands in for simulation state that does not exist. `civilization.name` names the
civilization.

`steps[]` is a list in which **each step lists only what changed** from the previous one. A step
may contain these parts:

| part | fields |
| --- | --- |
| `label` | shown in the state-source panel |
| `research` | `pointsPerTurn`, `currentTarget` (node id), `note` |
| `nodes[]` | `id`, `state` (must be in the node type's state set), `researchProgress`, `researchPoints`, `realizationProgress`, `note` |
| `ages` | `current` (never earlier than the previous step's), `progress` (omitted: carried over, unless the Age changed), `transition { pending, to, note }` (`to` after the current Age; omitted or null clears it) |
| `milestones[]` | `id`, `completion` (`not-started` · `partial` · `complete`), `progress`, `evidence` |
| `buildings[]` | `building`, `maturity`, `maturityProgress`, `constructionProgress`, `personnel { current, capacity }`, `capacity`, `specialization`, `ageBuilt`, `status` |
| `units[]` | `unit`, `state`, `strength`, `experience`, `veterancy`, `experienceProgress`, `training`, `recovery`, `doctrine` (node id), `cohesion` |
| `diffusion` | `stage` 0..4 (the gallery's sample pathway) |

All fractions must lie in [0,1]. Deleting `demo-state.json` makes the screens show the content
alone (`NoStateSource`). Every node then shows the first state of its set.

## §6 `animations.json` — presentation only (schema `civ-sim/ui-animations@1`)

`animations[]` fields:

- `id`, `description`;
- `kind`: `none` · `pulse` · `flash-ring` · `shimmer` · `pip-fill` · `sweep` · `flow-dots`;
- exactly one of:
  - `appliesTo`, a steady loop while a reported state holds: `node-state:<state>`,
    `edge-kind:<kind>`, `building-status:<status>`, `unit-state:<state>` or `age:transition`;
  - `trigger`, a one-shot on a transition: `node-enter:<state>`, `node-stage-up`,
    `building-stage-up`, `unit-veterancy-up` or `age-enter`;
- `periodSeconds` (> 0 for loops), `durationSeconds` (> 0 for one-shots), `amplitude`, `dots`.

A target that names no existing state, relation kind or status is warned about, because such an
animation never plays. An animation can never change a state or a number. The painter draws glyphs from reported state
only (tested).

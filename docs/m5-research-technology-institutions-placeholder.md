# M5 — RESEARCH, TECHNOLOGY & INSTITUTIONS (SCOPE PLACEHOLDER)

> **Superseded 2026-10-03 (Director roadmap rebase).** The authoritative ladder is now: M0 Kernel · M1 Walking Skeleton · M2 Demography / Food · M3 Production / Markets · M4 Empire / Strategic Foundation · M5 Governing Gameplay (current, being finished) · M6 Knowledge / Research / Technology (next) · M7 Battle Layer · M8 Politics / Diplomacy · M9 Society · M10 Integrated Civilization Simulation · M11+ Depth & Content Expansion. Knowledge/research/technology is **M6** and precedes the Battle Layer (**M7**), which consumes M6's authoritative technology/capability state. Canonical record: `docs/milestones.md` §"Roadmap rebase 2026-10-03". The text below is retained as history.


> **CORRECTION NOTICE — 2026-09-30 (added later; the original text below is NOT rewritten).** The Director
> rulings recorded verbatim in `docs/d044-research-progression-rulings.md` (D-044) supersede three scope items
> below:
> - **§3 item 1 ("Science as an accumulating resource") and item 2 ("Knowledge as an accumulating
>   resource").** There is one Cognitive Load Point (CLP) pool, no Science or Knowledge resource, and no
>   general bank. Technology is the knowledge (D-044 R1, R2, R20-D).
> - **§3 item 8 ("Multiple simultaneous research projects — NOT a single Civ-style queue").** There is one
>   active research target at a time. Many nodes keep partial progress, and there is no queue (D-044 R9).
>
> **TERMINOLOGY — 2026-10-01 (D-046 G5; added later, nothing above rewritten).** The one research pool is now named
> **Research Points (RP)**; "Cognitive Load Point (CLP)" above is the D-044 name it replaces.
>
> The implementation built on those rulings is recorded in `docs/adr/adr-029-research-engine.md`. **The
> milestone-placement question in CR-005 remains OPEN** and is not ruled by D-044 (D-044 Part D, T1).

**THIS IS NOT A SPEC AND NOTHING HERE IS RATIFIED.** It is a scope record, made so
the architectural requirement cannot be lost while M4 continues.

**Milestone placement is NOT settled.** Placing this at M5 conflicts with the
frozen milestone order — the Spine currently assigns M5 to the governing loop, M6
to knowledge & diffusion, and M7 to politics/institutions, and `m4-spec.md`
defers money to M5 by name in five places. That conflict is written up in
`docs/adr/cr-005-m5-research-technology-institutions-placement.md` and **awaits a
director ruling.** No frozen document has been edited.

**NO MECHANIC IS IMPLEMENTED AND NO FORMULA IS FROZEN.** Per the direction, the
following are deliberately ABSENT and must not be invented before the dedicated
M5 design workshop: science formulas · knowledge formulas · population exponents ·
research costs · technology prerequisites · government modifiers · institution
bonuses · foreign-exchange coefficients · research slot counts · technology eras ·
military technology values · economic technology values.

**A packet written against this document without a ratified spec would be
implementing ahead of the spec, which CLAUDE.md forbids.**

---

## §1 WHY THIS NEEDS AN OWNER BEFORE ITS DEPENDANTS

The governing sequencing rule, stated by the director:

> **TECHNOLOGY MUST BE DESIGNED BEFORE DOWNSTREAM SYSTEMS THAT DEPEND ON
> TECHNOLOGICAL CAPABILITIES.**

Concretely: a later warfare or economic packet **must not independently invent its
own technology prerequisites** if those prerequisites belong to this milestone.
The tree already shows this pressure — M4's T4.8 declined to specify notable
*purchase* because "payment is money, M5", deferring rather than inventing. The
same discipline is wanted for capability questions of the form *"can this
civilization do X yet?"*.

## §2 THE ARCHITECTURE IS A CHAIN, NOT A TREE

Explicitly **NOT** "technology = a tree where the player unlocks bonuses". The
intended shape:

```
population
  → human intellectual potential
    → education / institutions
      → science and knowledge generation
        → research
          → technologies / institutions
            → changed civilization capabilities
              → changed economic, military, political and social behaviour
```

Two orthogonal axes, stated so they are not conflated later:

- **Research determines which capabilities can become POSSIBLE.**
- **Government determines which institutional pathways are naturally AVAILABLE
  or EFFICIENT.**

A government change therefore **transforms** institutional and technological
pathways rather than deleting unlocked technologies.

## §3 INTENDED SCOPE

Recorded as ownership, not as design. Numbering follows the director's list.

**Resources and their source**
1. Science as an accumulating resource.
2. Knowledge as an accumulating resource.
3. Population as the fundamental source of intellectual potential.
4. Diminishing returns from raw population scale.
5. Education and literacy as modifiers of knowledge production.

**Institutions as the conversion mechanism**
6. Schools, colleges, universities, academies, laboratories, libraries and
   similar institutions converting human potential into usable knowledge/science.

**Research structure**
7. Research capacity and research throughput.
8. **Multiple simultaneous research projects — NOT a single Civ-style queue.**
9. Multiple parallel research domains.
10. Cross-domain research requirements.
11. Technology prerequisites and **alternative technological pathways**.

**Government coupling**
12. Government-dependent technological and institutional pathways.
13. Government changes transforming, replacing, disabling or enabling institutions.
14. Technologies/institutions becoming obsolete or transforming into successors.

**Foreign and inter-civilization**
15. Foreign knowledge exchange.
16. Open-border academic and intellectual exchange.
17. Student/researcher migration and possible brain drain.
18. Research diffusion between civilizations.
19. AI-controlled civilization research strategy.
20. Circumstantial research and discovery.

**Downstream effects (the reason for the sequencing rule)**
21. Warfare: available military capabilities, organization, doctrines, weapons,
    strategic options.
22. Economics: which economic mechanisms and levers become available.
23. Agriculture, medicine, industry, infrastructure.
24. Government, civics, diplomacy, population and institutions.

Foreign contribution is expected to be gated by future rules involving distance,
diplomacy, language, institutional capacity, literacy and wealth. **Those rules
are not decided here.**

## §4 WHAT THIS DOCUMENT DOES NOT DO

- It does not amend the Spine, the milestone order, or any ratified document.
- It does not open, close or reinterpret any D-decision.
- It does not create a packet, a corridor, a band or an acceptance criterion.
- It does not authorise any implementation.

## §5 OPEN QUESTIONS FOR THE M5 DESIGN WORKSHOP

Listed because naming them now is cheaper than rediscovering them, and because
each is a place where a packet might otherwise improvise:

1. Are **science** and **knowledge** two distinct conserved stocks, one stock with
   two flows, or one stock and one rate? Law 1 and law 7 (conserved stocks are
   `long`) bind whichever answer is taken.
2. Is an institution a **stock, a structure, or a table row with a lifecycle**?
   Item 14 (obsolescence and transformation into successors) implies a lifecycle.
3. Does knowledge **decay**? If so, is decay a Ledger sink with its own reason,
   as spoilage and granary overflow are for grain?
4. Item 17 moves **people**, so brain drain must flow through the existing
   migration/`Ledger.Transfer` machinery rather than a parallel mover.
5. Item 4's "diminishing returns" is a curve, and CR-003 §5.1's discipline on
   derived-vs-chosen constants applies to it.
6. Does technology alter **mechanisms** (law 2 compliant) or apply **modifiers**
   (law 2 forbids free-floating permanent buffs)? **This is the single most
   load-bearing question in the packet** — "unlock a bonus" is precisely the
   shape law 2 bans, and §2 above is the answer's outline, not the answer.
7. Item 19 (AI research strategy) needs a decision procedure that is deterministic
   and free of unordered iteration (law 5).

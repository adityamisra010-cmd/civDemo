#!/usr/bin/env python3
"""ADR-045 (D-062) — the NON-BREAKING research-content tranche: proposal -> Sim.Data/content/research.json.

Reads, read-only:
  * the PRE-TRANCHE research.json (default: Sim.Tests/Fixtures/research-pre-adr045.json, the file as it stood
    before this tranche; it is also the attribution twin's fixture);
  * technology-tree-proposal.json and civics-tree-proposal.json (this package; canonical proposal data).
Writes:
  * Sim.Data/content/research.json (--out);
  * docs/m6-plan/knowledge-redesign/implementation-status.md (--status).

Deterministic: no clock, no randomness, no set/dict iteration that reaches the output unordered; every list is
emitted in proposal key order or in the base file's own order. Re-running on the same inputs is byte-identical.

The tranche rules (the coordinator's Track TREE contract, D-062):
  * no key retirement and no merge (siege_tower / crossbow_repeating stay, and so do their survivors'
    merge-only edits);
  * a split's NEW part is always added as a new key; a consumer OUTSIDE the node graph (an entity requirement)
    moves off an existing key only when holding the existing key keeps every holder eligible — otherwise the
    move is DEFERRED to the remap seam (KR-2), and a narrowing re-point whose new route is genuinely new is
    implemented as a WIDENING (new route OR the old one) — REVISED;
  * node prerequisite rewires (including a split's re-pointed dependants) are implemented: completed knowledge
    never decays, so no holder loses anything, and partial progress stays dormant exactly as for any rewire;
  * research.json researchStage and tuning are NOT touched (Track CP owns them);
  * the 7 legacy civics are unchanged; the 60 new civics are knowledge-only (capability strings, no entity
    kind), which the loader accepts (civics may unlock only institutions — they unlock none).

Usage (from the repository root):
  python3 -I docs/m6-plan/knowledge-redesign/tools/implement_tranche.py
  python3 -I docs/m6-plan/knowledge-redesign/tools/implement_tranche.py --check   (exit 1 if an output is stale)
"""
import argparse
import json
import re
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
ROOT = PKG.parent.parent.parent
BASE = ROOT / "Sim.Tests" / "Fixtures" / "research-pre-adr045.json"
OUT = ROOT / "Sim.Data" / "content" / "research.json"
STATUS = PKG / "implementation-status.md"
TECH = PKG / "technology-tree-proposal.json"
CIVICS = PKG / "civics-tree-proposal.json"

KEYWORDS = {"AND", "OR", "NOT"}
FACTOR_ORDER = ["novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"]
UNLOCK_KINDS = [("buildings", "building."), ("infrastructure", "infra."), ("institutions", "inst."),
                ("units", "unit."), ("activities", "activity."), ("projects", "project."), ("recipes", "recipe.")]

# --------------------------------------------------------------------------------------------------
# Decisions (each one is reported in implementation-status.md and ADR-045)
# --------------------------------------------------------------------------------------------------

# Merge survivors: their edits ARE the merge (rename / capability gained from the retired node) — deferred with it.
MERGE_DEFERRED = {
    "siege_ram": "its rename, widened emerged text and the gained 'assault over walls' string are the siege_tower merge, deferred (no merge in this tranche; siege_tower stays)",
    "crossbow": "its gained repeating-crossbow string is the crossbow_repeating merge, deferred (crossbow_repeating stays)",
}

# Renames kept back because the eligibility the old name describes stays on the key (entity move deferred).
RENAME_DEFERRED = {
    "electricity_generation": "renamed 'Central power station' only because infra.power_grid moves to ac_transmission; that move is deferred (it would remove a consumer from this key), so the key keeps its grid eligibility and its name",
}

# Entity requirement changes: verdict, the requirement written, and why.
ENTITY_DECISIONS = {
    "building.library": ("accepted", None, "widening (mesoamerican_script added); no holder loses eligibility"),
    "building.school": ("accepted", None, "widening (mesoamerican_script added); no holder loses eligibility"),
    "inst.archive": ("accepted", None, "widening (mesoamerican_script added)"),
    "inst.historiography": ("accepted", None, "widening (mesoamerican_script added)"),
    "inst.natural_philosophy": ("accepted", None, "widening (mesoamerican_script added)"),
    "inst.scribal_school": ("accepted", None, "widening (mesoamerican_script added)"),
    "building.lighthouse": ("revised", "ashlar_masonry OR stone_fortification",
                            "the proposal moves it to ashlar_masonry; written as 'ashlar_masonry OR stone_fortification' so holders of stone_fortification keep it (stone_fortification now requires ashlar_masonry, so for every new completion this IS the proposal's requirement); the narrowing waits for the remap seam (KR-2)"),
    "infra.sewerage_system": ("revised", "sanitary_engineering OR (germ_theory AND reinforced_concrete)",
                              "the proposal's sanitary_engineering route is added; the old route is kept as an OR alternative until the remap seam (KR-2) so no holder loses eligibility"),
    "inst.guild": ("revised", "textile_workshop AND (legal_code_roman OR craft_guilds)",
                   "craft_guilds route added (it brings the municipal_charter path); legal_code_roman kept as an alternative — narrowing deferred (KR-2)"),
    "inst.military_medicine": ("revised", "surgery_instruments AND (census OR standing_army)",
                               "standing_army route added; census kept as an alternative — narrowing deferred (KR-2)"),
    "inst.mint_institution": ("revised", "coinage_electrum AND (census OR appointed_officials)",
                              "appointed_officials route added; census kept as an alternative — narrowing deferred (KR-2)"),
    "inst.statistics_vital": ("revised", "(census AND calculus) OR (official_statistics AND epidemiology)",
                              "official_statistics + epidemiology route added; the old route kept — narrowing deferred (KR-2)"),
    "building.spaceport": ("deferred", None,
                           "split re-point rocket -> orbital_launch_vehicle would remove a consumer from rocket; orbital_launch_vehicle requires rocket, so an OR widening is a no-op — deferred to the remap seam (KR-2)"),
    "project.orbital_launch": ("deferred", None,
                               "split re-point rocket -> orbital_launch_vehicle would remove a consumer from rocket (as building.spaceport) — deferred (KR-2)"),
    "infra.power_grid": ("deferred", None,
                         "split re-point electricity_generation -> ac_transmission would remove a consumer from electricity_generation; ac_transmission requires it, so an OR widening is a no-op — deferred (KR-2)"),
    "inst.mass_media_broadcast": ("deferred", None,
                                  "split re-point radio -> radio_broadcasting would remove a consumer from radio; radio_broadcasting requires radio (and television now requires radio_broadcasting), so an OR widening is a no-op — deferred (KR-2)"),
    "inst.central_bank": ("deferred", None,
                          "re-point to the central_banking civic, whose prerequisite is the old requirement itself: an OR widening is a no-op and the narrowing removes the joint_stock / paper_money consumers — deferred (KR-2)"),
    "inst.mass_schooling": ("deferred", None,
                            "census -> compulsory_education: compulsory_education requires census, so an OR widening is a no-op and the narrowing removes a consumer from census — deferred (KR-2)"),
    "inst.quarantine": ("deferred", None,
                        "census -> public_health_board: public_health_board requires census, so an OR widening is a no-op and the narrowing removes a consumer from census — deferred (KR-2)"),
}

# Widenings the proposal carries only in a NEW node's unlock list (not in entityChanges): accepted.
IMPLIED_WIDENINGS = {
    "unit.siege_engine": ("traction_trebuchet", "traction_trebuchet lists it in the proposal (unit-family eligibility only, realized by the battle layer, M7)"),
    "activity.ocean_shipping": ("ocean_junk", "ocean_junk lists it in the proposal (a fourth oceanic route beside carrack, caravel and the Polynesian canoe)"),
}

# Capability-string updates on EXISTING keys (from the proposal records' 'changes').
CAPABILITY_OVERRIDES = {
    "collar_harness": ["horse haulage"],
    "anatomy_dissection": ["accurate surgery"],
    "interchangeable_parts": ["gauge-and-jig manufacture of identical parts", "field repair", "the American system of manufactures"],
    "automobile_mass": ["suburb", "trucking", "the road economy", "moving assembly line (Ford, 1913)", "mass production"],
    "electromagnetism": ["motor", "generator", "telegraph"],
    "satellite": ["orbital observation (a pointed observer, never a map reveal — D-039 B3, D-040 B3)", "orbit insertion, tracking and telemetry"],
    "radio": ["wireless command (D-039 A)", "ship-to-shore wireless", "broadcast media (D-041 C1)"],
    "nuclear_fission": ["controlled self-sustaining chain reaction", "critical assembly (research pile)"],
}
CAPABILITY_NOTES = {
    "radio": "'broadcast media' stays on radio while inst.mass_media_broadcast does (that move is deferred); 'ship-to-shore wireless' added",
    "rocket": "'satellite launch' stays on rocket while building.spaceport and project.orbital_launch do (that move is deferred)",
}

# Description updates on EXISTING keys whose name or scope changed.
DESC_OVERRIDES = {
    "collar_harness": "A breast strap that bears on the horse's chest instead of its throat — far more pulling power for carts and chariots.",
    "electromagnetism": "Oersted 1820, Faraday 1831 — current makes magnetic fields and moving fields make current: the motor and the generator.",
    "internal_combustion": "Daimler and Benz 1885-86 — a light, fast-running petrol engine: portable power for vehicles.",
    "radio": "Marconi 1895 — wireless telegraphy: coded messages without wires, between shore and ship and across oceans.",
    "nuclear_fission": "Discovery 1938; Chicago Pile 1942 — the controlled, self-sustaining chain reaction.",
    "nuclear_power": "Obninsk 1954, Calder Hall 1956 — engineering a reactor's heat into steam and electricity for the grid.",
}

# Capability strings for NEW nodes where the proposal's knowledge grant names a move this tranche defers.
NEW_CAPABILITY_OVERRIDES = {
    "ac_transmission": ["transformers and polyphase AC", "high-voltage transmission over long distances"],
    "orbital_launch_vehicle": ["orbital-class rocket (payloads placed in orbit)"],
    "radio_broadcasting": ["voice radio and broadcast transmission"],
    "fission_weapon": ["nuclear weapon (D-011 deterrence abstraction; knowledge only, no mechanic)"],
    "balloon": ["lighter-than-air flight"],
}

# Display names of NEW nodes shortened so every name fits the narrowest research card on two lines (UR-4,
# Sim.Ui.Tests ReadabilityStageBTests); the parenthetical detail stays in the description and the proposal.
NAME_OVERRIDES = {
    "geophysical_prospecting": "Geophysical prospecting",
    "uranium_milling": "Radioactive ore processing",
    "nuclear_medicine": "Nuclear medicine",
    "nuclear_physics": "Nuclear physics",
    "light_water_reactor": "Light-water reactors",
    "structural_iron": "Structural iron",
    "sanitary_engineering": "Sanitary engineering",
    "ore_prospecting": "Ore prospecting",
    "geological_disposal": "Geological waste disposal",
    "mineralogy": "Systematic mineralogy",
    "uranium_enrichment": "Uranium enrichment",
    "radioisotope_production": "Radioisotope production",
    "radiation_protection": "Radiation protection",
    "radiotherapy": "Radiotherapy",
    "metallography": "Metallography",
}

# In-game descriptions (short game text; the educational corpus is separate and not shipped).
NEW_DESC = {
    "cotton_cultivation": "Growing cotton as a fibre crop, domesticated independently in South Asia, Africa and the Americas — a second great plant fibre beside flax and wool.",
    "ashlar_masonry": "Cutting and dressing stone blocks to fit tightly — the masonry of pyramids, temples, towers and stone arches.",
    "cupellation": "Separating silver from lead by burning the lead away in a hot hearth; the same fire assay tests gold and silver.",
    "mesoamerican_script": "Full writing grown out of calendar notation — the dated monuments and bark-paper books of the Zapotec and Maya.",
    "ore_prospecting": "Reading the land for ore: coloured stains, weathered outcrops and stream placers that betray copper and tin.",
    "counting_rods": "Decimal place-value arithmetic with rods laid on a counting board, including negative numbers and systems of equations.",
    "drawloom": "A loom whose harness stores a pattern, weaving figured silks and damasks.",
    "traction_trebuchet": "A lever-and-sling stone thrower powered by a crew hauling on ropes.",
    "ocean_junk": "Compartmented ocean-going hulls navigated by compass and the monsoon winds.",
    "percussion_drilling": "Cased boreholes hammered hundreds of metres deep to reach brine and natural gas.",
    "rifling": "Spiral grooves in the barrel spin the bullet for accurate aimed fire at long range.",
    "physiology": "How the living body works, studied by experiment — beginning with the circulation of the blood.",
    "optical_telegraph": "Chains of semaphore towers relaying coded signals across a country within hours.",
    "mineralogy": "Identifying minerals by their physical properties and by blowpipe and chemical tests.",
    "probability_statistics": "The mathematics of chance, and of drawing conclusions from counts and measurements.",
    "taxonomy": "Naming and classifying living species in one orderly system.",
    "breech_loading_rifle": "Rifles loaded at the breech with cartridges, so soldiers can fire rapidly from cover.",
    "machine_gun": "Weapons that keep firing while the trigger is held.",
    "rifled_artillery": "Breech-loading rifled guns firing explosive shells accurately at long range.",
    "epidemiology": "Studying disease through its rates in a population to find how it spreads.",
    "ac_transmission": "Transformers and alternating current carry electric power over long high-voltage lines.",
    "automatic_switching": "Telephone exchanges that connect calls automatically by relays, without an operator.",
    "balloon": "Lighter-than-air flight with hot air or hydrogen.",
    "gas_engine": "A stationary engine burning gas inside its cylinder — compact power for workshops without a boiler.",
    "glider": "Flight on fixed wings, held up by aerodynamic lift and controlled in three axes.",
    "metallography": "Reading the structure of metals under the microscope to explain hardening, tempering and alloys.",
    "punched_card_tabulation": "Machines that count and sort records punched on cards, first built for a census.",
    "sanitary_engineering": "Sewers that carry waste away in water, and filtered, piped water supplies for towns.",
    "structural_iron": "Iron frames and girders for bridges, mills and great halls of iron and glass.",
    "water_turbine": "An efficient enclosed water wheel for high heads, later the driver of hydroelectric stations.",
    "cell_theory": "All living things are made of cells, and disease begins in cells.",
    "electromagnetic_field_theory": "Electric and magnetic fields as one theory: light is an electromagnetic wave, and such waves can be made — the theory behind radio.",
    "organic_chemistry": "The chemistry of carbon compounds — analysing, synthesising and drawing their structures.",
    "periodic_system": "Ordering the chemical elements by their properties, so that the gaps predict elements not yet found.",
    "spectroscopy": "Identifying substances by the lines in their light — even the composition of the stars.",
    "stratigraphy": "Reading rock layers in order and mapping them, to tell which formations may hold coal, ore or oil.",
    "thermodynamics": "The laws of energy and entropy, which set the limits of every heat engine.",
    "agricultural_chemistry": "What plants take from the soil — nitrogen, phosphorus and potash — tested in field experiments.",
    "chemical_fertilizer": "Manufactured superphosphate and potash fertilizers spread on the fields.",
    "farm_machinery": "Horse-drawn reapers, threshing machines and factory-made iron ploughs.",
    "fission_weapon": "A weapon releasing the energy of an uncontrolled chain reaction. Knowledge only: no mechanic uses it.",
    "guided_missile": "Rocket weapons steered to their target by radio, radar or heat-seeking guidance.",
    "submarine": "A warship that dives, running on batteries underwater and on engines at the surface.",
    "endocrinology": "Hormones and the glands that make them — insulin for diabetes and hormone replacement.",
    "immunology": "How the body defends itself with antibodies — serum therapy, blood groups and antibody tests.",
    "nuclear_medicine": "Radioactive tracers to see inside the body, and radioactive drugs to treat disease.",
    "radiation_protection": "Measuring radiation doses and limiting them by time, distance and shielding.",
    "radiotherapy": "Treating cancer with radium sources and beams of radiation.",
    "sulfonamides": "The first drugs to cure bacterial infections from within the body.",
    "communications_satellite": "Satellites relaying telephone calls, television and data between continents.",
    "fast_breeder_reactor": "Fast reactors that make more nuclear fuel than they burn. Knowledge only.",
    "geological_disposal": "Isolating long-lived radioactive waste deep in stable rock. Knowledge only.",
    "graphite_reactor": "Reactors moderated by graphite and fuelled with natural uranium. Knowledge only.",
    "heavy_water_reactor": "Reactors moderated by heavy water and fuelled with natural uranium. Knowledge only.",
    "light_water_reactor": "Pressurised- and boiling-water reactors running on enriched uranium. Knowledge only.",
    "orbital_launch_vehicle": "A rocket powerful enough to place a payload in orbit.",
    "programmable_computer": "Machines that calculate automatically from a program set by switches, plugboards or tape.",
    "radio_broadcasting": "Voice transmission and scheduled broadcasts to anyone with a receiver.",
    "reprocessing": "Chemically recovering uranium and plutonium from spent reactor fuel. Knowledge only.",
    "uranium_enrichment": "Raising the share of fissile uranium-235 in uranium. Knowledge only.",
    "uranium_milling": "Processing radioactive ores — first for radium, then into uranium concentrate.",
    "dna_sequencing": "Reading the order of the bases in a DNA molecule.",
    "geophysical_prospecting": "Magnetic, electrical, gravity and seismic surveys that reveal structures hidden underground.",
    "nuclear_physics": "The structure of the atomic nucleus: isotopes, the neutron and the splitting of uranium.",
    "radiation_detection": "Counters, cloud chambers and film badges that detect and measure radiation.",
    "radioactivity": "Some elements give off rays as they change into others — radium and polonium are found.",
    "radioisotope_production": "Making radioactive isotopes in accelerators and reactors for tracing, testing and sterilising.",
    "radiometric_prospecting": "Surveying the ground, on foot, by car or from the air, for the gamma rays of uranium and thorium.",
    "semiconductor_physics": "The band theory of solids and the junction between differently treated semiconductors.",
    "scientific_plant_breeding": "Planned crosses and selection on Mendelian lines, including hybrid seed.",
}

CIVIC_DESC = {
    "kin_council": "Heads of households meet to decide common matters and settle disputes through the mediation of elders.",
    "hereditary_rank": "Rank and office pass down within leading families.",
    "oath_alliance": "Communities bind themselves in alliance by oath, gifts and marriage.",
    "organized_priesthood": "A temple priesthood keeps the ritual calendar and manages temple estates.",
    "chiefdom": "Many villages answer to a paramount chief.",
    "tribal_confederacy": "Autonomous tribes swear themselves into a league.",
    "appointed_officials": "Officials appointed and dismissed by the ruler govern by written order.",
    "corvee_labour": "Subjects owe the state labour service, worked in rotating gangs.",
    "elite_council": "A council of the leading families deliberates on war, law and finance.",
    "royal_envoys": "Accredited envoys carry sealed letters between rulers.",
    "sacral_kingship": "Kingship sanctioned by the gods and upheld by temple ritual.",
    "service_tenure": "Land is granted in return for military service.",
    "succession_law": "A fixed rule names the ruler's heir.",
    "sworn_treaties": "Written treaties, sworn before the gods, bind both parties.",
    "city_state_oligarchy": "A city governed by a council of its leading families.",
    "territorial_kingship": "A hereditary king rules a realm of many settlements.",
    "citizen_assembly": "An assembly of free members decides by vote.",
    "elected_magistracies": "Executive offices are filled by election for fixed terms.",
    "land_registry": "A public register records land, its holders and the dues owed on it.",
    "provincial_administration": "Provinces are governed by appointed governors who answer to the capital.",
    "standing_army": "A permanent army is paid and supplied by the state.",
    "tax_farming": "The right to collect taxes is contracted to private tax farmers.",
    "theocracy": "Religious office holds the government, under religious law.",
    "craft_guilds": "Chartered associations of master craftsmen control admission to and training in their trade.",
    "municipal_charter": "A written charter grants a town self-government.",
    "religious_toleration": "The law tolerates more than one faith.",
    "assembly_democracy": "All citizens govern together in an assembly.",
    "bureaucratic_empire": "An empire is governed through salaried officials and provinces.",
    "classical_republic": "A republic of elected magistrates, a senate and popular assemblies.",
    "civil_service_examinations": "Officials are recruited by competitive written examination.",
    "commercial_law": "Merchant courts enforce contracts between strangers.",
    "representative_assembly": "Representatives of estates and towns meet to consent to taxation.",
    "feudal_monarchy": "A monarchy of vassals who hold their fiefs in return for service.",
    "merchant_republic": "A republic governed by councils of merchants.",
    "military_rule": "The armed forces hold the government.",
    "bill_of_rights": "Rights are guaranteed by law against the ruler and enforceable in court.",
    "central_banking": "A public bank holds the state's accounts and issues notes.",
    "civil_police": "A salaried police force keeps order under public authority.",
    "compulsory_education": "The law requires children to attend school.",
    "law_of_nations": "A law among sovereign states governs envoys, treaties, neutrality and war.",
    "official_statistics": "A statistical office publishes regular tables of population, births and deaths.",
    "public_health_board": "A public authority holds powers of quarantine and sanitation.",
    "resident_embassies": "Ambassadors reside permanently at foreign courts.",
    "separation_of_powers": "Legislative, executive and judicial powers are held by separate bodies.",
    "written_constitution": "A fundamental written law establishes and limits the organs of government.",
    "absolute_monarchy": "A monarch rules without the consent of the estates.",
    "constitutional_monarchy": "A monarchy is bound by a constitution and a legislature.",
    "federal_republic": "Sovereignty is divided between a union and its member states.",
    "presidential_republic": "A republic with a separately elected president.",
    "abolition": "Slavery and hereditary bondage are abolished by law.",
    "general_staff": "A permanent staff of officers plans mobilization and operations.",
    "laws_of_war": "International conventions protect the wounded, prisoners and civilians in war.",
    "mass_political_party": "Permanent membership parties compete in elections.",
    "mass_suffrage": "All adults vote by secret ballot from a public register.",
    "ministerial_responsibility": "Ministers hold office only while they keep the confidence of the elected chamber.",
    "universal_conscription": "Young men are registered and called up for military service.",
    "parliamentary_government": "A cabinet responsible to parliament governs.",
    "central_planning": "The state plans output and allocation for the whole economy.",
    "marxist_leninist_state": "A communist party-state runs a planned economy.",
    "one_party_state": "A single legal party holds the government.",
}
CIVIC_DESC_TAIL = " Knowledge only for now: adopting a form of government arrives with the government layer (M8)."

# --------------------------------------------------------------------------------------------------
# predicate helpers (the research dialect: identifiers, AND / OR / NOT, parentheses)
# --------------------------------------------------------------------------------------------------
_TOK = re.compile(r"\s*(\(|\)|[A-Za-z_][A-Za-z0-9_.]*)")


def toks(expr):
    out, pos = [], 0
    while pos < len(expr):
        if expr[pos:].strip() == "":
            break
        m = _TOK.match(expr, pos)
        if not m:
            raise ValueError(f"cannot tokenize {expr!r}")
        out.append(m.group(1))
        pos = m.end()
    return out


def parse(expr):
    t = toks(expr)
    i = [0]

    def peek():
        return t[i[0]] if i[0] < len(t) else None

    def take():
        i[0] += 1
        return t[i[0] - 1]

    def p_or():
        xs = [p_and()]
        while peek() == "OR":
            take()
            xs.append(p_and())
        return xs[0] if len(xs) == 1 else ("or", xs)

    def p_and():
        xs = [p_prim()]
        while peek() == "AND":
            take()
            xs.append(p_prim())
        return xs[0] if len(xs) == 1 else ("and", xs)

    def p_prim():
        x = take()
        if x == "(":
            e = p_or()
            assert take() == ")"
            return e
        assert x not in KEYWORDS, expr
        return ("atom", x)

    e = p_or()
    assert i[0] == len(t), expr
    return e


def atoms(expr):
    if not expr:
        return []
    out = []
    for x in toks(expr):
        if x in ("(", ")") or x in KEYWORDS:
            continue
        if x not in out:
            out.append(x)
    return out


def must_atoms(ast):
    if ast is None:
        return set()
    if ast[0] == "atom":
        return {ast[1]}
    sets = [must_atoms(x) for x in ast[1]]
    if ast[0] == "and":
        r = set()
        for s in sets:
            r |= s
        return r
    r = sets[0]
    for s in sets[1:]:
        r &= s
    return r


def holds(ast, done):
    if ast is None:
        return True
    if ast[0] == "atom":
        return ast[1] in done
    if ast[0] == "and":
        return all(holds(x, done) for x in ast[1])
    return any(holds(x, done) for x in ast[1])


def norm(expr):
    return None if expr is None else re.sub(r"\s+", " ", expr.strip())


# --------------------------------------------------------------------------------------------------
# D-047 chronology heuristic (ResearchContentTests.EarliestMarkedYear, reproduced)
# --------------------------------------------------------------------------------------------------
_MARK = re.compile(r"(\d[\d,]*(?:\.\d+)?)(?:\s*[-–/]\s*(\d[\d,]*(?:\.\d+)?))?\s*(BCE|CE|kya|Mya)\b")


def earliest(emerged):
    if emerged is None:
        return None
    best = None
    for m in _MARK.finditer(emerged):
        for g in (1, 2):
            if m.group(g) is None:
                continue
            v = float(m.group(g).replace(",", ""))
            u = m.group(3)
            y = v if u == "CE" else -v if u == "BCE" else -v * 1000.0 if u == "kya" else -v * 1e6
            if best is None or y < best:
                best = y
    return best


# --------------------------------------------------------------------------------------------------
# record builders
# --------------------------------------------------------------------------------------------------

def tier_for(novelty, old_tier=None):
    if novelty == 0.5:
        return "T1"
    if novelty == 2.0:
        return "T4"
    return old_tier if old_tier in ("T2", "T3") else "T2"


def eureka_record(e):
    cls = e["class"]
    when = e.get("when")
    if cls == "machine-evaluable":
        assert when, e
        system = "goods state" if "stock_" in when else "research (knowledge state)"
    else:
        assert cls.startswith("future-system:"), cls
        system = cls[len("future-system:"):]
        when = None
    return {"text": e["text"], "when": when, "kind": e["kind"], "justification": e["justification"], "system": system,
            "evaluable_now": when is not None, "weight": e["weight"], "source": "authored", "class": cls}


def capability_strings(n, overrides):
    if n["id"] in overrides:
        return list(overrides[n["id"]])
    listed = (n.get("unlocks") or {}).get("capabilities") or []
    if listed:
        return list(listed)
    return [g[len("knowledge:"):] for g in n["capability"]["grants"] if g.startswith("knowledge:")]


def factors_of(f):
    return {k: float(f[k]) for k in FACTOR_ORDER}


def new_record(n, desc, tree):
    caps = capability_strings(n, NEW_CAPABILITY_OVERRIDES)
    unlocks = {"capabilities": caps, "units": [], "buildings": [], "institutions": [], "infrastructure": [],
               "activities": [], "projects": [], "techniques": [], "applications": []}
    fac = factors_of(n["factors"])
    mag = sum(fac[k] for k in FACTOR_ORDER)
    assert abs(mag - n["magnitude"]) < 1e-9, n["id"]
    rec = {"key": n["key"], "id": n["id"], "name": NAME_OVERRIDES.get(n["id"], n["name"]), "desc": desc, "age": n["age"], "frontier": False,
           "emerged": n["emerged"]}
    if tree == "technology":
        rec["branch"] = None if n["branch"] == "trunk" else n["branch"]
    rec.update({
        "domain": n["domain"], "secondaryDomains": list(n.get("secondaryDomains") or []), "depth": 0,
        "cost": float(n["cost"]), "prereq": n["prereq"],
        "eurekas": [eureka_record(e) for e in n["eurekas"]],
        "unlocks": unlocks, "family": n["family"], "generation": n["generation"], "effects": {"immediate": []},
        "repeatable": None,
        "universityRelevance": [{"type": u["type"], "name": UNI_NAMES[u["type"]], "role": u["role"]} for u in n["universityRelevance"]],
        "costRationale": {"tier": tier_for(fac["novelty"]), "factors": fac, "magnitude": n["magnitude"],
                          "content_cost": int(n["cost"]), "calibration_adjustment": 0,
                          "note": "ADR-045 (D-062) new node: " + n["costRationale"]},
    })
    return rec


# --------------------------------------------------------------------------------------------------
# main transformation
# --------------------------------------------------------------------------------------------------

UNI_NAMES = {}


def build(base, tech, civ):
    for u in base["universityTypes"]:
        name = u["name"]
        UNI_NAMES[u["key"]] = name[:-len(" University")] if name.endswith(" University") else name
    log = []          # (section, id, change, verdict, reason)
    techs = base["technologies"]
    by_id = {t["id"]: t for t in techs}
    pnodes = tech["nodes"]
    pby = {n["id"]: n for n in pnodes}

    # ---- existing technologies ----
    for n in pnodes:
        nid = n["id"]
        if n["status"] == "retired":
            log.append(("merge", nid, f"merge into {n['mergedInto']} (key {n['key']} retired)", "deferred",
                        "no key retirement and no merge in this tranche: needs the remap seam (KR-2)"))
            continue
        if n["status"] != "existing":
            continue
        rec = by_id[nid]
        if nid in MERGE_DEFERRED:
            log.append(("merge", nid, "merge-survivor edits", "deferred", MERGE_DEFERRED[nid]))
            continue
        kinds = []
        if n["name"] != rec["name"]:
            if nid in RENAME_DEFERRED:
                log.append(("rename", nid, f"'{rec['name']}' -> '{n['name']}'", "deferred", RENAME_DEFERRED[nid]))
            else:
                log.append(("rename", nid, f"'{rec['name']}' -> '{n['name']}'", "accepted", "name only; id and key unchanged"))
                rec["name"] = n["name"]
        nb = None if n["branch"] == "trunk" else n["branch"]
        if nb != rec["branch"]:
            log.append(("relocate", nid, f"{rec['branch'] or 'trunk'} -> {nb or 'trunk'}", "accepted",
                        "practitioner craft moved to the Main Technology Tree (D-044 R15, KD-04); no consumer reads the branch"))
            rec["branch"] = nb
        for f in ("age", "domain", "emerged", "generation", "family"):
            if n.get(f) != rec.get(f):
                kinds.append(f)
                rec[f] = n.get(f)
        if list(n.get("secondaryDomains") or []) != rec["secondaryDomains"]:
            kinds.append("secondaryDomains")
            rec["secondaryDomains"] = list(n.get("secondaryDomains") or [])
        if norm(n["prereq"]) != norm(rec["prereq"]):
            log.append(("rewire", nid, f"'{rec['prereq']}' -> '{n['prereq']}'", "accepted",
                        "causal prerequisite edit; completed knowledge never decays, partial progress stays dormant until the new prerequisites hold"))
            rec["prereq"] = n["prereq"]
        cr = rec["costRationale"]
        if factors_of(n["factors"]) != factors_of(cr["factors"]) or float(n["cost"]) != rec["cost"]:
            old = rec["cost"]
            newf = factors_of(n["factors"])
            if newf["novelty"] != float(cr["factors"]["novelty"]):
                cr["tier"] = tier_for(newf["novelty"], cr["tier"])
            cr["factors"] = newf
            cr["magnitude"] = n["magnitude"]
            cr["content_cost"] = int(n["cost"])
            rec["cost"] = float(n["cost"])
            cr["note"] = "ADR-045 (D-062) re-score: " + (n.get("costRationale") or n.get("changes") or "factors per technology-tree-proposal.json")
            log.append(("rescore", nid, f"{old:,.0f} -> {rec['cost']:,.0f} RP", "accepted",
                        "factor re-score from content (never to hit an Age duration, D-046 §1)"))
        uni = [{"type": u["type"], "name": UNI_NAMES[u["type"]], "role": u["role"]} for u in n["universityRelevance"]]
        if uni != rec["universityRelevance"]:
            kinds.append("universityRelevance")
            rec["universityRelevance"] = uni
        if nid in CAPABILITY_OVERRIDES and CAPABILITY_OVERRIDES[nid] != rec["unlocks"]["capabilities"]:
            log.append(("capability", nid, f"{rec['unlocks']['capabilities']} -> {CAPABILITY_OVERRIDES[nid]}",
                        "revised" if nid in CAPABILITY_NOTES else "accepted",
                        CAPABILITY_NOTES.get(nid, "capability strings follow the proposal record's 'changes'")))
            rec["unlocks"]["capabilities"] = list(CAPABILITY_OVERRIDES[nid])
        elif nid in CAPABILITY_NOTES:
            log.append(("capability", nid, "capability string move", "deferred", CAPABILITY_NOTES[nid]))
        if nid in DESC_OVERRIDES:
            log.append(("description", nid, "in-game description updated to the node's new scope", "accepted", "name / scope change"))
            rec["desc"] = DESC_OVERRIDES[nid]
        if kinds:
            log.append(("metadata", nid, ", ".join(kinds), "accepted", "metadata only (Age relevance, domains, dates, family, university classification)"))
        if "split" in n["disposition"]:
            log.append(("split", nid, f"split into {', '.join(n.get('splitInto') or [])}", "accepted" if nid not in SPLIT_REVISED else "revised",
                        SPLIT_REVISED.get(nid, "the new part is a new key; every consumer outside the node graph stays on the existing key")))

    # ---- new technologies ----
    new_techs = []
    for n in pnodes:
        if n["status"] != "new":
            continue
        rec = new_record(n, NEW_DESC[n["id"]], "technology")
        new_techs.append(rec)
        if n["id"] in NAME_OVERRIDES:
            log.append(("rename", n["id"], f"(new node) '{n['name']}' -> '{NAME_OVERRIDES[n['id']]}'", "revised",
                        "display name shortened to fit the narrowest research card on two lines (UR-4); the detail stays in the description"))
        log.append(("new", n["id"], f"key {n['key']} {n['branch']} {n['age']} {n['cost']:,.0f} RP"
                    + (f" (split from {n['splitFrom']})" if n.get("splitFrom") else ""), "accepted",
                    "knowledge-only node (capability strings) unless an entity is named below"))
    techs.extend(new_techs)
    techs.sort(key=lambda t: t["key"])

    # ---- new civics (knowledge-only) ----
    new_civics = []
    for n in civ["nodes"]:
        if n["status"] != "new":
            continue
        assert not any(v for k, v in (n.get("unlocks") or {}).items() if k != "capabilities"), n["id"]
        rec = new_record(n, CIVIC_DESC[n["id"]] + CIVIC_DESC_TAIL, "civics")
        new_civics.append(rec)
        log.append(("civic", n["id"], f"key {n['key']} {n['age']} {n['cost']:,.0f}", "accepted",
                    "knowledge-only civic: capability strings, no entity kind (adoption M8)"))
    civics = base["civics"] + new_civics

    # ---- entities ----
    ents = base["entities"]
    eby = {e["id"]: e for e in ents}
    for ch in tech["entityChanges"]:
        e = eby[ch["entity"]]
        verdict, written, reason = ENTITY_DECISIONS[ch["entity"]]
        assert norm(e["requires"]) == norm(ch["oldRequires"]), ch["entity"]
        if verdict == "accepted":
            e["requires"] = ch["newRequires"]
        elif verdict == "revised":
            e["requires"] = written
        log.append(("entity", ch["entity"], f"'{ch['oldRequires']}' -> '{ch['newRequires']}'"
                    + (f" (written: '{written}')" if verdict == "revised" else ""), verdict, reason))
    for eid, (node, reason) in IMPLIED_WIDENINGS.items():
        e = eby[eid]
        old = e["requires"]
        e["requires"] = f"{old} OR {node}"
        log.append(("entity", eid, f"'{old}' -> '{e['requires']}'", "accepted", "widening: " + reason))

    nodes = techs + civics
    ids = [x["id"] for x in nodes]
    idset = set(ids)
    assert len(idset) == len(ids)

    # ---- reverse index (unlock lists) from the authoritative entity requirements ----
    named = {i: [] for i in ids}
    for e in ents:
        if e["requires"] is None:
            continue
        for a in atoms(e["requires"]):
            if a in idset and e["id"] not in named[a]:
                named[a].append(e["id"])
    for x in nodes:
        u = x["unlocks"]
        want = named[x["id"]]
        for lst, prefix in UNLOCK_KINDS:
            cur = u.get(lst)
            target = [eid for eid in want if eid.startswith(prefix)]
            if cur is None:
                if target:
                    u[lst] = target
                continue
            kept = [eid for eid in cur if eid in target]
            u[lst] = kept + [eid for eid in target if eid not in kept]

    # ---- depth (longest prerequisite path) ----
    asts = {x["id"]: (parse(x["prereq"]) if x["prereq"] else None) for x in nodes}
    depth = {}

    def d(i, stack=()):
        if i in depth:
            return depth[i]
        assert i not in stack, f"cycle at {i}"
        v = 0
        for a in atoms(pby_any(i)["prereq"]):
            v = max(v, d(a, stack + (i,)) + 1)
        depth[i] = v
        return v

    rec_by = {x["id"]: x for x in nodes}

    def pby_any(i):
        return rec_by[i]

    for x in nodes:
        x["depth"] = d(x["id"])

    # ---- recursive set: per-subtree finite counts ----
    recursive = set(base["researchSets"]["recursive"])
    finite = {}
    for t in techs:
        if t["branch"] is not None and t["id"] not in recursive:
            finite[t["branch"]] = finite.get(t["branch"], 0) + 1
    for t in techs:
        if t["id"] in recursive:
            av = t["repeatable"]["availability"]
            av["finite_nodes_to_exhaust"] = finite[av["subtree"]]

    validate(techs, civics, ents, asts, recursive, base)
    return techs, new_civics, ents, log


SPLIT_REVISED = {
    "electricity_generation": "ac_transmission added and the four grid dependants re-pointed to it; the infra.power_grid move and the rename are deferred (KR-2)",
    "rocket": "orbital_launch_vehicle added and satellite / reusable_launch re-pointed to it; the building.spaceport and project.orbital_launch moves are deferred (KR-2)",
    "radio": "radio_broadcasting added and television / mobile_phone re-pointed to it; the inst.mass_media_broadcast move is deferred (KR-2)",
    "stone_fortification": "ashlar_masonry added upstream; building.lighthouse widened to 'ashlar_masonry OR stone_fortification' instead of moved",
}


def validate(techs, civics, ents, asts, recursive, base):
    nodes = techs + civics
    keys = [x["key"] for x in nodes]
    assert all(keys[i] < keys[i + 1] for i in range(len(keys) - 1)), "keys must ascend"
    branch = {t["id"]: t["branch"] for t in techs}
    for t in techs:   # trunk rule
        if t["branch"] is None:
            for a in atoms(t["prereq"]):
                assert branch.get(a) is None, f"trunk {t['id']} requires subtree {a}"
    deps = {x["id"]: 0 for x in nodes}
    for x in nodes:
        for a in atoms(x["prereq"]):
            assert a in deps, f"{x['id']}: unknown prerequisite {a}"
            deps[a] += 1
    for x in nodes:   # orphans
        u = x["unlocks"]
        ent = sum(len(u.get(k) or []) for k, _ in UNLOCK_KINDS)
        assert deps[x["id"]] or ent or u["capabilities"], f"orphan {x['id']}"
    # reachability before the stage (Main tree + civics), as the loader does
    stage = parse(base["researchStage"]["requires"])
    done = set()
    changed = True
    while changed:
        changed = False
        for x in nodes:
            if x["id"] in done or (x["id"] in branch and branch[x["id"]] is not None):
                continue
            if holds(asts[x["id"]], done):
                done.add(x["id"])
                changed = True
    for t in techs:
        assert t["branch"] is not None or t["id"] in done, f"main-tree {t['id']} unreachable before the stage"
    assert holds(stage, done), "stage deadlock"
    # D-047 chronology (the content test's rule)
    emerged = {x["id"]: earliest(x["emerged"]) for x in nodes}
    bad = []
    for x in nodes:
        y = emerged[x["id"]]
        if y is None or y <= -3000.0 or asts[x["id"]] is None:
            continue
        for a in sorted(must_atoms(asts[x["id"]])):
            ya = emerged[a]
            if ya is not None and ya > y:
                bad.append(f"{x['id']} ({y}) <- {a} ({ya})")
    assert not bad, "D-047 chronology: " + "; ".join(bad)
    for t in techs:
        if t["id"] in recursive:
            assert t["branch"] is not None


# --------------------------------------------------------------------------------------------------
# text output: keep every untouched section byte-identical to the base file
# --------------------------------------------------------------------------------------------------

def nested(value, level):
    s = json.dumps(value, indent=1, ensure_ascii=True)
    return s.replace("\n", "\n" + " " * level)


def splice(base_text, techs, legacy_civics, new_civics, ents):
    def span(name, nxt):
        a = base_text.index(f'\n "{name}": ')
        b = base_text.index(f'\n "{nxt}": ')
        return a, b
    a, b = span("technologies", "civics")
    tech_text = f'\n "technologies": {nested(techs, 1)},'
    out = base_text[:a] + tech_text
    c, e = span("civics", "entities")
    civ_text = base_text[b:e]
    # Legacy civics stay byte-identical except their DERIVED depth (it follows their technology prerequisites,
    # whose depths the rewires move); their unlock lists are asserted unchanged in build().
    for x in legacy_civics:
        at = civ_text.index(f'"key": {x["key"]},')
        m = re.compile(r'"depth": (\d+),').search(civ_text, at)
        civ_text = civ_text[:m.start()] + f'"depth": {x["depth"]},' + civ_text[m.end():]
    close = civ_text.rindex("\n ],")
    if new_civics:
        body = ",\n  ".join(nested(x, 2) for x in new_civics)
        civ_text = civ_text[:close] + ",\n  " + body + civ_text[close:]
    out += civ_text
    f, g = span("entities", "researchSets")
    out += f'\n "entities": {nested(ents, 1)},' + base_text[g:]
    return out


# --------------------------------------------------------------------------------------------------
# status document
# --------------------------------------------------------------------------------------------------

ORDER = ["new", "civic", "split", "rewire", "rescore", "relocate", "rename", "capability", "description", "metadata",
         "entity", "merge"]
TITLES = {
    "new": "New technology nodes (keys 427-496)", "civic": "New civics (keys 1008-1067, knowledge-only)",
    "split": "Splits (the new part is a new key)", "rewire": "Rewires (prerequisite edits on existing keys)",
    "rescore": "Re-scores (factor and cost edits on existing keys)", "relocate": "Relocations to the Main Technology Tree",
    "rename": "Renames", "capability": "Capability-string updates on existing keys",
    "description": "Description updates on existing keys", "metadata": "Metadata (Age relevance, domains, dates, family, university classification)",
    "entity": "Entity requirement changes", "merge": "Merges and retirements",
}


def status_markdown(log, tech, civ):
    w = []
    w.append("# Implementation status of the knowledge-redesign proposal (ADR-045, D-062)")
    w.append("")
    w.append("GENERATED by `tools/implement_tranche.py` from `technology-tree-proposal.json`, `civics-tree-proposal.json` and the")
    w.append("pre-tranche content (`Sim.Tests/Fixtures/research-pre-adr045.json`). Do not edit by hand; re-run the tool.")
    w.append("")
    w.append("The tranche is NON-BREAKING: no key retirement, no merge, and no split or re-point that removes a consumer")
    w.append("(an entity requirement) from an existing key — those need the remap seam (roadmap KR-2) and are DEFERRED.")
    w.append("Verdicts: **accepted** (implemented as proposed) · **revised** (implemented in a changed form, reason given) ·")
    w.append("**deferred** (not implemented in this tranche, reason given) · **rejected** (not to be implemented).")
    w.append("Not in this tranche at all: costs in Civics Points, regime adoption (M8), unit entities for the new military")
    w.append("nodes (T6.12), identification / prospecting grants (docket A-5; Track EX owns the M6 Prospect action), the")
    w.append("educational corpus (0 of 561 texts may ship). Content descriptions are short game text written for this tranche.")
    w.append("")
    counts = {}
    for sec, _, _, verdict, _ in log:
        counts.setdefault(sec, {}).setdefault(verdict, 0)
        counts[sec][verdict] += 1
    w.append("## Summary")
    w.append("")
    w.append("| Change | accepted | revised | deferred | rejected |")
    w.append("|---|---|---|---|---|")
    for sec in ORDER:
        c = counts.get(sec, {})
        w.append(f"| {TITLES[sec]} | {c.get('accepted', 0)} | {c.get('revised', 0)} | {c.get('deferred', 0)} | {c.get('rejected', 0)} |")
    w.append("")
    for sec in ORDER:
        rows = [r for r in log if r[0] == sec]
        if not rows:
            continue
        w.append(f"## {TITLES[sec]}")
        w.append("")
        w.append("| Node / entity | Change | Verdict | Reason |")
        w.append("|---|---|---|---|")
        for _, nid, change, verdict, reason in rows:
            w.append(f"| `{nid}` | {change.replace('|', '/')} | {verdict} | {reason.replace('|', '/')} |")
        w.append("")
    return "\n".join(w) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default=str(BASE))
    ap.add_argument("--out", default=str(OUT))
    ap.add_argument("--status", default=str(STATUS))
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    base_text = Path(args.base).read_text(encoding="utf-8")
    base = json.loads(base_text)
    tech = json.loads(TECH.read_text(encoding="utf-8"))
    civ = json.loads(CIVICS.read_text(encoding="utf-8"))
    legacy_unlocks = json.dumps([c["unlocks"] for c in base["civics"]])
    techs, new_civics, ents, log = build(base, tech, civ)
    legacy_civics = base["civics"]
    assert json.dumps([c["unlocks"] for c in legacy_civics]) == legacy_unlocks, "a legacy civic's unlock list moved"
    out_text = splice(base_text, techs, legacy_civics, new_civics, ents)
    reparsed = json.loads(out_text)
    assert reparsed["researchStage"] == base["researchStage"] and reparsed["tuning"] == base["tuning"]
    # researchStage and tuning belong to Track CP (ADR-043): whatever the CURRENT output file carries in those two blocks
    # is kept verbatim, so the tool stays reproducible (and --check meaningful) after CP's edits are merged.
    if Path(args.out).exists():
        current = Path(args.out).read_text(encoding="utf-8")
        for name, nxt in (("tuning", "baseline"), ("researchStage", "universityTypes")):
            a0, b0 = current.index(f'\n "{name}": '), current.index(f'\n "{nxt}": ')
            a1, b1 = out_text.index(f'\n "{name}": '), out_text.index(f'\n "{nxt}": ')
            out_text = out_text[:a1] + current[a0:b0] + out_text[b1:]
    status = status_markdown(log, tech, civ)
    if args.check:
        stale = [p for p, t in ((args.out, out_text), (args.status, status)) if Path(p).read_text(encoding="utf-8") != t]
        if stale:
            print("stale: " + ", ".join(stale))
            return 1
        print("up to date")
        return 0
    Path(args.out).write_text(out_text, encoding="utf-8", newline="\n")
    Path(args.status).write_text(status, encoding="utf-8", newline="\n")
    n_new = sum(1 for r in log if r[0] == "new")
    print(f"wrote {args.out} ({len(techs)} technologies, {len(base['civics']) + len(new_civics)} civics; {n_new} new technologies)")
    print(f"wrote {args.status}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

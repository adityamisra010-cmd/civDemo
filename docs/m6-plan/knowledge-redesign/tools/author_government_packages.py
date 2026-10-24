"""Author the government-form packages and the switching rule (PROPOSED design input for M8 adoption, 2026-10-24).

Director direction (2026-10-24): once a government-form node is researched (its prerequisites first, then the node), the
player may choose to switch to it; each form's advantages are visible on its node; there are no policy cards — what
Civilization VI spreads over Military / Economic / Diplomatic / Wildcard cards is fixed per government and built into it.

How that is kept inside the constitution: every built-in policy is a coefficient inside an existing or named resolution
equation (law 2: mechanisms, not free-floating buffs). `operand` names the sim.json key when the equation is live today
(`live: true`, value shown as current -> under this form) or the owning system and milestone when it is not yet built.
All values are TUNE.

Writes data/government-packages.json. Usage: python3 -I tools/author_government_packages.py
"""
import json
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PKG / "tools"))
import kr_lib as kr  # noqa: E402

# Live operands (sim.json / research.json today): key -> (label, current value)
LIVE = {
    "governance.authorityDecayCostUnits": ("administrative reach (e-fold of travel cost from the capital)", 25.0),
    "governance.taxExtractionResponseMax": ("tax extraction response (output multiplier at full rate)", 0.3),
    "trade.gapClosingFraction": ("how fast trade closes price gaps between settlements", 0.25),
    "trade.costPerBulkCostUnit": ("trade cost per unit of bulk haulage", 0.16),
    "mobility.promoteRatePerYear": ("class promotion rate per year", 0.08),
    "migration.baseRatePerYear": ("base migration rate per year", 0.03),
    "migration.dampingDecayCostUnits": ("how far migrants travel (e-fold of travel cost)", 25.0),
    "granaries.famineReductionMax": ("famine mortality relief from granaries", 0.25),
    "price.stockReleaseRatePerYear": ("how fast traders release stocks onto the market", 0.5),
    "colonization.orderPartyFraction": ("share of a settlement sent with a new colony", 0.2),
}

def L(cat, title, operand, value, why):
    label, cur = LIVE[operand]
    return {"category": cat, "title": title, "operand": operand, "operandLabel": label, "live": True,
            "from": cur, "to": value, "why": why}

def F(cat, title, owner, why):
    return {"category": cat, "title": title, "operand": owner, "live": False, "why": why}

SWITCHING = {
    "eligibility": "The form's node is researched (which already requires every prerequisite). Research is permanent, so "
                   "an eligible form stays eligible.",
    "order": "AdoptGovernment(form) — one order, the same for player and AI (parity); validated at preview and delivery.",
    "firstAdoption": "A polity starts under kin rule (no form). Its first adoption is immediate and costs nothing.",
    "cost": "Each later switch costs Civics Points equal to 25 % of the new form's research cost (TUNE), paid on the "
            "turn the order is delivered; refused if the bank is short (nothing paid).",
    "transition": "A transition of 2 sim-years plus 1 year for each class whose enfranchisement changes (TUNE): the old "
                  "package is off, the new one not yet on, and tax extraction runs at half its rate. Integrated over "
                  "dtYears, never counted in turns.",
    "cooldown": "No new switch while a transition runs, and none within 10 sim-years of the last delivered switch "
                "(elapsed sim-time since the polity's own act — state, not a calendar gate).",
    "exchange": "On the switch turn the RP<->CP exchange of cp-economy-proposal §3 may ride on the same order.",
    "resistance": "Classes that lose enfranchisement gain grievance (NeedsGrievance, M9); until M9 this line is shown "
                  "but has no effect.",
    "milestone": "M8 (D-D, D-062 R3: adoption and active political effects are M8). Until then the node shows the "
                 "package as 'available when government switching arrives'.",
}

# form -> (power base, legitimacy, civ6 analog, civ7 analog, built-in policies, trade-offs)
P = {
    "chiefdom": ("The chief and the elders of the leading lineage", "descent and generous redistribution",
                 "Chiefdom", "Despotism",
                 [F("Military", "Kin levy: every household sends its men for short raids", "Battle layer (M7) — levy share and campaign length", "raiding parties, not standing forces"),
                  L("Economic", "The chief's store feeds the hungry", "granaries.famineReductionMax", 0.30, "redistribution from the chief's stores in dearth")],
                 [L("Trade-off", "Rule fades quickly beyond the kin group", "governance.authorityDecayCostUnits", 20.0, "authority is personal"),
                  L("Trade-off", "Tribute, not taxation", "governance.taxExtractionResponseMax", 0.15, "no officials to assess and collect")]),
    "tribal_confederacy": ("A council of clan chiefs under an elected war leader", "sworn oaths between clans",
                           "Chiefdom", "—",
                           [F("Diplomatic", "Sworn confederates defend each other", "Diplomacy (M8) — shared-defence obligation", "member clans are bound by oath"),
                            F("Military", "A combined war host gathers for great campaigns", "Battle layer (M7) — allied mobilization", "bands march together under the war leader"),
                            L("Economic", "Gift exchange between member clans", "trade.gapClosingFraction", 0.30, "kin and oath ties ease exchange")],
                           [L("Trade-off", "No central levy", "governance.taxExtractionResponseMax", 0.15, "each clan keeps its own"),
                            F("Trade-off", "War needs the council's consent", "Adoption/Diplomacy (M8) — war declaration rule", "slow to act")]),
    "city_state_oligarchy": ("A council of wealthy notables", "the notables' law and property",
                             "Oligarchy", "Oligarchy",
                             [F("Military", "Notables serve as officers and heavy troops", "Battle layer (M7) — training and experience", "a trained officer class"),
                              L("Economic", "Notables collect dues efficiently around the city", "governance.taxExtractionResponseMax", 0.35, "local assessment by those who own the land"),
                              F("Society", "The council funds public works", "ConstructionSystem (M8 policy) — project priority", "building as a mark of status")],
                             [L("Trade-off", "Rule is city-centred", "governance.authorityDecayCostUnits", 22.0, "notables rule what they can reach"),
                              L("Trade-off", "A closed elite", "mobility.promoteRatePerYear", 0.06, "newcomers rarely enter the council")]),
    "territorial_kingship": ("The king and his household officials", "dynasty and the gods' favour",
                             "Monarchy", "Despotism",
                             [L("Economic", "Officials govern distant districts by written order", "governance.authorityDecayCostUnits", 30.0, "appointed officials extend reach"),
                              F("Military", "A royal levy and garrisons", "Battle layer (M7) — levy and garrison upkeep", "the king's own forces"),
                              F("Diplomatic", "Royal envoys to neighbouring courts", "Diplomacy (M8) — contact range", "standing contact between kings")],
                             [F("Trade-off", "Succession crises when the king dies", "Adoption/legitimacy (M8)", "everything rests on one person")]),
    "theocracy": ("The priesthood", "divine law",
                  "Theocracy", "Theocracy",
                  [L("Economic", "Temple stores relieve famine", "granaries.famineReductionMax", 0.30, "temple granaries"),
                   F("Society", "Shared faith binds the realm", "NeedsGrievance (M9) — Faith need met for the state religion", "temples meet the need for belonging"),
                   F("Military", "Holy war raises morale", "Battle layer (M7) — morale", "war blessed by the clergy")],
                  [L("Trade-off", "Trade with unbelievers is restricted", "trade.gapClosingFraction", 0.22, "religious law limits exchange"),
                   F("Trade-off", "Religious minorities resent the regime", "NeedsGrievance (M9)", "discrimination grievance")]),
    "assembly_democracy": ("The assembly of all adult male citizens", "the citizens' vote",
                           "Classical Republic", "Classical Republic",
                           [L("Economic", "Citizens vote their own taxes and pay them", "governance.taxExtractionResponseMax", 0.33, "consent raises compliance"),
                            F("Society", "Open debate produces new ideas", "Civics Points (M8) — enfranchised citizens weighted in CP generation", "public deliberation"),
                            F("Military", "Citizen militia: all citizens serve, in short seasons", "Battle layer (M7) — levy share", "hoplite service")],
                           [L("Trade-off", "Direct democracy works only at city scale", "governance.authorityDecayCostUnits", 20.0, "citizens must attend the assembly"),
                            F("Trade-off", "War needs an assembly vote", "Diplomacy (M8) — war declaration rule", "slow and volatile decisions")]),
    "bureaucratic_empire": ("The emperor and a salaried officialdom", "the throne's order and the officials' law",
                            "Autocracy", "Bureaucratic Monarchy",
                            [L("Economic", "Provinces run by appointed governors", "governance.authorityDecayCostUnits", 35.0, "a hierarchy of offices"),
                             L("Economic", "Regular assessment and collection", "governance.taxExtractionResponseMax", 0.35, "census and land registers"),
                             L("Society", "Careers open through office", "mobility.promoteRatePerYear", 0.10, "officials rise by service"),
                             F("Military", "A standing army paid from the levy", "Battle layer (M7) — upkeep", "professional garrisons")],
                            [F("Trade-off", "Officials draw salaries in grain", "Adoption (M8) — official upkeep via Ledger", "the state's own cost")]),
    "classical_republic": ("Elected magistrates and a senate of the propertied", "law and annual election",
                           "Classical Republic", "Classical Republic",
                           [L("Economic", "Enforceable contracts lower the cost of trade", "trade.costPerBulkCostUnit", 0.15, "courts and written contract"),
                            L("Society", "Wealth can buy entry to the elite", "mobility.promoteRatePerYear", 0.09, "census classes, not birth, decide rank"),
                            F("Society", "Enfranchised citizens add to civic thought", "Civics Points (M8) — enfranchised weight", "debate in the forum"),
                            F("Military", "Citizen legions under annual commanders", "Battle layer (M7)", "levy by census class")],
                           [F("Trade-off", "Ambitious generals with large armies", "Adoption/legitimacy (M8) — civil-war risk", "the republic's classic failure")]),
    "steppe_empire": ("The khan and the clan nobility", "the khan's victories and clan oaths",
                      "—", "Mongolia (civ-unique)",
                      [F("Military", "A decimal army of mounted archers", "Battle layer (M7) — mounted levy share", "every herder is a horseman"),
                       L("Economic", "Protected caravan roads", "trade.gapClosingFraction", 0.32, "the khan polices the routes"),
                       L("Economic", "Cheaper long-distance haulage", "trade.costPerBulkCostUnit", 0.14, "relay stations and safe passage"),
                       L("Society", "A people on the move", "migration.baseRatePerYear", 0.05, "pastoral mobility")],
                      [F("Trade-off", "The empire splits among heirs", "Adoption/legitimacy (M8) — appanage division at succession", "every son gets a share"),
                       L("Trade-off", "Weak hold on farming districts", "governance.taxExtractionResponseMax", 0.22, "tribute, not administration")]),
    "feudal_monarchy": ("The king and landed vassals", "oaths of fealty and inherited right",
                        "Monarchy", "Feudal Monarchy",
                        [F("Military", "Vassals owe knights and castle guard", "Battle layer (M7) — feudal levy", "service for land"),
                         L("Economic", "Vassals govern their own fiefs", "governance.authorityDecayCostUnits", 30.0, "reach through lords"),
                         F("Society", "A fixed order of estates", "ClassMobility (M9) — estate rules", "everyone knows their place")],
                        [L("Trade-off", "Vassals keep most of the revenue", "governance.taxExtractionResponseMax", 0.20, "dues stay in the manor"),
                         L("Trade-off", "Little social movement", "mobility.promoteRatePerYear", 0.04, "birth decides rank"),
                         F("Trade-off", "Overmighty vassals", "Adoption/legitimacy (M8) — rebellion risk", "lords with their own armies")]),
    "merchant_republic": ("Councils of merchant families", "commercial prosperity and the councils' law",
                          "Merchant Republic", "Plutocracy",
                          [L("Economic", "Trade policy set by traders", "trade.gapClosingFraction", 0.35, "merchants run the state"),
                           L("Economic", "Low-cost shipping and credit", "trade.costPerBulkCostUnit", 0.13, "state galleys, banking"),
                           L("Economic", "Warehouses release stock quickly", "price.stockReleaseRatePerYear", 0.6, "merchants sell into shortages"),
                           F("Diplomatic", "Consuls and commercial treaties", "Diplomacy (M8)", "trade diplomacy"),
                           F("Military", "Hired fleets and mercenaries", "Battle layer (M7) — mercenary contracts", "pay instead of levy")],
                          [L("Trade-off", "Little interest in the hinterland", "governance.authorityDecayCostUnits", 22.0, "rule follows the sea lanes"),
                           F("Trade-off", "Rural grievance", "NeedsGrievance (M9)", "peasants unrepresented")]),
    "military_rule": ("The officer corps", "force and the promise of order",
                      "Fascism (partial)", "Revolutionary Authoritarianism",
                      [F("Military", "The army comes first in supply and pay", "Battle layer (M7) — upkeep priority", "rule by soldiers"),
                       L("Economic", "Requisitions near the garrisons", "governance.taxExtractionResponseMax", 0.40, "collection at sword point")],
                      [L("Trade-off", "Arbitrary rule raises the cost of trade", "trade.costPerBulkCostUnit", 0.18, "insecure property"),
                       F("Trade-off", "Legitimacy lasts only while it wins", "Adoption/legitimacy (M8)", "coups and counter-coups")]),
    "elective_monarchy": ("Electors from the great magnates", "election by the magnates and sworn charters",
                          "Monarchy", "—",
                          [F("Society", "Orderly succession by election", "Adoption/legitimacy (M8) — no succession crisis", "the electors choose"),
                           F("Diplomatic", "Foreign princes may be elected", "Diplomacy (M8) — dynastic unions", "crowns as diplomatic prizes")],
                          [L("Trade-off", "Each election buys the magnates' votes with privileges", "governance.taxExtractionResponseMax", 0.22, "tax exemptions"),
                           L("Trade-off", "A weak centre", "governance.authorityDecayCostUnits", 22.0, "magnates rule their lands")]),
    "absolute_monarchy": ("The monarch and the court bureaucracy", "divine right and royal law",
                          "Monarchy", "Bureaucratic Monarchy",
                          [L("Economic", "Intendants enforce royal law in every province", "governance.authorityDecayCostUnits", 35.0, "a centralized administration"),
                           L("Economic", "Royal taxation without consent", "governance.taxExtractionResponseMax", 0.38, "the fiscal-military state"),
                           F("Military", "A standing army and a royal navy", "Battle layer (M7)", "permanent forces"),
                           F("Economic", "Mercantilist trade policy", "Adoption (M8) — trade policy", "export promotion")],
                          [L("Trade-off", "Nobles tied to court, not enterprise", "mobility.promoteRatePerYear", 0.06, "privilege by birth"),
                           F("Trade-off", "Heavy taxes stoke revolt", "NeedsGrievance (M9)", "tax burden grievance")]),
    "constitutional_monarchy": ("The crown in parliament, elected by property holders", "the constitution and parliament's consent",
                                "Monarchy / Democracy", "Constitutional Monarchy",
                                [L("Economic", "Taxes voted by parliament are paid", "governance.taxExtractionResponseMax", 0.36, "credible consent"),
                                 L("Economic", "Secure property lowers trade costs", "trade.costPerBulkCostUnit", 0.15, "courts and contracts"),
                                 L("Society", "Commoners can rise by wealth and merit", "mobility.promoteRatePerYear", 0.10, "open professions"),
                                 F("Diplomatic", "Alliances backed by parliament", "Diplomacy (M8)", "durable commitments")],
                                [F("Trade-off", "War needs parliament's money", "Diplomacy (M8) — war supply vote", "the crown cannot fight alone")]),
    "federal_republic": ("Member states and a federal government", "a written constitution dividing powers",
                         "Democracy", "Elective Republic",
                         [L("Economic", "Self-governing states extend the republic's reach", "governance.authorityDecayCostUnits", 40.0, "provinces govern themselves"),
                          L("Society", "Free movement across the union", "migration.dampingDecayCostUnits", 35.0, "no internal borders"),
                          F("Military", "A federal army and state militias", "Battle layer (M7)", "layered defence")],
                         [L("Trade-off", "Revenue shared with the states", "governance.taxExtractionResponseMax", 0.27, "federal and state taxes"),
                          F("Trade-off", "Secession risk", "Adoption/legitimacy (M8)", "states can leave")]),
    "presidential_republic": ("An elected president and congress", "election and the separation of powers",
                              "Democracy", "Elective Republic",
                              [L("Economic", "Taxes voted by elected representatives", "governance.taxExtractionResponseMax", 0.34, "consent"),
                               L("Economic", "Contract and property law ease trade", "trade.costPerBulkCostUnit", 0.15, "courts"),
                               L("Society", "Open careers", "mobility.promoteRatePerYear", 0.11, "no legal estates"),
                               F("Diplomatic", "Treaties need ratification", "Diplomacy (M8)", "credible but slow")],
                              [F("Trade-off", "Gridlock between president and congress", "Adoption (M8) — policy-change lag", "divided government")]),
    "parliamentary_government": ("A cabinet responsible to an elected parliament", "a parliamentary majority",
                                 "Democracy", "Elective Republic",
                                 [L("Economic", "Broad-based taxation by consent", "governance.taxExtractionResponseMax", 0.35, "the modern tax state"),
                                  L("Society", "Mass education and open careers", "mobility.promoteRatePerYear", 0.12, "meritocratic institutions"),
                                  F("Society", "Free press and parties feed civic thought", "Civics Points (M8) — enfranchised weight", "public opinion")],
                                 [F("Trade-off", "War weariness topples governments", "Battle layer (M7) / NeedsGrievance (M9)", "voters end wars"),
                                  F("Trade-off", "Coalitions fall", "Adoption (M8) — cabinet instability", "policy reversals")]),
    "revolutionary_republic": ("The nation in arms and its revolutionary committees", "popular sovereignty",
                               "—", "Revolutionary Republic",
                               [F("Military", "Mass levy of the whole nation", "Battle layer (M7) — levy share", "levée en masse"),
                                L("Society", "Estates abolished; careers open to talent", "mobility.promoteRatePerYear", 0.14, "privilege abolished"),
                                L("Economic", "Requisitions for the war effort", "governance.taxExtractionResponseMax", 0.38, "emergency collection")],
                               [L("Trade-off", "Blockade and disorder raise trade costs", "trade.costPerBulkCostUnit", 0.18, "foreign hostility"),
                                F("Trade-off", "Monarchies abroad turn hostile", "Diplomacy (M8)", "ideological war")]),
    "marxist_leninist_state": ("The ruling communist party", "the party's claim to lead the working class",
                               "Communism", "Communism",
                               [L("Economic", "The plan reaches every district", "governance.authorityDecayCostUnits", 40.0, "party and ministries everywhere"),
                                L("Economic", "The state takes the surplus", "governance.taxExtractionResponseMax", 0.45, "collectivized production"),
                                L("Society", "Promotion through the party and schools", "mobility.promoteRatePerYear", 0.12, "class origin reversed"),
                                F("Military", "Mass conscription", "Battle layer (M7)", "a people's army")],
                               [L("Trade-off", "Prices set by the plan, not the market", "trade.gapClosingFraction", 0.15, "administered prices"),
                                L("Trade-off", "Movement controlled", "migration.baseRatePerYear", 0.015, "internal passports"),
                                F("Trade-off", "Shortages of consumer goods", "NeedsGrievance (M9)", "planning failures")]),
    "one_party_state": ("A single ruling party", "the party's monopoly and its performance",
                        "Communism / Fascism", "Authoritarianism",
                        [L("Economic", "Party cells extend the state's reach", "governance.authorityDecayCostUnits", 35.0, "a parallel hierarchy"),
                         L("Economic", "Firm collection", "governance.taxExtractionResponseMax", 0.38, "no opposition to taxation")],
                        [L("Trade-off", "Movement watched", "migration.baseRatePerYear", 0.02, "registration and permits"),
                         F("Trade-off", "Legitimacy rests on results; succession is contested", "Adoption/legitimacy (M8)", "no lawful opposition")]),
    "fascist_state": ("The leader and the ultranationalist party", "the leader, the nation and mobilization",
                      "Fascism", "Fascism",
                      [F("Military", "Industry geared to arms", "Battle layer (M7) — war production priority", "rearmament"),
                       L("Economic", "Corporatist control of labour and capital", "governance.taxExtractionResponseMax", 0.40, "the state directs firms and unions"),
                       F("Society", "Propaganda suppresses dissent for a time", "NeedsGrievance (M9) — grievance damping", "mass rallies, censorship")],
                      [L("Trade-off", "Autarky cuts trade", "trade.gapClosingFraction", 0.18, "self-sufficiency drive"),
                       F("Trade-off", "Persecuted minorities and collapse on defeat", "NeedsGrievance (M9) / legitimacy (M8)", "the regime cannot survive losing")]),
}

CIV6_SLOTS = {"Chiefdom": "1 Military · 1 Economic", "Autocracy": "1 Military · 1 Economic · 1 Diplomatic · 1 Wildcard",
              "Oligarchy": "2 Military · 1 Economic · 1 Wildcard", "Classical Republic": "2 Economic · 1 Diplomatic · 1 Wildcard",
              "Monarchy": "2 Military · 1 Economic · 1 Diplomatic · 2 Wildcard", "Theocracy": "2 Military · 2 Economic · 1 Diplomatic · 1 Wildcard",
              "Merchant Republic": "1 Military · 2 Economic · 2 Diplomatic · 1 Wildcard", "Fascism": "4 Military · 1 Economic · 1 Diplomatic · 2 Wildcard",
              "Communism": "3 Military · 3 Economic · 1 Diplomatic · 1 Wildcard", "Democracy": "1 Military · 3 Economic · 2 Diplomatic · 2 Wildcard"}


def main():
    civ = {n["id"]: n for n in kr.load_json(PKG / "civics-tree-proposal.json")["nodes"]}
    forms = sorted(i for i, n in civ.items() if (n.get("civic") or {}).get("category") == "macro")
    missing = sorted(set(forms) - set(P)); extra = sorted(set(P) - set(forms))
    if missing or extra:
        raise SystemExit(f"package/form mismatch: missing {missing}, extra {extra}")
    out = {}
    for f in forms:
        power, legit, c6, c7, pol, trade = P[f]
        out[f] = {"powerBase": power, "legitimacy": legit,
                  "civ6": {"analog": c6, "slots": CIV6_SLOTS.get(c6.split(" (")[0].split(" / ")[0], "")},
                  "civ7": c7, "builtIn": pol, "tradeOffs": trade,
                  "modules": sorted(kr.atoms(kr.parse(civ[f]["prereq"])))}
    doc = {"status": "PROPOSED — design input for M8 adoption (D-D, D-062 R3); not ratified; values TUNE",
           "direction": "Director 2026-10-24: switch government once its node is researched; advantages shown on the node; "
                        "no policy cards — the Civ VI card slots are pre-chosen per government and built in.",
           "law2": "Every built-in policy is a coefficient inside a named resolution equation; none is a free-floating buff.",
           "civ6Source": "Civilization VI base ruleset Governments.xml (slot counts, bonus types)",
           "switching": SWITCHING, "forms": out}
    kr.dump_json(doc, PKG / "data" / "government-packages.json")
    print(f"wrote {len(out)} government packages")


if __name__ == "__main__":
    main()

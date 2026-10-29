"""STATIC codebook for the "tuple" encoding of telemetry/v4 lines.

Everything an encoder or decoder (or a reading AI) needs is in this file; the
codebook sheet (codebook.tsv) is generated from it by make_codebook.py.

Leaf values are typed tuples:
  ('n', text)  a JSON number, exactly as the writer printed it
  ('s', text)  a JSON string
  ('b', bool)  a JSON boolean

Default specs (an EMPTY field in the cell means "the default"):
  ('k', leaf)              a constant
  ('r', path)              same value as another field; path roots:
                             S = this settlement record, T = the turn object,
                             R = the current table row; '{r}' / '{r+N}' = row index (+N)
  ('pos', [leaf, ...])     constant chosen by row position (row 0, row 1, ...)
  ('idx',)                 the row position itself, as an integer
  ('nm', idkey, mapname)   the codebook name of the id in field `idkey` of the same row
  ('rowd', {row: spec}, fallback)   per-row default override
  ('elems', [spec, ...])   (double arrays) element-wise defaults; whole array must match
  ('own_attr',)            this settlement's value in the line-level A: table
  None                     no default: always written
"""

def N(t):
    return ('n', t)


def S(t):
    return ('s', t)


Z = ('k', N('0'))
ONE = ('k', N('1'))
TRUE = ('k', ('b', True))
FALSE = ('k', ('b', False))
MINUS1 = ('k', N('-1'))


def R(p):
    return ('r', p)


SECTORS_DEFAULT = ('elems', [('k', N(x)) for x in ('0.55', '0.15', '0.1', '0.12', '0.08')])

# ---------------------------------------------------------------- closed sets
SCHEMA_CODES = {'telemetry/v4': 'v4', 'telemetry/v3': 'v3', 'telemetry/v2': 'v2'}

REASONS = {1: 'growth', 2: 'initialEndowment', 3: 'harvest', 4: 'eaten', 5: 'births',
           6: 'deaths', 7: 'starvation', 8: 'produced', 9: 'inputsConsumed',
           10: 'toolWear', 11: 'housingBuilt', 12: 'housingDecayed',
           13: 'housingMaterials', 14: 'spoilage', 15: 'granaryOverflow',
           16: 'constructionMaterials'}
GOODS = {1: 'grain', 2: 'livestock', 3: 'fish', 4: 'timber', 5: 'stone', 6: 'clay',
         7: 'copper-ore', 8: 'tin-ore', 9: 'fiber', 10: 'hides', 11: 'bronze',
         12: 'tools', 13: 'pottery', 14: 'cloth'}
QUANTITIES = dict([(3, 'population'), (4, 'dwellings')] +
                  [(100 + g, n) for g, n in GOODS.items()])
CLASSES = {1: 'Peasants', 2: 'Artisans', 3: 'Merchants'}
NEEDS = {1: 'Sustenance', 2: 'Shelter', 3: 'Safety', 4: 'Health', 5: 'Belonging/Faith',
         6: 'Comfort', 7: 'Dignity/Liberty', 8: 'Prospects'}
NAME_MAPS = {
    'reason': (REASONS, 'reason-%d'),
    'quantity': (QUANTITIES, None),
    'good': (GOODS, None),
    'class': (CLASSES, 'class-%d'),
    'need': (NEEDS, 'need-%d'),
}

# enum string -> code (code must not contain , ; / | space " ~ and not be a number)
ENUMS = {
    'kind': {'SetRainBias': 'RB', 'LaborAllocation': 'LA', 'SectorAllocation': 'SA',
             'EnqueueConstruction': 'EC', 'SetTaxRate': 'TX', 'SetResearchTarget': 'RT',
             'AdvanceAge': 'AA', 'DevelopRoads': 'DR', 'FoundSettlement': 'FS',
             'CancelConstruction': 'CC', 'SetResourceAllocation': 'RA',
             'CancelConstructionEntry': 'CE'},
    'state': {'Normal': 'N', 'Stress': 'S', 'Severe': 'V', 'Famine': 'F'},
    'famine': {'None': 'N', 'Disaster': 'D', 'Abandonment': 'A', 'Both': 'B'},
}

# double non-finite strings -> code
NONFINITE = {'NaN': 'N', 'Infinity': 'I', '-Infinity': '-I'}

# the 5 constant strings (Observer.cs). Exact decoded text.
C1 = ("Opening + Births - Deaths + Inflow - Outflow - Closing; absorbs ONLY colonization "
      "(the party leaves by Ledger.Transfer and no row records it); 0 on every turn no "
      "settlement was founded; negative on a founding record (the party ARRIVED)")
C2 = ("GrainOpening + Harvest - Eaten - GrainClosing; absorbs spoilage + granary overflow "
      "(unsplit per settlement) AND any appropriation transfer AND colony provisions AND "
      "intra-polity grain relief (D-053 s2: grain a settlement sent to a hungry sibling of "
      "its own polity counts here at the donor; grain it received and ate counts here "
      "NEGATIVE); sum over all records == ledger spoilage + overflow on every turn; "
      "negative means grain ARRIVED by an unrecorded transfer (provisions on a founding "
      "record; otherwise a raid, or relief from its own polity, which leaves the "
      "recipient's granary empty)")
C3 = ("GAP: HousingSystem records only the dwelling stock and LastMaintenanceFraction; the "
      "per-settlement built/decayed split is a world ledger total (see TurnRecord.Dwellings)")
C4 = ("GAP: the From->To flow matrix, damping, viability products and gap scale are "
      "computed inside MigrationSystem and discarded; only per-settlement totals are recorded")
C5 = ("GAP: MigrationPlan.DesiredInflowAe is the adult-equivalent inflow the vacancy bound "
      "tests, summed over the gap AND flight channels together (MigrationSystem.Plan, the "
      "vacancy-bound loop); the planner forms no per-channel adult-equivalent split, so none "
      "is recorded. The per-channel HEAD counts are recorded (gapIn, flightIn).")

# ---------------------------------------------------------------- sections
# field: (jsonKey, type, default)
# types: i int literal | d double | b bool | e:<enum> | c const string | n:<map> name
#        da double array | legs (stock sources+sinks)
# A section is ('tuple', code, fields) or ('table', code, rowKeyOrder, fields, tableDefault).
# Field lists are in TUPLE (cell) order; JSON key order is in the templates below.

def nm(idkey, m):
    return ('nm', idkey, m)


TURN_SECTIONS = {
    't': ('tuple', [('turn', 'i', None), ('year', 'd', None), ('dtYears', 'd', None)]),
    'k': ('table', ['quantity', 'name', 'opening', 'closing', 'sources', 'sinks',
                    'reconciles', 'discrepancy'],
          [('opening', 'i', None), ('closing', 'i', None), (('sources', 'sinks'), 'legs', None),
           ('reconciles', 'b', TRUE), ('discrepancy', 'i', Z),
           ('quantity', 'i', ('pos', [N(str(q)) for q in sorted(QUANTITIES)])),
           ('name', 'n:quantity', nm('quantity', 'quantity'))], 'empty'),
    'p': ('tuple', [('births', 'i', None), ('naturalDeaths', 'i', None),
                    ('opening', 'i', R('T.stocks.0.opening')), ('closing', 'i', R('T.stocks.0.closing')),
                    ('starvation', 'i', Z), ('reconciles', 'b', TRUE), ('discrepancy', 'i', Z)]),
    'g': ('tuple', [('harvest', 'i', None), ('eaten', 'i', None), ('spoilage', 'i', Z),
                    ('overflow', 'i', Z), ('opening', 'i', R('T.stocks.2.opening')),
                    ('closing', 'i', R('T.stocks.2.closing')), ('endowment', 'i', Z),
                    ('reconciles', 'b', TRUE), ('discrepancy', 'i', Z)]),
    'w': ('tuple', [('built', 'i', Z), ('decayed', 'i', Z),
                    ('opening', 'i', R('T.stocks.1.opening')), ('closing', 'i', R('T.stocks.1.closing')),
                    ('reconciles', 'b', TRUE), ('discrepancy', 'i', Z)]),
    'q': ('table', ['good', 'name', 'opening', 'produced', 'inputsConsumed', 'toolWear', 'eaten',
                    'housingMaterials', 'constructionMaterials', 'closing', 'reconciles', 'discrepancy'],
          [('produced', 'i', Z), ('inputsConsumed', 'i', Z), ('eaten', 'i', Z),
           ('housingMaterials', 'i', Z), ('constructionMaterials', 'i', Z),
           ('opening', 'i', R('T.stocks.{r+3}.opening')), ('closing', 'i', R('T.stocks.{r+3}.closing')),
           ('toolWear', 'i', Z), ('reconciles', 'b', TRUE), ('discrepancy', 'i', Z),
           ('good', 'i', ('pos', [N(str(g)) for g in range(2, 15)])),
           ('name', 'n:good', nm('good', 'good'))], 'empty'),
    'f': ('tuple', [('migrantsMoved', 'i', Z), ('settlementsFounded', 'i', Z), ('controlLost', 'i', Z),
                    ('tradeUnits', 'i', Z), ('tradeFlowCount', 'i', Z),
                    ('unattributedGrainTransfer', 'b', FALSE)]),
    'o': ('table', ['index', 'turn', 'actor', 'kind', 'targetId', 'settlement', 'sector', 'amount'],
          [('index', 'i', None), ('turn', 'i', None), ('actor', 'i', None), ('kind', 'e:kind', None),
           ('targetId', 'i', None), ('amount', 'd', None), ('settlement', 'i', MINUS1),
           ('sector', 'i', MINUS1)], 'empty'),
    'y': ('table', ['settlement', 'shares'],
          [('shares', 'da', SECTORS_DEFAULT), ('settlement', 'i', ('idx',))], 'empty'),
    'c': ('tuple', [('populationDelta', 'i', Z), ('populationExplained', 'i', R('T.causes.populationDelta')),
                    ('grainDelta', 'i', Z), ('grainExplained', 'i', R('T.causes.grainDelta'))]),
}

ATTR_ROW = (['settlement', 'value'], [('value', 'd', None), ('settlement', 'i', ('idx',))])

ORDER_ROW_KEYS = TURN_SECTIONS['o'][1]
ORDER_FIELDS = TURN_SECTIONS['o'][2]

SETTLEMENT_SECTIONS = {
    '#': ('tuple', [('settlement', 'i', None), ('foundedTurn', 'i', Z), ('controller', 'i', None),
                    ('founded', 'b', FALSE)]),
    'P': ('tuple', [('opening', 'i', None), ('closing', 'i', R('S.population.opening')),
                    ('children', 'i', None), ('adults', 'i', None), ('elders', 'i', None),
                    ('births', 'i', Z), ('deaths', 'i', Z), ('inflow', 'i', Z), ('outflow', 'i', Z),
                    ('colonistsDeparted', 'i', Z), ('notables', 'i', Z),
                    ('colonistsDepartedIdentity', 'c', ('k', S(C1)))]),
    'Pc': ('table', ['class', 'name', 'count'],
           [('count', 'i', Z), ('class', 'i', ('pos', [N('1'), N('2'), N('3')])),
            ('name', 'n:class', nm('class', 'class'))], 'empty'),
    'F': ('tuple', [('grainOpening', 'i', None), ('grainClosing', 'i', R('S.food.grainOpening')),
                    ('harvest', 'i', Z), ('eaten', 'i', Z), ('storeLosses', 'i', Z),
                    ('demandUnits', 'i', Z), ('deficitRatio', 'd', Z),
                    ('foodObtained', 'i', R('S.food.demandUnits')), ('foodProduced', 'i', Z),
                    ('foodBalance', 'i', Z), ('storeLossesIdentity', 'c', ('k', S(C2)))]),
    'Fg': ('table', ['good', 'name', 'produced', 'demand', 'eaten'],
           [('produced', 'i', ('rowd', {0: R('S.food.harvest')}, Z)),
            ('demand', 'i', ('rowd', {0: R('S.food.eaten')}, Z)),
            ('eaten', 'i', R('R.demand')),
            ('good', 'i', ('pos', [N('1'), N('2'), N('3')])),
            ('name', 'n:good', nm('good', 'good'))], 'empty'),
    'H': ('tuple', [('dwellingsOpening', 'i', None), ('dwellingsClosing', 'i', R('S.housing.dwellingsOpening')),
                    ('capacity', 'd', None), ('need', 'd', None), ('sufficiency', 'd', ONE),
                    ('lastMaintenanceFraction', 'd', ONE), ('lastLaborUsed', 'd', Z),
                    ('hasRow', 'b', TRUE), ('builtDecayedSplit', 'c', ('k', S(C3)))]),
    'E': ('tuple', [('foodSurplusRatio', 'd', None), ('artisanShare', 'd', None),
                    ('tradeVolume', 'd', Z), ('sectorShares', 'da', SECTORS_DEFAULT),
                    ('sectorRowPresent', 'b', FALSE)]),
    'Eg': ('table', ['good', 'name', 'stock', 'produced', 'inputDemand', 'consumptionDemand', 'eaten', 'price'],
           [('stock', 'i', ('rowd', {0: R('S.food.grainClosing')}, Z)),
            ('produced', 'i', ('rowd', {0: R('S.food.harvest'),
                                        1: R('S.food.foodGoods.1.produced'),
                                        2: R('S.food.foodGoods.2.produced'),
                                        8: R('S.economy.goods.3.produced'),
                                        9: R('S.economy.goods.3.produced')}, Z)),
            ('inputDemand', 'i', ('rowd', {10: R('S.economy.goods.3.inputDemand')}, Z)),
            ('consumptionDemand', 'i', ('rowd', {0: R('S.food.eaten'),
                                                 1: R('S.food.foodGoods.1.demand'),
                                                 2: R('S.food.foodGoods.2.demand')}, Z)),
            ('eaten', 'i', ('rowd', {0: R('S.food.eaten'),
                                     1: R('S.food.foodGoods.1.eaten'),
                                     2: R('S.food.foodGoods.2.eaten'),
                                     13: R('R.produced')}, Z)),
            ('price', 'd', ('pos', [N(x) for x in ('1', '0.05', '0.05', '0.05', '0.05', '0.05',
                                                    '0.05', '0.05', '20', '0.05', '20', '1', '20', '20')])),
            ('good', 'i', ('pos', [N(str(g)) for g in range(1, 15)])),
            ('name', 'n:good', nm('good', 'good'))], 'empty'),
    'Ei': ('table', ['other', 'good', 'name', 'quantity'],
           [('other', 'i', None), ('good', 'i', None), ('quantity', 'i', Z),
            ('name', 'n:good', nm('good', 'good'))], 'empty'),
    'Eo': ('table', ['other', 'good', 'name', 'quantity'],
           [('other', 'i', None), ('good', 'i', None), ('quantity', 'i', Z),
            ('name', 'n:good', nm('good', 'good'))], 'empty'),
    'Ea': ('table', ['class', 'name', 'active'],
           [('active', 'i', ('pos', [N('1'), N('1'), N('0')])),
            ('class', 'i', ('pos', [N('1'), N('2'), N('3')])),
            ('name', 'n:class', nm('class', 'class'))], 'empty'),
    'S': ('tuple', [('happiness', 'd', ('k', N('100'))),
                    ('happinessFactors', 'da', ('elems', [ONE, R('S.housing.sufficiency')]))]),
    'Sg': ('table', ['class', 'name', 'value'],
           [('value', 'd', Z), ('class', 'i', ('pos', [N('1'), N('2'), N('3')])),
            ('name', 'n:class', nm('class', 'class'))], 'empty'),
    'Sn': ('table', ['class', 'need', 'name', 'value'],
           [('value', 'd', ('pos', [N(x) for x in ('0', '1', '0', '1', '0', '1', '0', '1')])),
            ('class', 'i', ('pos', [N(x) for x in ('1', '1', '1', '1', '2', '2', '2', '2')])),
            ('need', 'i', ('pos', [N(x) for x in ('1', '2', '6', '7', '1', '2', '6', '7')])),
            ('name', 'n:need', nm('need', 'need'))], 'empty'),
    'St': ('tuple', [('controlStrength', 'd', None), ('nominalRate', 'd', Z), ('effectiveRate', 'd', Z),
                     ('scale', 'd', ONE), ('controller', 'i', R('S.controller')),
                     ('policyRowPresent', 'b', FALSE)]),
    'M': ('tuple', [('prevGrainHarvest', 'i', Z), ('pushDeficitRatio', 'd', Z),
                    ('pullAttractiveness', 'd', ('own_attr',)),
                    ('prevGrainStock', 'i', R('S.food.grainOpening')),
                    ('unplacedDeparture', 'd', Z), ('unplacedRemainder', 'd', Z),
                    ('pairwiseFlows', 'c', ('k', S(C4)))]),
    'Ma': ('table', ATTR_ROW[0], ATTR_ROW[1], 'lineA'),
    'X': ('tuple', [('harvestWeatherApplied', 'd', ONE), ('foodLimit', 'd', None),
                    ('vacancy', 'd', None), ('headroom', 'd', R('S.foodState.vacancy')),
                    ('surplusRatio', 'd', R('S.economy.foodSurplusRatio')),
                    ('nominalDeficit', 'd', Z), ('effectiveDeficit', 'd', Z),
                    ('state', 'e:state', ('k', S('Normal'))),
                    ('famineReason', 'e:famine', ('k', S('None'))),
                    ('abandoned', 'b', FALSE), ('prevRowsPresent', 'b', TRUE),
                    ('harvestWeatherRowPresent', 'b', TRUE),
                    ('disasterRowPresent', 'b', FALSE), ('disasterKind', 'i', Z),
                    ('disasterSeverity', 'd', Z), ('disasterMultiplierThisStep', 'd', ONE),
                    ('disasterAppliedMultiplier', 'd', ONE), ('disasterRemainingYears', 'd', Z),
                    ('disasterPendingRowPresent', 'b', FALSE), ('disasterPendingKind', 'i', Z),
                    ('disasterPendingSeverity', 'd', Z), ('disasterPendingMultiplier', 'd', ONE),
                    ('disasterPendingRemainingYears', 'd', Z)]),
    'Y': ('tuple', [('exitOpenness', 'd', None), ('gapOut', 'd', Z), ('gapOutflowCap', 'd', None),
                    ('gapIn', 'd', Z), ('gapInflowCap', 'd', None), ('vacancyCap', 'd', None),
                    ('desiredInflowAe', 'd', Z),
                    ('flightFractionPrime', 'd', Z), ('flightBound', 'd', Z), ('flightOut', 'd', Z),
                    ('flightIn', 'd', Z), ('srcScale', 'd', ONE), ('destScale', 'd', ONE),
                    ('vacancyScale', 'd', ONE), ('planRecorded', 'b', TRUE),
                    ('dtYears', 'd', R('T.dtYears')), ('inflowAeByChannel', 'c', ('k', S(C5)))]),
    'L': ('tuple', [('declaredWeights', 'da', SECTORS_DEFAULT), ('declaredRowPresent', 'b', FALSE),
                    ('effectiveShares', 'da', ('r', 'S.economy.sectorShares'))]),
    'O': ('table', ORDER_ROW_KEYS, ORDER_FIELDS, 'myorders'),
}

# ---------------------------------------------------------------- JSON templates
# (key, sectionCode) for a leaf; (key, [template]) for an object;
# (key, '?', [template]) for an OPTIONAL object (presence is meaningful);
# (key, '@', tableCode) for an array (table).
TURN_TEMPLATE = [
    ('turn', 't'), ('year', 't'), ('dtYears', 't'),
    ('stocks', '@', 'k'),
    ('population', [(k, 'p') for k in ('opening', 'births', 'naturalDeaths', 'starvation',
                                       'closing', 'reconciles', 'discrepancy')]),
    ('grain', [(k, 'g') for k in ('opening', 'endowment', 'harvest', 'eaten', 'spoilage',
                                  'overflow', 'closing', 'reconciles', 'discrepancy')]),
    ('dwellings', [(k, 'w') for k in ('opening', 'built', 'decayed', 'closing', 'reconciles',
                                      'discrepancy')]),
    ('goods', '@', 'q'),
    ('flows', [(k, 'f') for k in ('migrantsMoved', 'settlementsFounded', 'controlLost', 'tradeUnits',
                                  'tradeFlowCount', 'unattributedGrainTransfer')]),
    ('orders', '@', 'o'),
    ('policy', '@', 'y'),
    ('causes', [(k, 'c') for k in ('populationDelta', 'populationExplained', 'grainDelta',
                                   'grainExplained')]),
]

SETTLEMENT_TEMPLATE = [
    ('settlement', '#'), ('foundedTurn', '#'), ('controller', '#'), ('founded', '#'),
    ('population', [(k, 'P') for k in ('opening', 'closing', 'children', 'adults', 'elders', 'notables',
                                       'births', 'deaths', 'inflow', 'outflow', 'colonistsDeparted',
                                       'colonistsDepartedIdentity')] + [('classCounts', '@', 'Pc')]),
    ('food', [(k, 'F') for k in ('grainOpening', 'grainClosing', 'harvest', 'eaten', 'storeLosses',
                                 'storeLossesIdentity', 'demandUnits', 'deficitRatio')]
     + [('foodGoods', '@', 'Fg')]
     + [(k, 'F') for k in ('foodObtained', 'foodProduced', 'foodBalance')]),
    ('housing', [(k, 'H') for k in ('hasRow', 'dwellingsOpening', 'dwellingsClosing', 'capacity', 'need',
                                    'sufficiency', 'lastMaintenanceFraction', 'lastLaborUsed',
                                    'builtDecayedSplit')]),
    ('economy', [('goods', '@', 'Eg'), ('tradeIn', '@', 'Ei'), ('tradeOut', '@', 'Eo')]
     + [(k, 'E') for k in ('sectorShares', 'sectorRowPresent', 'foodSurplusRatio', 'artisanShare',
                           'tradeVolume')]
     + [('classActive', '@', 'Ea')]),
    ('social', [('happiness', 'S'), ('happinessFactors', 'S'), ('grievance', '@', 'Sg'),
                ('needSatisfaction', '@', 'Sn'),
                ('tax', '?', 'St', [(k, 'St') for k in ('controller', 'policyRowPresent', 'nominalRate',
                                                       'controlStrength', 'effectiveRate', 'scale')])]),
    ('migration', [('pushDeficitRatio', 'M'), ('pullAttractiveness', 'M'), ('allAttractiveness', '@', 'Ma')]
     + [(k, 'M') for k in ('prevGrainStock', 'prevGrainHarvest', 'unplacedDeparture', 'unplacedRemainder',
                           'pairwiseFlows')]),
    ('foodState', '?', 'X', [(k, 'X') for k in (
        'prevRowsPresent', 'state', 'famineReason', 'nominalDeficit', 'effectiveDeficit', 'abandoned',
        'disasterRowPresent', 'disasterKind', 'disasterSeverity', 'disasterMultiplierThisStep',
        'disasterAppliedMultiplier', 'disasterRemainingYears', 'disasterPendingRowPresent',
        'disasterPendingKind', 'disasterPendingSeverity', 'disasterPendingMultiplier',
        'disasterPendingRemainingYears', 'harvestWeatherRowPresent', 'harvestWeatherApplied',
        'foodLimit', 'vacancy', 'surplusRatio', 'headroom')]),
    ('migrationPlan', '?', 'Y', [(k, 'Y') for k in (
        'planRecorded', 'dtYears', 'exitOpenness', 'flightFractionPrime', 'flightBound', 'flightOut',
        'gapOut', 'gapOutflowCap', 'srcScale', 'flightIn', 'gapIn', 'gapInflowCap', 'destScale',
        'vacancyCap', 'desiredInflowAe', 'vacancyScale', 'inflowAeByChannel')]),
    ('policy', [(k, 'L') for k in ('declaredWeights', 'declaredRowPresent', 'effectiveShares')]),
    ('orders', '@', 'O'),
]

# section order inside a segment of the cell
TURN_SECTION_ORDER = ['t', 'k', 'p', 'g', 'w', 'q', 'f', 'o', 'y', 'c', 'A']
SETTLEMENT_SECTION_ORDER = ['#', 'P', 'Pc', 'F', 'Fg', 'H', 'E', 'Eg', 'Ei', 'Eo', 'Ea', 'S', 'Sg', 'Sn',
                            'St', 'M', 'Ma', 'X', 'Y', 'L', 'O']


# ================================================================ codebook objects
# One codebook per telemetry schema (README §5.4; D-066 A15). Everything above is codebook v4
# (telemetry/v2, v3 and v4 lines), unchanged, so make_codebook.py still writes
# codebook-telemetry-v4.tsv byte for byte. Codebook v5 (telemetry/v5) is derived from it below.

class Codebook:
    def __init__(self, **kw):
        self.__dict__.update(kw)


def _names(reasons, goods):
    quantities = dict([(3, 'population'), (4, 'dwellings')] + [(100 + g, n) for g, n in goods.items()])
    return quantities, {
        'reason': (reasons, 'reason-%d'),
        'quantity': (quantities, None),
        'good': (goods, None),
        'class': (CLASSES, 'class-%d'),
        'need': (NEEDS, 'need-%d'),
    }


V4 = Codebook(
    version='v4', SCHEMA_CODES=SCHEMA_CODES, REASONS=REASONS, GOODS=GOODS, QUANTITIES=QUANTITIES,
    CLASSES=CLASSES, NEEDS=NEEDS, NAME_MAPS=NAME_MAPS, ENUMS=ENUMS, NONFINITE=NONFINITE,
    CONSTANTS=(C1, C2, C3, C4, C5), TURN_SECTIONS=TURN_SECTIONS, ATTR_ROW=ATTR_ROW,
    SETTLEMENT_SECTIONS=SETTLEMENT_SECTIONS, TURN_TEMPLATE=TURN_TEMPLATE,
    SETTLEMENT_TEMPLATE=SETTLEMENT_TEMPLATE, TURN_SECTION_ORDER=TURN_SECTION_ORDER,
    SETTLEMENT_SECTION_ORDER=SETTLEMENT_SECTION_ORDER)

# ---------------------------------------------------------------- codebook v5
# telemetry/v5 (D-066 A15) = the line shape of the M7 pass, which the writer emitted under the
# unchanged telemetry/v4 tag:
#   - turn.grain.military (ADR-055) and turn.grain.fodder (ADR-056 EC-2): OPTIONAL keys, written
#     only when non-zero, between "overflow" and "closing";
#   - flow reasons 17-21 have names (ReasonNames.Of); 22 and 23 still print "reason-22" /
#     "reason-23", which the fallback reproduces;
#   - 23 goods (15-23: iron-ore ... explosives), so 23 economy goods rows per settlement and 22
#     turn goods rows (2-23), and 25 stock rows (3, 4, 101-123);
#   - order kinds 14-18 (Explore, Prospect, RecruitFormation, ReequipFormation, DisbandFormation);
#   - constant C2 (food.storeLossesIdentity) names the military grain (ADR-055).
# Every other section, field, position and default is codebook v4's.
#
# New default spec:
#   ('absent',)   the key is NOT in the line (an OPTIONAL key); an EMPTY field means "not written".
# New template entry:
#   (key, 'opt', sectionCode)   an OPTIONAL leaf: the key may be missing from the JSON object.

ABSENT = ('absent',)

REASONS_V5 = dict(REASONS)
REASONS_V5.update({17: 'prospecting', 18: 'expedition', 19: 'recruitment', 20: 'formationUpkeep',
                   21: 'reequipment'})
GOODS_V5 = dict(GOODS)
GOODS_V5.update({15: 'iron-ore', 16: 'coal', 17: 'iron', 18: 'steel', 19: 'horses', 20: 'saltpetre',
                 21: 'sulphur', 22: 'gunpowder', 23: 'explosives'})
QUANTITIES_V5, NAME_MAPS_V5 = _names(REASONS_V5, GOODS_V5)
ENUMS_V5 = {k: dict(v) for k, v in ENUMS.items()}
ENUMS_V5['kind'].update({'Explore': 'EX', 'Prospect': 'PR', 'RecruitFormation': 'RF',
                         'ReequipFormation': 'RE', 'DisbandFormation': 'DF'})


def _with_field(fields, key, new_default):
    return [(k, t, new_default if k == key else d) for k, t, d in fields]


TURN_SECTIONS_V5 = dict(TURN_SECTIONS)
_k = TURN_SECTIONS['k']
TURN_SECTIONS_V5['k'] = (_k[0], _k[1], _with_field(_k[2], 'quantity',
                                                    ('pos', [N(str(q)) for q in sorted(QUANTITIES_V5)])), _k[3])
TURN_SECTIONS_V5['g'] = ('tuple', TURN_SECTIONS['g'][1] + [('military', 'i', ABSENT), ('fodder', 'i', ABSENT)])
_q = TURN_SECTIONS['q']
TURN_SECTIONS_V5['q'] = (_q[0], _q[1], _with_field(_q[2], 'good', ('pos', [N(str(g)) for g in range(2, 24)])),
                         _q[3])

# C2 (food.storeLossesIdentity) gained the military grain clause in the M7 pass (ADR-055; Observer.cs).
C2_V5 = ("GrainOpening + Harvest - Eaten - GrainClosing; absorbs spoilage + granary overflow "
         "(unsplit per settlement) AND any appropriation transfer AND colony provisions AND "
         "intra-polity grain relief (D-053 s2: grain a settlement sent to a hungry sibling of "
         "its own polity counts here at the donor; grain it received and ate counts here "
         "NEGATIVE) AND military grain (ADR-055: formation upkeep, recruitment and re-equipment "
         "grain sunk from its stores); sum over all records == ledger spoilage + overflow + "
         "military on every turn; negative means grain ARRIVED by an unrecorded transfer "
         "(provisions on a founding record; otherwise a raid, or relief from its own polity, "
         "which leaves the recipient's granary empty)")

SETTLEMENT_SECTIONS_V5 = dict(SETTLEMENT_SECTIONS)
SETTLEMENT_SECTIONS_V5['F'] = ('tuple', _with_field(SETTLEMENT_SECTIONS['F'][1], 'storeLossesIdentity',
                                                    ('k', S(C2_V5))))
_eg = SETTLEMENT_SECTIONS['Eg']
_eg_price = [d for k, t, d in _eg[2] if k == 'price'][0]
_eg_fields = _with_field(_eg[2], 'price', ('pos', _eg_price[1] + [N('1')] * 9))
_eg_fields = _with_field(_eg_fields, 'good', ('pos', [N(str(g)) for g in range(1, 24)]))
SETTLEMENT_SECTIONS_V5['Eg'] = (_eg[0], _eg[1], _eg_fields, _eg[3])


def _grain_v5(template):
    out = []
    for e in template:
        if e[0] == 'grain':
            keys = [k for k, _ in e[1]]
            at = keys.index('overflow') + 1
            out.append(('grain', e[1][:at] + [('military', 'opt', 'g'), ('fodder', 'opt', 'g')] + e[1][at:]))
        else:
            out.append(e)
    return out


V5 = Codebook(
    version='v5', SCHEMA_CODES={'telemetry/v5': 'v5'}, REASONS=REASONS_V5, GOODS=GOODS_V5,
    QUANTITIES=QUANTITIES_V5, CLASSES=CLASSES, NEEDS=NEEDS, NAME_MAPS=NAME_MAPS_V5, ENUMS=ENUMS_V5,
    NONFINITE=NONFINITE, CONSTANTS=(C1, C2_V5, C3, C4, C5), TURN_SECTIONS=TURN_SECTIONS_V5, ATTR_ROW=ATTR_ROW,
    SETTLEMENT_SECTIONS=SETTLEMENT_SECTIONS_V5, TURN_TEMPLATE=_grain_v5(TURN_TEMPLATE),
    SETTLEMENT_TEMPLATE=SETTLEMENT_TEMPLATE, TURN_SECTION_ORDER=TURN_SECTION_ORDER,
    SETTLEMENT_SECTION_ORDER=SETTLEMENT_SECTION_ORDER)

CODEBOOKS = {'v4': V4, 'v5': V5}


def codebook_for_schema(tag):
    """The codebook of a telemetry schema tag, or None (README §5.4: one codebook per schema)."""
    for cb in (V5, V4):
        if tag in cb.SCHEMA_CODES:
            return cb
    return None


def codebook_for_head(head):
    """The codebook of a cell's first token (v2, v3, v4 -> v4; v5 -> v5), or None."""
    for cb in (V5, V4):
        if head in cb.SCHEMA_CODES.values():
            return cb
    return None

# ---------------------------------------------------------------- continuation cells (D-066 A17)
# A workbook cell holds at most 32,767 characters. A coded turn longer than that is split at a
# '|' (a segment boundary; '|' never occurs inside a segment) into further rows. A continuation
# row has an EMPTY column A and its cell starts with the reserved marker below, followed by the
# '|' that was cut. A reader joins it by dropping the marker and appending the rest to the cell
# above. No cell begins with '>' otherwise (a coded cell begins with its schema code).
CELL_LIMIT = 32767
CONTINUATION_MARK = '>'

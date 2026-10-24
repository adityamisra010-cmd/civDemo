# Tranche 2 — effect coverage (D-063 R1)

- Nodes: **1005**. With a realized operand effect: **979**. Live unlock only: 21. Order gate only: 2 (taxation, ore_prospecting). Repeatables with level effects: 13.
- Nodes with no real effect: **0**.
- A realized effect is read by its owning system (ADR-047); the per-operand sum is clamped to its cap. Where the authored sum exceeded 90 % of the cap, every delta on that operand was scaled down proportionally so no node is made inert by the clamp (the saturation column).

| Operand | Direction | Cap Σ | Nodes | Σ deltas | Σ / cap | Saturation scale | Read by |
|---|---|---:|---:|---:|---:|---:|---|
| `farming.yieldPerArableKm2PerYear` | Higher | 3 | 45 | 2.230 | 74 % |  | ProductionSystem |
| `farming.outputPerFarmerPerYear` | Higher | 3 | 26 | 1.340 | 45 % |  | ProductionSystem |
| `production.outputPerHerderPerYear` | Higher | 2 | 20 | 0.790 | 40 % |  | ProductionSystem |
| `production.outputPerExtractorPerYear` | Higher | 3 | 42 | 1.430 | 48 % |  | ProductionSystem |
| `production.toolYieldBonusMax` | Higher | 1 | 17 | 0.620 | 62 % |  | ProductionSystem |
| `production.toolWearPerEquippedFarmerPerYear` | Lower | 0.8 | 17 | 0.640 | 80 % |  | ProductionSystem |
| `consumption.grainSpoilagePerYear` | Lower | 0.85 | 12 | 0.510 | 60 % |  | ConsumptionSystem |
| `consumption.granaryYearsOfDemand` | Higher | 1 | 9 | 0.420 | 42 % |  | ConsumptionSystem |
| `harvestVariance.sigmaLogYield` | Lower | 0.6 | 24 | 0.562 | 94 % | 0.7397 | HarvestWeatherSystem |
| `granaries.famineReductionMax` | Higher | 1 | 8 | 0.280 | 28 % |  | GranaryEffects |
| `granaries.farmYieldBonusMax` | Higher | 1 | 2 | 0.070 | 7 % |  | GranaryEffects |
| `foodState.adaptationAbsorbableShortfall` | Higher | 1 | 33 | 0.900 | 90 % | 0.9677 | FoodState |
| `housing.buildLaborAdultYearsPerDwelling` | Lower | 0.8 | 7 | 0.340 | 42 % |  | HousingSystem |
| `housing.buildTimberPerDwelling` | Lower | 0.7 | 7 | 0.260 | 37 % |  | HousingSystem |
| `housing.upkeepTimberPerDwellingYear` | Lower | 0.8 | 4 | 0.130 | 16 % |  | HousingSystem |
| `housing.tauYears` | Higher | 1.5 | 17 | 0.560 | 37 % |  | HousingSystem |
| `housing.personsPerDwelling` | Higher | 0.5 | 10 | 0.390 | 78 % |  | HousingSystem and readers |
| `pathBuild.laborPerAdultPerYear` | Higher | 1 | 9 | 0.270 | 27 % |  | PathBuildSystem |
| `pathBuild.buildCostMultiplier` | Lower | 0.7 | 9 | 0.300 | 43 % |  | PathBuildSystem |
| `pathBuild.dirtPathSpeedFactor` | Higher | 0.8 | 0 | 0.000 | 0 % |  | PathBuildSystem |
| `transport.riverCostFactor` | Lower | 0.8 | 7 | 0.360 | 45 % |  | transport readers |
| `roads.baselineKmPerDay` | Higher | 3 | 17 | 0.590 | 20 % |  | TransportQuery |
| `roads.maxRouteKm` | Higher | 3 | 6 | 0.290 | 10 % |  | RoadDevelopmentQuery |
| `catchment.hinterlandRadiusKm` | Higher | 1 | 20 | 0.610 | 61 % |  | CatchmentSystem |
| `trade.gapClosingFraction` | Higher | 1.5 | 53 | 1.349 | 90 % | 0.8182 | TradeArbitrageSystem |
| `trade.costPerBulkCostUnit` | Lower | 0.8 | 41 | 0.719 | 90 % | 0.5581 | TradeArbitrageSystem |
| `price.stockReleaseRatePerYear` | Higher | 1 | 24 | 0.710 | 71 % |  | Price / Trade |
| `demographics.mortalityPerYear` | Lower | 0.7 | 49 | 0.636 | 91 % | 0.5727 | DemographicsSystem |
| `demographics.starvationMortalityMaxPerYear` | Lower | 0.6 | 22 | 0.540 | 90 % | 0.8308 | DemographicsSystem |
| `demographics.starvationChildMultiplier` | Lower | 0.5 | 19 | 0.450 | 90 % | 0.7759 | DemographicsSystem |
| `institutions.universities.maxMortalityReduction` | Higher | 2 | 30 | 0.730 | 36 % |  | Institutions |
| `institutions.universities.maxResearchCostReduction` | Higher | 0.5 | 27 | 0.451 | 90 % | 0.7627 | Institutions |
| `institutions.universities.maturityTauYears` | Lower | 0.5 | 18 | 0.450 | 90 % | 0.9783 | Institutions |
| `governance.authorityDecayCostUnits` | Higher | 3 | 74 | 2.490 | 83 % |  | Governance |
| `governance.taxExtractionResponseMax` | Higher | 1.5 | 44 | 1.349 | 90 % | 0.9375 | Governance |
| `migration.baseRatePerYear` | Higher | 1 | 21 | 0.610 | 61 % |  | MigrationSystem |
| `migration.dampingDecayCostUnits` | Higher | 2 | 23 | 0.730 | 36 % |  | MigrationSystem |
| `mobility.promoteRatePerYear` | Higher | 1.5 | 49 | 1.349 | 90 % | 0.9574 | ClassMobilitySystem |
| `mobility.targetShareCap` | Higher | 1 | 34 | 0.900 | 90 % | 0.9677 | ClassMobilitySystem |
| `colonization.orderPartyFraction` | Higher | 0.5 | 11 | 0.350 | 70 % |  | FoundingQuery |
| `fog.exploreStepNodes` | Higher | 1 | 17 | 0.810 | 81 % |  | DiscoveryQuery |
| `fog.exploreMaxRadiusNodes` | Higher | 1 | 21 | 0.900 | 90 % | 0.9474 | DiscoveryQuery |
| `fog.formationSightNodes` | Higher | 2 | 15 | 1.520 | 76 % |  | DiscoveryQuery |
| `research.civicsPointsCoefficient` | Higher | 0.3 | 20 | 0.271 | 90 % | 0.6279 | ResearchQuery (CP) |
| `research.researchPointsCoefficient` | Higher | 0.3 | 29 | 0.277 | 92 % | 0.3649 | ResearchQuery (RP) |
| `society.grievanceRelief` | Higher | 0.6 | 63 | 0.539 | 90 % | 0.3506 | NeedsGrievanceSystem |
| `society.unrestOutputDrag` | Lower | 0.6 | 54 | 0.553 | 92 % | 0.4154 | Governance / Unrest |

## Repeatables (level effects)

| Node | Tree | Cost growth | Level effects (operand, gap fraction) |
|---|---|---:|---|
| frontier_launch | technology | 1.2 | `trade.costPerBulkCostUnit` 0.1 |
| frontier_propulsion | technology | 1.2 | `fog.exploreMaxRadiusNodes` 0.1 |
| frontier_materials | technology | 1.2 | `housing.buildTimberPerDwelling` 0.1 |
| frontier_energy | technology | 1.2 | `production.outputPerExtractorPerYear` 0.1 |
| frontier_crops | technology | 1.2 | `farming.yieldPerArableKm2PerYear` 0.1 |
| frontier_medicine | technology | 1.2 | `demographics.mortalityPerYear` 0.1 |
| frontier_computation | technology | 1.2 | `research.researchPointsCoefficient` 0.1 |
| frontier_military | technology | 1.2 | `fog.formationSightNodes` 0.1 |
| frontier_cyber | technology | 1.2 | `society.unrestOutputDrag` 0.1 |
| frontier_climate | technology | 1.2 | `harvestVariance.sigmaLogYield` 0.1 |
| institutional_reform | civics | 1.2 | `governance.authorityDecayCostUnits` 0.1 |
| social_policy_research | civics | 1.2 | `society.grievanceRelief` 0.1 |
| civic_participation | civics | 1.2 | `research.civicsPointsCoefficient` 0.1 |

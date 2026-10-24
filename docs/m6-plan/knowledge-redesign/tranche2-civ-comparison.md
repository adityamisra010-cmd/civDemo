# Tranche 2 — Civilization VI / VII comparison

Every node carries a relation to its nearest Civilization VI (base game) and Civilization VII node: **direct** (same concept and role), **partial** (overlapping concept), **different** (same name, different role), **none-by-design** (deliberately absent there — finer-grained history or simulation-specific), **not-reviewed**. Analogs come from `data/civ6-civics-base.json`, `data/civ7-civics.json` and the lane audits.

## Civilization VI

| Tree | direct | partial | different | none-by-design | not-reviewed |
|---|---:|---:|---:|---:|---:|
| technology | 48 | 179 | 12 | 283 | 3 |
| civics | 28 | 113 | 6 | 330 | 3 |

## Civilization VII

| Tree | direct | partial | different | none-by-design | not-reviewed |
|---|---:|---:|---:|---:|---:|
| technology | 23 | 159 | 13 | 330 | 0 |
| civics | 27 | 176 | 7 | 266 | 4 |

## Direct and partial analogs

| Node | Tree | Age | Civ VI | Civ VII |
|---|---|---|---|---|
| bow_simple | technology | A1 | direct: Archery | partial: Bronze Working |
| sling | technology | A1 | none-by-design | partial: Animal Husbandry |
| raft | technology | A1 | partial: Sailing | partial: Sailing |
| dugout | technology | A1 | partial: Sailing | partial: Sailing |
| fishing_hook | technology | A1 | partial: Celestial Navigation (Fish harvest) | partial: Sailing (fishing boat food) |
| dog_domestication | technology | A1 | partial: Animal Husbandry | partial: Animal Husbandry |
| cereal_cultivation | technology | A2 | none-by-design | direct: Agriculture |
| cereal_domesticated | technology | A2 | partial: Pottery (Wheat harvest) | partial: Agriculture |
| rice_wet | technology | A2 | partial: Pottery (Rice harvest) | partial: Agriculture |
| millet | technology | A2 | none-by-design | partial: Agriculture |
| maize | technology | A2 | none-by-design | partial: Agriculture |
| sorghum_pearl_millet | technology | A2 | none-by-design | partial: Agriculture |
| orchard | technology | A2 | partial: Irrigation (Bananas harvest, plantations) | partial: Pottery (plantation food) |
| sheep_goat | technology | A2 | direct: Animal Husbandry (Sheep) | partial: Animal Husbandry (pasture food) |
| cattle | technology | A2 | direct: Animal Husbandry (Cattle) | partial: Animal Husbandry (pasture food) |
| pig | technology | A2 | none-by-design | partial: Animal Husbandry |
| wool | technology | A2 | partial: Animal Husbandry | partial: Animal Husbandry |
| pottery_open_fired | technology | A2 | direct: Pottery | direct: Pottery |
| kiln_updraft | technology | A2 | partial: Pottery | partial: Guilds |
| potters_wheel_fast | technology | A2 | partial: Pottery | partial: Pottery |
| mudbrick | technology | A2 | partial: Masonry (Ancient Walls) | partial: Pottery (Brickyard) |
| stone_dry | technology | A2 | partial: Masonry (Ancient Walls) | partial: Masonry (Ancient Walls) |
| copper_smelting | technology | A2 | partial: Mining | partial: Bronze Working |
| gold_silver_native | technology | A2 | partial: Currency | partial: Currency |
| irrigation_basin | technology | A2 | partial: Irrigation | partial: Irrigation |
| irrigation_canal | technology | A2 | partial: Irrigation | partial: Irrigation |
| terrace | technology | A2 | none-by-design | partial: Mit'a (Inca civic: Terrace Farm) |
| wheel_solid | technology | A2 | direct: The Wheel | direct: Wheel |
| cart | technology | A2 | partial: The Wheel | partial: Wheel |
| plough_ard | technology | A2 | none-by-design | partial: Agriculture |
| calendar_lunar | technology | A2 | partial: Astrology | none-by-design |
| arsenical_bronze | technology | A3 | partial: Bronze Working | partial: Bronze Working |
| tin_bronze | technology | A3 | direct: Bronze Working | direct: Bronze Working |
| mining_shaft | technology | A3 | partial: Mining | partial: Masonry |
| proto_writing | technology | A3 | partial: Writing | partial: Writing |
| cuneiform | technology | A3 | direct: Writing | direct: Writing |
| hieroglyphic | technology | A3 | direct: Writing | direct: Writing |
| chinese_script | technology | A3 | direct: Writing | direct: Writing |
| papyrus | technology | A3 | partial: Writing | partial: Writing |
| numeral_sexagesimal | technology | A3 | partial: Mathematics | partial: Mathematics |
| arithmetic_babylonian | technology | A3 | partial: Mathematics | partial: Mathematics |
| sail_square | technology | A3 | direct: Sailing | direct: Sailing |
| plank_boat | technology | A3 | partial: Shipbuilding | partial: Sailing |
| horse_domestication | technology | A3 | partial: Horseback Riding | different: Horses (resource, not tech-gated) |
| wheel_spoked | technology | A3 | partial: The Wheel | partial: Wheel |
| chariot | technology | A3 | direct: The Wheel | direct: The Wheel |
| composite_bow | technology | A3 | partial: Horseback Riding | none-by-design |
| fired_brick | technology | A3 | partial: Masonry | partial: Pottery |
| standard_weights | technology | A3 | partial: Currency | partial: Currency |
| iron_bloomery | technology | A4 | direct: Iron Working | direct: Iron Working |
| abjad | technology | A4 | partial: Writing | partial: Literacy |
| alphabet_vowels | technology | A4 | partial: Drama Poetry | partial: Literacy |
| hacksilver | technology | A3 | partial: Currency | partial: Currency |
| coinage_electrum | technology | A4 | direct: Currency | direct: Currency |
| cavalry | technology | A4 | direct: Horseback Riding | partial: Iron Working |
| siege_ram | technology | A4 | partial: Masonry | none-by-design |
| siege_tower | technology | A4 | partial: Construction | none-by-design |
| stone_fortification | technology | A4 | partial: Masonry (Ancient Walls) | partial: Masonry (Ancient Walls) |
| crossbow | technology | A4 | partial: Machinery | partial: Castles |
| naval_ram | technology | A4 | partial: Sailing | partial: Sailing |
| qanat | technology | A4 | partial: Engineering (Aqueduct) | none-by-design |
| aqueduct_tunnel | technology | A4 | partial: Engineering (Aqueduct) | none-by-design |
| aqueduct_channel | technology | A4 | partial: Engineering (Aqueduct) | none-by-design |
| arch | technology | A3 | partial: Engineering | partial: Engineering |
| astronomy_records | technology | A4 | partial: Astrology | none-by-design |
| geometry_practical | technology | A3 | partial: Mathematics | partial: Mathematics |
| medicine_hippocratic | technology | A4 | none-by-design | partial: Urban Planning |
| rotary_quern | technology | A4 | partial: The Wheel (Water Mill) | partial: Machinery (Gristmill) |
| geometry_axiomatic | technology | A5 | partial: Mathematics | partial: Mathematics |
| mechanics_archimedean | technology | A5 | partial: Engineering | partial: Engineering |
| torsion_artillery | technology | A5 | partial: Engineering | partial: The Wheel |
| water_mill | technology | A5 | partial: The Wheel | partial: Machinery |
| gearing | technology | A5 | partial: Machinery | partial: Machinery |
| vault_dome | technology | A5 | partial: Construction | partial: Currency (Bath) |
| road_paved | technology | A5 | partial: Military Engineering | none-by-design |
| bridge_stone | technology | A5 | none-by-design | direct: Engineering (Ancient Bridge) |
| aqueduct_arcade | technology | A5 | direct: Engineering (Aqueduct) | none-by-design |
| cast_iron | technology | A5 | partial: Metal Casting | partial: Metal Casting |
| crossbow_repeating | technology | A5 | partial: Machinery | none-by-design |
| stirrup | technology | A5 | direct: Stirrups | partial: Heraldry |
| astronomy_geometric | technology | A5 | partial: Astronomy | partial: Astronomy |
| cartography | technology | A5 | partial: Cartography | partial: Cartography |
| latitude_sailing | technology | A4 | partial: Celestial Navigation | partial: Navigation |
| mortise_hull | technology | A3 | partial: Shipbuilding | none-by-design |
| heavy_plough | technology | A6 | none-by-design | partial: Feudalism (farm food) |
| sugar_refining | technology | A6 | partial: Irrigation (Sugar plantation) | different: Sugar (Exploration resource) |
| fulling_mill | technology | A6 | partial: Apprenticeship (Workshop) | partial: Machinery |
| portolan | technology | A6 | partial: Cartography | partial: Cartography |
| astrolabe | technology | A6 | partial: Celestial Navigation | partial: Astronomy |
| gunpowder | technology | A6 | direct: Gunpowder | direct: Gunpowder |
| saltpetre_refining | technology | A6 | partial: Gunpowder | none-by-design |
| cannon_early | technology | A6 | partial: Gunpowder | partial: Gunpowder |
| trebuchet | technology | A6 | direct: Military Engineering | direct: Metallurgy |
| plate_armour | technology | A6 | partial: Apprenticeship | partial: Heraldry |
| woodblock_print | technology | A6 | partial: Printing | none-by-design |
| movable_type_ceramic | technology | A6 | partial: Printing | none-by-design |
| movable_type_metal | technology | A6 | partial: Printing | none-by-design |
| paper_money | technology | A6 | partial: Banking | none-by-design |
| hindu_arabic | technology | A6 | partial: Mathematics | partial: Mathematics |
| algebra | technology | A6 | partial: Education | partial: Education |
| crank_connecting_rod | technology | A6 | partial: Machinery | partial: Machinery (Sawmill) |
| blast_furnace_water | technology | A6 | partial: Metal Casting | partial: Metal Casting |
| double_entry | technology | A6 | partial: Banking | none-by-design |
| bill_of_exchange | technology | A6 | partial: Banking | partial: Economics |
| printing_press | technology | A7 | direct: Printing | none-by-design |
| type_founding | technology | A7 | partial: Printing | none-by-design |
| caravel | technology | A7 | direct: Cartography | partial: Shipbuilding |
| carrack | technology | A7 | partial: Square Rigging | direct: Shipbuilding |
| polynesian_canoe | technology | A6 | none-by-design | partial: Navigation |
| celestial_navigation | technology | A7 | partial: Celestial Navigation | partial: Astronomy |
| mercator | technology | A7 | partial: Cartography | partial: Cartography |
| corned_powder | technology | A7 | partial: Gunpowder | partial: Gunpowder |
| cannon_cast_bronze | technology | A7 | direct: Metal Casting | partial: Metal Casting |
| matchlock | technology | A7 | direct: Gunpowder | direct: Gunpowder |
| flintlock | technology | A7 | partial: Military Science | none-by-design |
| trace_italienne | technology | A7 | direct: Siege Tactics | partial: Castles |
| ship_of_line | technology | A7 | partial: Square Rigging | partial: Gunpowder |
| heliocentrism | technology | A7 | partial: Astronomy | partial: Education |
| telescope | technology | A7 | partial: Astronomy | partial: Astronomy |
| mechanics_newtonian | technology | A7 | partial: Scientific Theory | none-by-design |
| steam_atmospheric | technology | A7 | partial: Steam Power | partial: Steam Engine |
| coke | technology | A7 | partial: Industrialization | partial: Steam Engine |
| steam_separate_condenser | technology | A8 | partial: Steam Power | partial: Steam Engine |
| cylinder_boring | technology | A8 | partial: Steam Power | partial: Steam Engine |
| steam_rotary | technology | A8 | partial: Steam Power | partial: Steam Engine |
| steam_high_pressure | technology | A8 | partial: Steam Power | partial: Steam Engine |
| puddling | technology | A8 | partial: Industrialization | partial: Industrialization |
| bessemer | technology | A8 | direct: Steel | partial: Industrialization |
| basic_process | technology | A8 | partial: Steel | none-by-design |
| slide_rest_lathe | technology | A8 | partial: Industrialization | partial: Mass Production |
| interchangeable_parts | technology | A8 | direct: Replaceable Parts | partial: Mass Production |
| milling_machine | technology | A8 | partial: Mass Production | partial: Mass Production |
| water_frame | technology | A8 | partial: Industrialization (Factory) | partial: Mass Production (Factory) |
| power_loom | technology | A8 | partial: Industrialization (Factory) | partial: Mass Production (Factory) |
| locomotive | technology | A8 | partial: Steam Power | partial: Steam Engine |
| railway | technology | A8 | partial: Steam Power | partial: Industrialization |
| steamboat | technology | A8 | partial: Steam Power | partial: Steam Engine |
| screw_propeller | technology | A8 | partial: Steam Power | partial: Steam Engine |
| iron_hull | technology | A8 | partial: Steam Power | partial: Steam Engine |
| chemistry_quantitative | technology | A8 | partial: Chemistry | different: Electricity |
| reinforced_concrete | technology | A8 | partial: Steel | partial: Urbanization (Modern Bridge) |
| electromagnetism | technology | A8 | partial: Electricity | partial: Electricity |
| dynamo | technology | A8 | partial: Electricity | partial: Electricity |
| electric_light | technology | A8 | partial: Electricity | partial: Electricity |
| electricity_generation | technology | A8 | direct: Electricity | partial: Electricity |
| electric_motor | technology | A8 | partial: Electricity | partial: Electricity |
| telephone | technology | A8 | partial: Telecommunications | none-by-design |
| internal_combustion | technology | A8 | direct: Combustion | partial: Combustion |
| germ_theory | technology | A8 | partial: Sanitation | none-by-design |
| aircraft | technology | A9 | direct: Flight | direct: Flight |
| aircraft_metal | technology | A9 | partial: Advanced Flight | partial: Aerodynamics |
| jet_engine | technology | A9 | partial: Advanced Flight | partial: Aerodynamics |
| tank | technology | A9 | direct: Combustion | partial: Combustion |
| diesel | technology | A9 | partial: Combustion | partial: Combustion |
| aluminium | technology | A9 | partial: Radio | none-by-design |
| rocket | technology | A9 | direct: Rocketry | direct: Rocketry |
| satellite | technology | A9 | direct: Satellites | partial: Rocketry |
| radio | technology | A8 | direct: Radio | direct: Radio |
| vacuum_tube | technology | A9 | partial: Radio | partial: Radio |
| television | technology | A9 | partial: Mass Media | none-by-design |
| computer | technology | A9 | direct: Computers | partial: Computation |
| transistor | technology | A9 | partial: Computers | none-by-design |
| integrated_circuit | technology | A9 | partial: Computers | none-by-design |
| microprocessor | technology | A9 | partial: Computers | none-by-design |
| internet | technology | A9 | partial: Telecommunications | none-by-design |
| nuclear_fission | technology | A9 | direct: Nuclear Fission | direct: Nuclear Fission |
| nuclear_power | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| thermonuclear | technology | A9 | partial: Nuclear Fusion | partial: Nuclear Fission |
| canning | technology | A8 | none-by-design | partial: Mass Production (Cannery) |
| plastics | technology | A9 | direct: Plastics | none-by-design |
| skyscraper | technology | A8 | partial: Steel | partial: Urbanization |
| numerical_control | technology | A9 | direct: Robotics | partial: Computation |
| mobile_phone | technology | A9 | partial: Telecommunications | none-by-design |
| laser | technology | A9 | direct: Lasers | none-by-design |
| fibre_optics | technology | A9 | partial: Telecommunications | none-by-design |
| cmos_vlsi | technology | A9 | partial: Computers | none-by-design |
| composites | technology | A9 | direct: Composites | none-by-design |
| nuclear_fusion_research | technology | A9 | partial: Nuclear Fusion | none-by-design |
| personal_computer | technology | A9 | partial: Computers | none-by-design |
| smartphone | technology | A9 | partial: Telecommunications | none-by-design |
| cellular_digital | technology | A9 | partial: Telecommunications | none-by-design |
| crewed_spaceflight | technology | A9 | not-reviewed | partial: Rocketry |
| space_probe | technology | A9 | partial: Satellites | none-by-design |
| space_telescope | technology | A9 | partial: Satellites | none-by-design |
| earth_observation | technology | A9 | partial: Satellites | partial: Rocketry |
| jet_airliner | technology | A9 | partial: Advanced Flight | partial: Aerodynamics |
| helicopter | technology | A9 | partial: Synthetic Materials | none-by-design |
| icbm | technology | A9 | partial: Rocketry | partial: Rocketry |
| nuclear_submarine | technology | A9 | direct: Telecommunications | partial: Mobilization |
| guided_munitions | technology | A9 | partial: Guidance Systems | none-by-design |
| stealth | technology | A9 | direct: Stealth Technology | none-by-design |
| missile_defence | technology | A9 | partial: Guidance Systems | none-by-design |
| nanotechnology | technology | A9 | direct: Nanotechnology | none-by-design |
| fusion_power | technology | A9 | direct: Nuclear Fusion | none-by-design |
| fusion_propulsion | technology | A9 | partial: Nuclear Fusion | none-by-design |
| frontier_launch | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_propulsion | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_materials | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_energy | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_medicine | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_computation | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_military | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_cyber | technology | A9 | partial: Future Tech | partial: Future Tech |
| frontier_climate | technology | A9 | partial: Future Tech | partial: Future Tech |
| trade | technology | A3 | partial: Foreign Trade | partial: Code of Laws |
| cotton_cultivation | technology | A2 | partial: Irrigation (Cotton plantation) | different: Cotton (resource, not tech-gated) |
| ashlar_masonry | technology | A3 | direct: Masonry | direct: Masonry |
| mesoamerican_script | technology | A3 | direct: Writing | direct: Writing |
| ore_prospecting | technology | A3 | partial: Bronze Working | none-by-design |
| counting_rods | technology | A5 | partial: Mathematics | partial: Mathematics |
| traction_trebuchet | technology | A5 | partial: Engineering | none-by-design |
| ocean_junk | technology | A6 | none-by-design | partial: Shipbuilding |
| rifling | technology | A7 | direct: Rifling | none-by-design |
| breech_loading_rifle | technology | A8 | partial: Rifling | none-by-design |
| machine_gun | technology | A8 | direct: Advanced Ballistics | none-by-design |
| rifled_artillery | technology | A8 | partial: Ballistics | partial: Industrialization |
| ac_transmission | technology | A8 | partial: Electricity | partial: Electricity |
| balloon | technology | A8 | partial: Flight | none-by-design |
| gas_engine | technology | A8 | partial: Combustion | partial: Combustion |
| punched_card_tabulation | technology | A8 | none-by-design | partial: Computation |
| sanitary_engineering | technology | A8 | direct: Sanitation | different: Urban Planning (Hospital) |
| structural_iron | technology | A8 | partial: Industrialization | partial: Steam Engine (Ironworks) |
| farm_machinery | technology | A8 | none-by-design | partial: Steam Engine (farm food) |
| fission_weapon | technology | A9 | direct: Nuclear Fission | direct: Nuclear Fission |
| guided_missile | technology | A9 | partial: Guidance Systems | none-by-design |
| submarine | technology | A9 | partial: Electricity | partial: Mobilization |
| communications_satellite | technology | A9 | partial: Satellites | none-by-design |
| fast_breeder_reactor | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| graphite_reactor | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| heavy_water_reactor | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| light_water_reactor | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| orbital_launch_vehicle | technology | A9 | partial: Rocketry | partial: Rocketry |
| programmable_computer | technology | A9 | partial: Computers | partial: Computation |
| radio_broadcasting | technology | A9 | partial: Radio | partial: Radio |
| reprocessing | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| uranium_enrichment | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| uranium_milling | technology | A9 | partial: Nuclear Fission | partial: Nuclear Fission |
| grain_storage | technology | A2 | partial: Pottery (Granary) | partial: Agriculture (Granary) |
| camel_caravan | technology | A4 | different: Foreign Trade | partial: Currency |
| codex | technology | A5 | none-by-design | partial: Writing |
| insula | technology | A5 | none-by-design | partial: Radio (Tenement) |
| herring_buss | technology | A6 | partial: Celestial Navigation (Fish harvest) | partial: Cartography (fishing boat food) |
| three_field_rotation | technology | A6 | partial: Feudalism (civic; farm adjacency) | partial: Feudalism (farm food) |
| coal_fuel | technology | A6 | partial: Industrialization | none-by-design |
| sustained_yield_forestry | technology | A7 | partial: Conservation | none-by-design |
| steam_printing_press | technology | A8 | partial: Mass Media | none-by-design |
| precast_housing | technology | A9 | none-by-design | partial: Radio (Tenement) |
| aerial_reconnaissance | technology | A9 | partial: Flight | partial: Flight |
| law_code | civics | A3 | direct: Code Of Laws | direct: Code of Laws |
| legal_code_roman | civics | A4 | partial: Code Of Laws | partial: Ritsuryo (Heian Japan unique civic) |
| coined_wage | civics | A4 | partial: Currency (technology) | partial: Skilled Trades (Coinage policy) |
| joint_stock | civics | A6 | none-by-design | partial: Chartered Companies (Great Britain unique civic) |
| kin_council | civics | A1 | none-by-design | partial: Chiefdom |
| hereditary_rank | civics | A2 | none-by-design | partial: Seonggol (Silla unique civic: bone-rank system) |
| organized_priesthood | civics | A2 | partial: Astrology (technology: Holy Site district) | direct: Mysticism (Priesthood tradition, Altar) |
| chiefdom | civics | A2 | direct: Code Of Laws (unlocks the Chiefdom government) | direct: Chiefdom |
| tribal_confederacy | civics | A2 | none-by-design | partial: Seven Tribes (Bulgaria unique civic) |
| corvee_labour | civics | A3 | partial: State Workforce | partial: Mit'a (Inca unique civic) |
| elite_council | civics | A3 | none-by-design | partial: Senatus Populusque Romanus (Rome unique civic) |
| royal_envoys | civics | A3 | partial: Diplomatic Service | partial: Diplomatic Service |
| sacral_kingship | civics | A3 | none-by-design | partial: Light of Amun-Ra (Egypt unique civic) |
| service_tenure | civics | A3 | partial: Feudalism | partial: Authority (Vassalage policy) |
| sworn_treaties | civics | A3 | partial: Diplomatic Service | not-reviewed |
| city_state_oligarchy | civics | A3 | partial: Political Philosophy (unlocks the Oligarchy government) | direct: Oligarchy (government) |
| territorial_kingship | civics | A3 | partial: Divine Right (unlocks the Monarchy government) | partial: Despotism (government) |
| citizen_assembly | civics | A4 | none-by-design | partial: Ekklesia (Greece unique civic) |
| elected_magistracies | civics | A4 | none-by-design | partial: Classical Republic (government) |
| land_registry | civics | A4 | none-by-design | partial: Domesday Book (Norman unique civic) |
| provincial_administration | civics | A4 | partial: Early Empire | partial: Satrapies (Achaemenid Persia unique civic) |
| standing_army | civics | A4 | partial: Military Training | partial: Organized Military |
| theocracy | civics | A4 | direct: Reformed Church (unlocks the Theocracy government) | direct: Theocracy (government) |
| craft_guilds | civics | A5 | direct: Guilds | partial: Rakuichi-Rakuza (Sengoku Japan unique civic; Kabunakama tradition) |
| municipal_charter | civics | A5 | none-by-design | partial: Nagaram (Chola unique civic) |
| assembly_democracy | civics | A5 | partial: Suffrage (unlocks the Democracy government) | partial: Ekklesia (Greece unique civic) |
| bureaucratic_empire | civics | A5 | partial: Civil Service | partial: Bureaucracy |
| classical_republic | civics | A5 | direct: Political Philosophy (unlocks the Classical Republic government) | direct: Classical Republic (government) |
| civil_service_examinations | civics | A6 | partial: Civil Service | partial: Bureaucracy |
| commercial_law | civics | A6 | none-by-design | partial: Economics (Maritime Law policy) |
| feudal_monarchy | civics | A6 | partial: Feudalism | direct: Feudal Monarchy (government) |
| merchant_republic | civics | A6 | direct: Exploration (unlocks the Merchant Republic government) | partial: Plutocracy (government) |
| military_rule | civics | A6 | none-by-design | partial: Authoritarianism (government) |
| law_of_nations | civics | A7 | partial: Diplomatic Service | partial: Sovereignty |
| public_health_board | civics | A7 | partial: Sanitation (technology) | none-by-design |
| resident_embassies | civics | A7 | partial: Diplomatic Service | partial: Globalism |
| written_constitution | civics | A7 | none-by-design | partial: Constitution (policy, Bureaucracy) |
| absolute_monarchy | civics | A7 | partial: Divine Right (unlocks the Monarchy government) | partial: Samoderzhaviye (Russia unique civic: autocracy) |
| constitutional_monarchy | civics | A7 | none-by-design | direct: Constitutional Monarchy (government) |
| federal_republic | civics | A7 | none-by-design | partial: Elective Republic (government) |
| presidential_republic | civics | A7 | partial: Suffrage (unlocks the Democracy government) | partial: Elective Republic (government) |
| abolition | civics | A8 | none-by-design | partial: Serfdom (Russia unique civic; Krestyanskaya Reforma tradition) |
| general_staff | civics | A8 | partial: Military Science (technology) | partial: Military Science (Modern technology) |
| mass_political_party | civics | A8 | partial: Ideology | none-by-design |
| mass_suffrage | civics | A8 | partial: Suffrage | partial: Suffrage (policy, Democracy) |
| universal_conscription | civics | A8 | partial: Mobilization | partial: Organized Military |
| parliamentary_government | civics | A8 | partial: Suffrage (unlocks the Democracy government) | partial: Elective Republic (government) |
| central_planning | civics | A9 | partial: Class Struggle | partial: Centralism (Collectivization policy) |
| marxist_leninist_state | civics | A9 | direct: Class Struggle (unlocks the Communism government) | direct: Communism |
| one_party_state | civics | A9 | partial: Totalitarianism | partial: Authoritarianism (government) |
| gift_exchange | civics | A1 | partial: Foreign Trade | partial: Cah-nah-ha (Mississippian unique civic; Gift Economy tradition) |
| oral_tradition | civics | A1 | partial: Drama Poetry | different: Chiefdom |
| shamanic_ritual | civics | A1 | partial: Mysticism | partial: Mysticism |
| war_band | civics | A1 | partial: Military Tradition | partial: Discipline |
| ancestor_veneration | civics | A2 | none-by-design | partial: Mysticism |
| communal_feasts | civics | A2 | partial: Games Recreation | partial: Public Life |
| court_patronage | civics | A3 | none-by-design | partial: Society (Patronage policy) |
| customs_tolls | civics | A3 | partial: Foreign Trade | partial: Tariffs (policy, Imperialism) |
| dynastic_marriage | civics | A3 | none-by-design | partial: Diplomatic Service (Heqin policy) |
| family_law | civics | A3 | partial: Code Of Laws | partial: Code of Laws |
| planned_cities | civics | A3 | partial: Urbanization | partial: Urban Grid (Heian Japan unique civic) |
| royal_archives | civics | A3 | partial: Recorded History | partial: Literacy |
| scribal_schools | civics | A3 | partial: Recorded History | partial: Literacy |
| temple_economy | civics | A3 | none-by-design | partial: Mysticism |
| trade_treaties | civics | A3 | partial: Foreign Trade | partial: Code of Laws (Improve Trade Relations action) |
| citizen_militia | civics | A4 | partial: Military Training | partial: Citizenship |
| daughter_colonies | civics | A4 | partial: Early Empire | different: Colonialism |
| mandate_doctrine | civics | A4 | none-by-design | partial: Ren / Yi / Li (Han unique civics) |
| public_games | civics | A4 | direct: Games Recreation | direct: Entertainment |
| royal_post | civics | A4 | none-by-design | partial: Satrapies (Čāpār-Kāna tradition, Achaemenid Persia unique civic) |
| scriptural_canon | civics | A4 | partial: Theology | partial: Piety |
| state_navy | civics | A4 | partial: Naval Tradition | partial: Organized Military |
| building_regulation | civics | A5 | partial: Civil Engineering | none-by-design |
| jury_courts | civics | A5 | none-by-design | partial: Citizenship |
| mercenary_contracts | civics | A5 | direct: Mercenaries | partial: Tactics (Drills policy) |
| missionary_religion | civics | A5 | partial: Theology | partial: Theology (Evangelism tradition) |
| monasticism | civics | A5 | partial: Theology | partial: Reformation (Religious Orders tradition) |
| philosophical_schools | civics | A5 | partial: Political Philosophy | direct: Philosophy |
| political_philosophy | civics | A5 | direct: Political Philosophy | partial: Political Theory |
| public_granaries | civics | A5 | none-by-design | partial: Public Life (Annona policy) |
| public_theatre | civics | A5 | direct: Drama Poetry | partial: Citizenship (Drama and Poetry policy) |
| state_academy | civics | A5 | partial: Recorded History | partial: Zhi (Xiu Taixue tradition, Han unique civic) |
| state_gazette | civics | A5 | none-by-design | partial: Gorkhapatra (Nepal unique civic) |
| state_monopolies | civics | A5 | none-by-design | partial: Centralism |
| state_religion | civics | A5 | partial: Theology | partial: Piety |
| statecraft_doctrine | civics | A5 | none-by-design | direct: Mantriparishad (Arthashastra tradition, Maurya unique civic) |
| tributary_relations | civics | A5 | none-by-design | partial: Diplomatic Service (Heqin policy) |
| steppe_empire | civics | A5 | none-by-design | partial: Ulus (Mongolia unique civic) |
| canon_law | civics | A6 | none-by-design | partial: Theology |
| common_law | civics | A6 | none-by-design | direct: Common Law (Norman unique civic) |
| forest_law | civics | A6 | partial: Conservation | partial: Angevin Empire (England unique civic; Charter of the Forest tradition) |
| legal_estates | civics | A6 | none-by-design | partial: Social Class |
| manorialism | civics | A6 | partial: Feudalism | partial: Serfdom (Russia unique civic) |
| market_fairs | civics | A6 | direct: Medieval Faires | partial: Economics |
| military_orders | civics | A6 | none-by-design | partial: Reformation (Religious Orders policy) |
| systematic_theology | civics | A6 | partial: Theology | partial: Theology |
| arts_academies | civics | A7 | none-by-design | partial: Inspiration (Renaissance policy) |
| colonial_administration | civics | A7 | partial: Colonialism | partial: Colonialism |
| divine_right | civics | A7 | direct: Divine Right | direct: Sovereignty (Divine Right tradition) |
| enlightenment | civics | A7 | direct: The Enlightenment | partial: Social Class (Enlightenment policy) |
| humanism | civics | A7 | direct: Humanism | partial: Inspiration (Renaissance policy) |
| mercantilism | civics | A7 | direct: Mercantilism | direct: Mercantilism |
| military_academies | civics | A7 | partial: Military Training | partial: Militarism (Materiel policy) |
| poor_law | civics | A7 | none-by-design | partial: Social Question |
| public_museums | civics | A7 | direct: Natural History | direct: Natural History |
| public_opera | civics | A7 | direct: Opera Ballet | none-by-design |
| religious_reform | civics | A7 | direct: Reformed Church | direct: Reformation |
| scientific_societies | civics | A7 | partial: The Enlightenment | partial: Society of Antiquaries (Great Britain unique civic) |
| social_contract | civics | A7 | partial: The Enlightenment | partial: Political Theory |
| state_exploration | civics | A7 | direct: Exploration | partial: Cartography (Exploration technology) |
| stock_exchange | civics | A7 | partial: Economics (technology; Stock Exchange building) | partial: Capitalism |
| westphalian_sovereignty | civics | A7 | partial: Diplomatic Service | partial: Sovereignty |
| antitrust | civics | A8 | none-by-design | partial: Progressivism |
| civil_code | civics | A8 | none-by-design | direct: Code Civil des Français (French Empire unique civic) |
| classical_liberalism | civics | A8 | partial: Capitalism | direct: Liberalism |
| cooperatives | civics | A8 | none-by-design | partial: Social Question |
| customs_union | civics | A8 | none-by-design | direct: Zollverein (Prussia unique civic) |
| factory_inspection | civics | A8 | none-by-design | partial: Progressivism |
| free_trade | civics | A8 | partial: Capitalism | partial: Capitalism (Laissez-Faire policy) |
| great_power_congress | civics | A8 | none-by-design | partial: Hegemony |
| heritage_preservation | civics | A8 | direct: Cultural Heritage | partial: Natural History (Land Heritage policy) |
| indirect_rule | civics | A8 | none-by-design | partial: Imperialism (Indenture policy) |
| international_organizations | civics | A8 | partial: Globalization | partial: Globalism |
| labour_unions | civics | A8 | partial: Class Struggle | partial: Social Question |
| limited_liability | civics | A8 | none-by-design | partial: Capitalism |
| mass_press | civics | A8 | partial: Mass Media | none-by-design |
| merit_civil_service | civics | A8 | direct: Civil Service | partial: Bureaucracy |
| nationalism | civics | A8 | direct: Nationalism | direct: Nationalism |
| nature_conservation | civics | A8 | direct: Conservation | partial: Natural History (Land Heritage policy) |
| popular_sovereignty | civics | A8 | partial: Suffrage | partial: Democracy |
| press_freedom | civics | A8 | none-by-design | direct: Liberalism (Free Press policy) |
| professional_sport | civics | A8 | direct: Professional Sports | none-by-design |
| referendum | civics | A8 | none-by-design | partial: Democracy (ideology) |
| research_university | civics | A8 | partial: Education (technology) | partial: Academics (modern technology) |
| secret_ballot | civics | A8 | partial: Suffrage | partial: Democracy (ideology) |
| socialism | civics | A8 | partial: Class Struggle | partial: Socialism |
| womens_suffrage | civics | A8 | partial: Suffrage | partial: Suffrage (policy, Democracy) |
| revolutionary_republic | civics | A8 | none-by-design | direct: Revolutionary Republic (government) |
| arms_control | civics | A9 | partial: Cold War | none-by-design |
| civil_rights_law | civics | A9 | none-by-design | partial: Progressivism |
| climate_accords | civics | A9 | partial: Environmentalism (Gathering Storm civic; outside the base-game data file) | partial: Globalism |
| collective_security | civics | A9 | none-by-design | partial: Hegemony |
| environmental_regulation | civics | A9 | partial: Environmentalism (Gathering Storm civic; outside the base-game data file) | none-by-design |
| fascism | civics | A9 | partial: Totalitarianism | direct: Fascism |
| integrated_alliance_command | civics | A9 | partial: Rapid Deployment | none-by-design |
| multilateral_trade_rules | civics | A9 | partial: Globalization | partial: Globalism |
| nuclear_deterrence | civics | A9 | partial: Nuclear Program | none-by-design |
| public_broadcasting | civics | A9 | partial: Mass Media | partial: Democracy (Fireside Chats policy) |
| science_policy | civics | A9 | none-by-design | partial: Centralism (Naukograd policy) |
| state_propaganda | civics | A9 | partial: Mass Media | direct: Radicalism (Propaganda policy) |
| total_mobilization | civics | A9 | direct: Mobilization | partial: Militarism (Materiel policy) |
| zoning | civics | A9 | partial: Urbanization | partial: Urbanization (Modern technology) |
| fascist_state | civics | A9 | direct: Totalitarianism (unlocks the Fascism government) | partial: Fascism |
| prestige_leadership | civics | A1 | none-by-design | partial: Chiefdom (Charismatic Leader tradition) |
| megalithic_monuments | civics | A2 | partial: Astrology (technology: unlocks Stonehenge) | none-by-design |
| barter_exchange | civics | A2 | none-by-design | partial: Commerce (Commodities policy) |
| craft_specialists | civics | A2 | partial: Craftsmanship | partial: Skilled Trades |
| age_sets | civics | A2 | none-by-design | partial: Agoge |
| chiefly_retinue | civics | A2 | partial: Military Tradition | partial: Chiefdom (Charismatic Leader policy) |
| village_defences | civics | A2 | partial: Masonry (technology) | partial: Masonry (Antiquity technology) |
| big_man_polity | civics | A2 | none-by-design | partial: Chiefdom (Charismatic Leader tradition) |
| communal_storage | civics | A2 | none-by-design | partial: Ayllu (Inca unique civic; Qullqa tradition) |
| extended_household | civics | A2 | none-by-design | partial: Ohana |
| epic_literature | civics | A3 | partial: Drama Poetry | partial: Enuma Elish (Babylon unique civic) |
| pantheon_hierarchy | civics | A3 | none-by-design | partial: Mysticism (unlocks pantheons) |
| frontier_fortresses | civics | A3 | different: Defensive Tactics | partial: Nine Garrisons (Ming unique civic) |
| victory_stelae | civics | A3 | none-by-design | partial: Monumentum Adulitanum (Aksum unique civic) |
| overkingship | civics | A3 | none-by-design | partial: Authority (Vassalage tradition) |
| banking_houses | civics | A4 | partial: Banking (technology) | none-by-design |
| letter_schools | civics | A4 | partial: Writing (technology) | partial: Literacy |
| siege_engineers | civics | A4 | partial: Military Engineering (technology) | none-by-design |
| tyranny | civics | A4 | partial: Political Philosophy (unlocks the Autocracy government) | partial: Despotism (government) |
| palace_library | civics | A4 | partial: Recorded History | partial: Literacy |
| household_registers | civics | A5 | none-by-design | partial: Lijia (Ming unique civic) |
| congregational_worship | civics | A5 | none-by-design | partial: Society (Uposatha tradition) |
| hegemonic_leagues | civics | A5 | none-by-design | partial: Symmachia (Greece unique civic) |
| rhetoric_schools | civics | A5 | none-by-design | partial: Code of Laws (Oratory policy) |
| water_commission | civics | A5 | partial: Engineering (technology; Aqueduct district) | none-by-design |
| citizenship_grants | civics | A5 | none-by-design | partial: Citizenship |
| art_of_war | civics | A5 | partial: Military Training | partial: Tactics (Drills policy) |
| public_physicians | civics | A5 | none-by-design | partial: Commerce (Medicine policy) |
| vernacular_literature | civics | A6 | different: Drama Poetry | partial: Mono No Aware (Heian Japan unique civic) |
| mendicant_orders | civics | A6 | none-by-design | partial: Reformation (Religious Orders tradition) |
| cathedral_madrasa_schools | civics | A6 | partial: Education (technology) | partial: Tarnovo Schools (Bulgaria unique civic) |
| marches | civics | A6 | none-by-design | partial: Society (Marcher Lords policy) |
| printed_classics | civics | A6 | partial: Printing (technology) | partial: Tripitaka Koreana (Goryeo unique civic) |
| apprenticeship | civics | A6 | partial: Apprenticeship (technology) | partial: Guilds (Exploration technology) |
| caliphate | civics | A6 | partial: Reformed Church (unlocks the Theocracy government) | partial: Theocracy (government) |
| chartered_universities | civics | A6 | partial: Education (technology) | partial: Education (technology) |
| geographic_compendia | civics | A6 | partial: Cartography (technology) | partial: Cartography (technology) |
| translation_patronage | civics | A6 | none-by-design | partial: Al-Jabr (Abbasid unique civic) |
| provincial_intendants | civics | A7 | none-by-design | partial: Bureaucracy |
| confessionalization | civics | A7 | partial: Reformed Church | partial: Reformation |
| humanist_colleges | civics | A7 | partial: Humanism | none-by-design |
| printed_newspapers | civics | A7 | partial: Printing (technology) | none-by-design |
| military_engineers | civics | A7 | partial: Military Engineering (technology) | partial: Nationalism (Army Corps of Engineers policy) |
| national_literature | civics | A8 | partial: Nationalism | partial: Nationalism |
| technical_academies | civics | A8 | none-by-design | partial: Twelve Gates (Dār al-Fonūn tradition, Qajar unique civic) |
| municipal_waterworks | civics | A8 | partial: Sanitation (technology) | none-by-design |
| assisted_emigration | civics | A8 | none-by-design | partial: Imperialism (Indenture policy) |
| geographical_societies | civics | A8 | partial: Natural History | partial: Natural History (Explorer unit) |
| imperialism_doctrine | civics | A8 | partial: Colonialism | partial: Imperialism |
| progressivism | civics | A8 | none-by-design | direct: Progressivism |
| social_democracy | civics | A8 | partial: Class Struggle | partial: Socialism |
| telegraph_news_agencies | civics | A8 | partial: Mass Media | none-by-design |
| bonapartist_regime | civics | A8 | none-by-design | partial: Revolutionary Authoritarianism (government) |
| cultural_institutes | civics | A9 | none-by-design | partial: Hegemony (Cultural Imperialism policy) |
| demand_management | civics | A9 | none-by-design | partial: Progressivism (New Deal policy) |
| special_economic_zones | civics | A9 | none-by-design | partial: Open Customs (Qing unique civic) |
| public_housing | civics | A9 | none-by-design | partial: Modernity (Living Standards policy) |
| public_works_relief | civics | A9 | none-by-design | partial: Socialism (Public Works policy) |
| liberal_internationalism | civics | A9 | partial: Globalization | partial: Globalism |
| neoliberalism | civics | A9 | partial: Capitalism | partial: Capitalism (Laissez-Faire tradition) |
| populism_modern | civics | A9 | none-by-design | partial: Nationalism (Demagogy tradition) |
| vanguard_party | civics | A9 | partial: Class Struggle | partial: Communism |
| script_reform | civics | A9 | none-by-design | partial: Chữ Nôm (Đại Việt unique civic) |
| social_media_platforms | civics | A9 | direct: Social Media | none-by-design |
| universal_telephone_service | civics | A9 | partial: Telecommunications (technology) | none-by-design |
| personality_cult | civics | A9 | none-by-design | partial: Radicalism (Propaganda tradition) |
| clerical_republic | civics | A9 | partial: Reformed Church (unlocks the Theocracy government) | partial: Theocracy (government) |
| personalist_dictatorship | civics | A9 | none-by-design | partial: Authoritarianism (government) |
| semi_presidential_republic | civics | A9 | partial: Suffrage (unlocks the Democracy government) | partial: Elective Republic (government) |
| supranational_union | civics | A9 | partial: Globalization | partial: Globalism |
| welfare_state | civics | A9 | none-by-design | partial: Liberalism (Welfare State policy) |
| national_laboratories | civics | A9 | partial: Nuclear Program | partial: Centralism (Naukograd policy) |
| think_tanks | civics | A9 | none-by-design | direct: Globalism (Think-tanks policy) |

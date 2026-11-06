using System.Numerics;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.WarEconE;

/// <summary>
/// Lane E (resumed 2026-11-06): ships' companies on lane N's vessel model (hulls x VesselClass.Crew), the sea ration, rowing
/// exertion, seamen's pay, bunker coal, naval powder, scurvy; occupation and blockade trade loss; contributions levied
/// whatever the grain want; lane TR's terrain classes in the logistics tables. Closed forms are independent BigInteger
/// arithmetic; conservation is exact equality of every commodity total.
/// </summary>
public class WarEconNavalTests
{
    private readonly ITestOutputHelper _out;
    public WarEconNavalTests(ITestOutputHelper output) => _out = output;

    private static readonly long Y = SimInstant.MsPerYear;
    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private const long W = 7 * Day;   // the early-modern band's PROPOSED pulse

    private static long Start(int turnInBand, long offsetDays)
    {
        var cal = ShippedCalendar();
        return cal.TurnStart(cal.Bands[3].FirstTurn + turnInBand).Ms + offsetDays * Day;
    }

    private static ArmyComposition Fleet(VesselClass v, long hulls, long embarkedFoot = 0)
        => new(embarkedFoot, 0, 0, 0, 0, 0, 0, 0, false) { Vessel = v, Hulls = hulls };

    private static WarEconSetup NavalSetup(ArmyComposition fleet, NavalCapability naval, WarTech tech = WarTech.Coinage, long coal = 0,
        long position = 1_000_000, bool march = false, Route? route = null, LocalitySetup[]? locs = null, LogisticsRates? rates = null,
        long train = 50_000_000, (long, long)[]? combat = null, long munitions = 0, long treasury = 100_000_000)
    {
        var p0 = Polity(0, tech, 300_000, 4_000_000_000, treasury, munitions: munitions)
                 with { Capability = new PolityCapability(tech) { Naval = naval }, Coal = coal };
        var p1 = Polity(1, WarTech.Coinage, 150_000, 500_000_000, 1_000_000);
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates ?? R, TerrainLogistics.Proposal(), new[] { p0, p1 },
            locs ?? Array.Empty<LocalitySetup>(),
            new[] { new ArmySetup(0, 0, 0, 0, fleet, route ?? new Route(new[] { Seg(5_000, TerrainClass.Water) }), position, march, train, 0, 0,
                                  combat ?? Array.Empty<(long, long)>()) });
    }

    private static EconRig RunPulses(WarEconSetup setup, long start, int pulses, CampaignCalendar? cal = null)
    {
        var rig = Build(setup, new[] { WarOf(0, start, start + pulses * W, 0, 1) }, 2, start, turnCal: cal);
        rig.R.RunUntil(start + pulses * W + Day);
        return rig;
    }

    // ------------------------------------------------------------------------------------------------ 1. lane N's vessel model

    /// <summary>The company is hulls x lane N's VesselClass.Crew; the class is the one lane N's NavalDesign derives from the
    /// polity's naval capability (no date); a composition the capability cannot man is refused.</summary>
    [Fact]
    public void ShipsCompany_IsLaneNHullsTimesCrew_ClassFromCapability_IllegalOnesRefused()
    {
        var galley = NavalDesign.Derive(NavalCapability.Oars, VesselRole.Warship);
        var line = NavalDesign.Derive(NavalCapability.Sails | NavalCapability.Gunpowder, VesselRole.Warship);
        var transport = NavalDesign.Derive(NavalCapability.Sails, VesselRole.Transport);
        Assert.Same(DomainProfiles.Galley, galley);
        Assert.Same(DomainProfiles.ShipOfTheLine, line);
        Assert.Equal(10 * DomainProfiles.Galley.Crew, Fleet(galley, 10).Seamen);
        Assert.Equal(10 * DomainProfiles.Galley.Crew, Fleet(galley, 10).Men);
        Assert.Equal(4 * transport.Crew + 4 * transport.Capacity, Fleet(transport, 4, 4 * transport.Capacity).Men);

        var oars = new PolityCapability(WarTech.None) { Naval = NavalCapability.Oars };
        var sailsGuns = new PolityCapability(WarTech.Gunpowder) { Naval = NavalCapability.Sails | NavalCapability.Gunpowder };
        Assert.Null(Fleet(galley, 10).Validate(oars));
        Assert.NotNull(Fleet(galley, 10).Validate(sailsGuns));                                 // oared hulls need Oars
        Assert.NotNull(Fleet(line, 3).Validate(new PolityCapability(WarTech.None) { Naval = NavalCapability.Sails })); // guns need Gunpowder
        Assert.Null(Fleet(line, 3).Validate(sailsGuns));
        Assert.NotNull(Fleet(transport, 4, 4 * transport.Capacity + 1).Validate(sailsGuns));    // more foot than the hulls carry
        Assert.NotNull((Fleet(galley, 10) with { Horses = 5 }).Validate(oars));                 // no horses aboard here
        Assert.NotNull(Fleet(galley, 0).Validate(oars));                                        // a fleet needs hulls
    }

    // ------------------------------------------------------------------------------------------------ 2. rations, rowing, stores

    /// <summary>At sea the sea ration replaces the field extra: the standing need of 10 sailing hulls (800 men) over one
    /// 7-day pulse is exactly floor(800 x (base + sea extra) x W / Y), larger than the same men in the field.</summary>
    [Fact]
    public void SeaRation_StandingNeed_IsTheClosedForm()
    {
        long start = Start(0, 10);
        var cog = DomainProfiles.Cog;
        var row = RunPulses(NavalSetup(Fleet(cog, 10), NavalCapability.Sails), start, 1).W.Rows.Single();
        long men = 10 * cog.Crew;
        Assert.Equal(men, row.MenStart);
        Assert.Equal((long)((BigInteger)men * (R.RationBase + R.RationSeaExtra) * W / Y), row.StandingNeed);
        Assert.True(row.StandingNeed > (long)((BigInteger)men * (R.RationBase + R.RationFieldExtra) * W / Y));
        Assert.Equal(0, row.MovementNeed);   // at anchor: no exertion
    }

    /// <summary>Only oared hulls have an exertion term: the movement need of 10 galleys under way for a whole pulse is exactly
    /// floor(rowers x RationRowExtra x W / Y) with rowers = floor(2000 x 850 / 1000); sailing hulls under way have none.
    /// Passage speed is the propulsion's (galley 60 km/day, sail 140 km/day; PROPOSAL).</summary>
    [Fact]
    public void Rowing_ExertionOnlyForOaredHulls_ExactRowersShare()
    {
        long start = Start(0, 10);
        var g = RunPulses(NavalSetup(Fleet(DomainProfiles.Galley, 10), NavalCapability.Oars, position: 0, march: true), start, 1).W.Rows.Single();
        long men = 10 * DomainProfiles.Galley.Crew, rowers = men * R.RowersPermille / 1000;
        Assert.Equal(1700, rowers);
        Assert.Equal((long)((BigInteger)rowers * R.RationRowExtra * W / Y), g.MovementNeed);
        Assert.Equal(7 * R.PassageOarMPerDay, g.DistanceM);
        var s = RunPulses(NavalSetup(Fleet(DomainProfiles.Cog, 10), NavalCapability.Sails, position: 0, march: true), start, 1).W.Rows.Single();
        Assert.Equal(0, s.MovementNeed);
        Assert.Equal(7 * R.PassageSailMPerDay, s.DistanceM);
    }

    /// <summary>Stowage by propulsion: a fleet at its depot with an empty hold asks exactly floor(men x sea ration x days /
    /// 360) to restock — five days for galleys, 120 for sailing ships (PROPOSAL; galleys had to put in to shore).</summary>
    [Fact]
    public void Stowage_GalleysDays_SailingShipsMonths_ExactRestockRequest()
    {
        long start = Start(0, 10);
        long Restock(VesselClass v, NavalCapability n)
        {
            var rig = RunPulses(NavalSetup(Fleet(v, 10), n, position: 0, train: 0), start, 1);
            return rig.W.Allocations.Single().Requests.Single().Restock;
        }
        long Expect(VesselClass v, long days) => 10 * v.Crew * (R.RationBase + R.RationSeaExtra) * days / SimInstant.YearDays;
        Assert.Equal(Expect(DomainProfiles.Galley, R.StoresDaysOar), Restock(DomainProfiles.Galley, NavalCapability.Oars));
        Assert.Equal(Expect(DomainProfiles.Cog, R.StoresDaysSail), Restock(DomainProfiles.Cog, NavalCapability.Sails));
    }

    // ------------------------------------------------------------------------------------------------ 3. coal

    /// <summary>Steam hulls burn coal per km steamed: 5 steam frigates under way for a pulse burn exactly ceil(5 x 60,000 x
    /// 1,896.048 km / 1000 km) kg and make 1,896.048 km (7 days at the class's sustained speed). With half that coal they make
    /// exactly the share of the distance the coal allows; with none they lie at anchor — movement restricted, still fed
    /// (never a starvation).</summary>
    [Fact]
    public void Steam_BurnsCoalPerKmSteamed_HalfCoalHalfDistance_NoCoalNoMove_StillFed()
    {
        long start = Start(0, 10);
        var naval = NavalCapability.SteamPower | NavalCapability.Gunpowder;
        var frigate = NavalDesign.Derive(naval, VesselRole.Warship);
        Assert.Same(DomainProfiles.SteamFrigate, frigate);
        var tech = WarTech.Coinage | WarTech.Gunpowder;
        // Passage speed from lane N's class (fix round 1, E-V7): 5,700 mm/s x 86.4 x 550 / 1000 = 270,864 m/day.
        long dist = 7 * (5_700L * 86_400 / 1000 * R.PassageEngineSustainedPermille / 1000);
        Assert.Equal(1_896_048, dist);
        long coal = (long)(((BigInteger)5 * R.CoalPerHull1000Km * dist + 999_999) / 1_000_000);
        Assert.Equal(568_815, coal);

        var full = RunPulses(NavalSetup(Fleet(frigate, 5), naval, tech, coal: 10_000_000, position: 0, march: true), start, 1);
        var r = full.W.Rows.Single();
        Assert.Equal(coal, r.CoalUsed);
        Assert.Equal(dist, r.DistanceM);
        Assert.Equal(coal, full.W.Ledger.Balance(full.W.CoalBurntAccount));
        Assert.Equal(10_000_000 - coal, full.W.CoalStockOf(0));
        Assert.Equal(0, full.W.BunkerOf(0));

        var half = RunPulses(NavalSetup(Fleet(frigate, 5), naval, tech, coal: coal / 2, position: 0, march: true), start, 1).W.Rows.Single();
        Assert.Equal(coal / 2, half.CoalUsed);
        Assert.Equal((long)((BigInteger)dist * (coal / 2) / coal), half.DistanceM);   // the share the coal allows

        var none = RunPulses(NavalSetup(Fleet(frigate, 5), naval, tech, coal: 0, position: 0, march: true), start, 1);
        var n = none.W.Rows.Single();
        Assert.Equal(0, n.DistanceM);
        Assert.Equal(0, n.CoalUsed);
        Assert.Equal(0, n.HungerPpm);
        Assert.Contains(none.W.Events, e => e.Kind == CascadeKind.MovementRestricted && e.Value == 0);
    }

    /// <summary>Oared and sailing hulls never burn coal, and a polity without SteamPower cannot hold coal ("ancient
    /// infantry must not consume oil", DIR5 §2.11, applied to galleys and coal).</summary>
    [Fact]
    public void OarsAndSail_BurnNoCoal_CoalNeedsSteamPower()
    {
        long start = Start(0, 10);
        foreach (var (v, n) in new[] { (DomainProfiles.Galley, NavalCapability.Oars), (DomainProfiles.Cog, NavalCapability.Sails) })
        {
            var rig = RunPulses(NavalSetup(Fleet(v, 10), n, position: 0, march: true), start, 3);
            Assert.All(rig.W.Rows, r => Assert.Equal(0, r.CoalUsed));
            Assert.Equal(0, rig.W.Ledger.Total(Commodity.Coal));
        }
        Assert.Throws<ArgumentException>(() => Build(NavalSetup(Fleet(DomainProfiles.Cog, 10), NavalCapability.Sails, coal: 1),
            new[] { WarOf(0, start, start + W, 0, 1) }, 2, start));
    }

    // ------------------------------------------------------------------------------------------------ 4. pay and powder

    /// <summary>Seamen are paid: with no desertion or disease (so the men are constant) the pay due over k pulses is exactly
    /// floor(800 x PaySeaman x kW / Y) (per-year integration with a persistent carry); with an empty treasury all of it is
    /// arrears.</summary>
    [Fact]
    public void SeamensPay_AccruesExactly_AndBecomesArrearsWhenUnpaid()
    {
        long start = Start(0, 10);
        var rates = R with
        {
            DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DesertionPerArrearsMonthPermilleYear = 0,
            DesertionHungerPermilleYear = 0, DesertionMutinyPermilleYear = 0, DiseaseBasePermilleYear = 0, ScurvyPermilleYearPerMonth = 0,
        };
        var rig = RunPulses(NavalSetup(Fleet(DomainProfiles.Cog, 10), NavalCapability.Sails, rates: rates, treasury: 0), start, 6);
        long men = 10 * DomainProfiles.Cog.Crew;
        for (int k = 1; k <= 6; k++)
        {
            long due = rig.W.Rows.Take(k).Sum(r => r.PayDue);
            Assert.Equal((long)((BigInteger)men * R.PaySeaman * (k * W) / Y), due);
        }
        Assert.All(rig.W.Rows, r => Assert.Equal(0, r.PayPaid));
        Assert.Equal(rig.W.Rows.Sum(r => r.PayDue), rig.W.ArrearsOf(0));
        Assert.True(rig.W.ArrearsOf(0) > 0);
    }

    /// <summary>Powder and shot for gun-armed hulls on days of action (Gunpowder only): 4 ships of the line in a 2-day action
    /// need exactly 2 x 4 x 5,000 kg; galleys (deck archers only) need none even when the polity holds Gunpowder.</summary>
    [Fact]
    public void NavalPowder_OnlyGunArmedHulls_OnDaysOfAction()
    {
        long start = Start(0, 10);
        var tech = WarTech.Coinage | WarTech.Gunpowder;
        var line = RunPulses(NavalSetup(Fleet(DomainProfiles.ShipOfTheLine, 4), NavalCapability.Sails | NavalCapability.Gunpowder, tech,
            combat: new (long, long)[] { (0, 2) }, munitions: 1_000_000), start, 1).W.Rows.Single();
        Assert.Equal(2 * 4 * R.MunitionsPerArmedHullCombatDay, line.MunitionsNeed);
        Assert.Equal(line.MunitionsNeed, line.MunitionsUsed);
        var galleys = RunPulses(NavalSetup(Fleet(DomainProfiles.Galley, 10), NavalCapability.Oars, tech,
            combat: new (long, long)[] { (0, 2) }, munitions: 1_000_000), start, 1).W.Rows.Single();
        Assert.Equal(0, galleys.MunitionsNeed);
    }

    // ------------------------------------------------------------------------------------------------ 5. scurvy

    /// <summary>The scurvy rate (pure function): zero up to the onset, then 400 ‰/yr per 30 days beyond it (floor), capped at
    /// 4,000; Antiscorbutics multiply it by 50 ‰; Sanitation does not touch it.</summary>
    [Fact]
    public void ScurvyRate_OnsetSlopeCapAndAntiscorbutics()
    {
        var none = new PolityCapability(WarTech.None);
        var anti = new PolityCapability(WarTech.Antiscorbutics);
        var san = new PolityCapability(WarTech.Sanitation);
        long onset = R.ScurvyOnsetDays * Day;
        Assert.Equal(0, NavalLogistics.ScurvyPermilleYear(onset, none, R));
        Assert.Equal(0, NavalLogistics.ScurvyPermilleYear(onset + 1, none, R));          // floor(400 x 1 ms / 30 d) = 0
        Assert.Equal(400, NavalLogistics.ScurvyPermilleYear(onset + 30 * Day, none, R));
        Assert.Equal(93, NavalLogistics.ScurvyPermilleYear(onset + 7 * Day, none, R));   // floor(400 x 7 / 30)
        Assert.Equal(4_000, NavalLogistics.ScurvyPermilleYear(onset + 3_000 * Day, none, R));
        Assert.Equal(20, NavalLogistics.ScurvyPermilleYear(onset + 30 * Day, anti, R));
        Assert.Equal(400, NavalLogistics.ScurvyPermilleYear(onset + 30 * Day, san, R));
    }

    /// <summary>A sailing squadron 1,000 km out on blockade for 30 weekly pulses: the latched scurvy rate of pulse k is
    /// exactly f(k x 7 days); deaths match the antiscorbutic twin until the onset and exceed it after. In its own port the
    /// clock restarts at every pulse start (PortCall) and scurvy never starts; at a foreign port with nothing to take, it does.</summary>
    [Fact]
    public void Scurvy_RisesWithTimeAtSea_AntiscorbuticsCut_PortCallResets()
    {
        long start = Start(0, 10);
        const int pulses = 30;
        var cog = Fleet(DomainProfiles.Cog, 10);
        var sea = RunPulses(NavalSetup(cog, NavalCapability.Sails), start, pulses);
        var lime = RunPulses(NavalSetup(cog, NavalCapability.Sails, WarTech.Coinage | WarTech.Antiscorbutics), start, pulses);
        var cap = new PolityCapability(WarTech.Coinage);
        for (int k = 0; k < pulses; k++)
        {
            long f = NavalLogistics.ScurvyPermilleYear(k * W, cap, R);
            Assert.Equal(f, sea.W.Rows[k].ScurvyPermilleYear);
            Assert.Equal(f * R.ScurvyAntiscorbuticPermille / 1000, lime.W.Rows[k].ScurvyPermilleYear);
            if (k * W <= R.ScurvyOnsetDays * Day) Assert.Equal(lime.W.Rows[k].DiseaseDeaths, sea.W.Rows[k].DiseaseDeaths);
        }
        Assert.Equal(0, sea.W.Rows[8].ScurvyPermilleYear);   // day 56: the onset itself
        Assert.Equal(93, sea.W.Rows[9].ScurvyPermilleYear);  // day 63
        Assert.True(sea.W.Rows.Skip(12).Sum(r => r.DiseaseDeaths) > lime.W.Rows.Skip(12).Sum(r => r.DiseaseDeaths));
        _out.WriteLine(Table(sea.W.Rows, 0, start));

        var home = new Route(new[] { Seg(5_000, TerrainClass.Water, locality: 0) });
        var port = RunPulses(NavalSetup(cog, NavalCapability.Sails, route: home,
            locs: new[] { new LocalitySetup(0, 0, 5_000, 1_000_000, 0) }), start, pulses);
        Assert.All(port.W.Rows, r => Assert.Equal(0, r.ScurvyPermilleYear));
        Assert.Equal(pulses, port.W.Events.Count(e => e.Kind == CascadeKind.PortCall));
        var bare = RunPulses(NavalSetup(cog, NavalCapability.Sails, route: home, train: 0,
            locs: new[] { new LocalitySetup(0, 1, 5_000, 0, 0) }), start, pulses);
        Assert.Equal(0, bare.W.Events.Count(e => e.Kind == CascadeKind.PortCall));
        Assert.True(bare.W.Rows[pulses - 1].ScurvyPermilleYear > 0);
    }

    /// <summary>Crossing invariance with a squadron at sea long enough for scurvy: shipped 720-day turns vs one-year turns give
    /// the identical army-side state although the splits differ (the scurvy rate is latched at pulse starts).</summary>
    [Fact]
    public void CrossingInvariance_SquadronAtSea_WithScurvy()
    {
        long start = Start(0, 300);
        const int pulses = 80;
        var a = RunPulses(NavalSetup(Fleet(DomainProfiles.Cog, 10), NavalCapability.Sails, treasury: 20_000), start, pulses);
        var b = RunPulses(NavalSetup(Fleet(DomainProfiles.Cog, 10), NavalCapability.Sails, treasury: 20_000), start, pulses, OneYearTurns());
        Assert.NotEqual(a.Inner.SubSteps.Count(x => x.EndsAtSplit), b.Inner.SubSteps.Count(x => x.EndsAtSplit));
        Assert.Contains(a.W.Rows, r => r.ScurvyPermilleYear > 0 && r.DiseaseDeaths > 0);
        Assert.Equal(a.W.ArmySideHashHex(), b.W.ArmySideHashHex());
    }

    /// <summary>Every commodity total (coal included) is constant by exact equality at the boundary instant of a pulse that
    /// straddles a turn start, one ms either side, and at the end, with a steam squadron under way.</summary>
    [Fact]
    public void Conservation_ExactAcrossAStraddle_WithASteamSquadron()
    {
        long s1 = Start(1, 0), start = s1 - 3 * Day;
        var naval = NavalCapability.SteamPower | NavalCapability.Gunpowder;
        var setup = NavalSetup(Fleet(DomainProfiles.SteamFrigate, 5), naval, WarTech.Coinage | WarTech.Gunpowder, coal: 3_000_000,
            position: 0, march: true, combat: new (long, long)[] { (0, 1) }, munitions: 100_000);
        var rig = Build(setup, new[] { WarOf(0, start, start + 12 * W, 0, 1) }, 2, start);
        var t0 = Totals(rig.W.Ledger);
        Assert.Contains(Commodity.Coal, AllCommodities);
        foreach (long at in new[] { start, s1 - 1, s1, s1 + 1, start + W, start + 12 * W + Day })
        {
            rig.R.RunUntil(at);
            Assert.Equal(t0, Totals(rig.W.Ledger));
        }
        Assert.Single(rig.Inner.SubSteps, x => x.EndsAtSplit && x.EndMs == s1);
        Assert.True(rig.W.Rows.Sum(r => r.CoalUsed) > 0);
        Assert.Equal(rig.W.Rows.Sum(r => r.CoalUsed), rig.W.Ledger.Balance(rig.W.CoalBurntAccount));
    }

    // ------------------------------------------------------------------------------------------------ 6. occupation, contributions

    private static WarEconSetup OccupationSetup(bool march, long trade, long share, long blockade = 0, long disbandPermille = 100,
        int armies = 1, long position = 0)
    {
        var foot = new ArmyComposition(1000, 0, 0, 0, 0, 0, 0, 0, false);
        // own ground 100 km (no locality), a foreign market town on 100 km, then 300 km of foreign country (no locality).
        var route = new Route(new[] { Seg(100, TerrainClass.Open), Seg(100, TerrainClass.Open, locality: 0), Seg(300, TerrainClass.Open) });
        var army = new List<ArmySetup>();
        for (int i = 0; i < armies; i++)
            army.Add(new ArmySetup(i, 0, 0, 0, foot, route, position, march, 10_000_000, 0, 0, Array.Empty<(long, long)>()));
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, R with { DisbandStrengthPermille = disbandPermille }, TerrainLogistics.Proposal(),
            new[] { Polity(0, WarTech.Coinage, 100_000, 2_000_000_000, 100_000_000),
                    Polity(1, WarTech.Coinage, 100_000, 2_000_000_000, 0, trade: trade, blockade: blockade) },
            new[] { new LocalitySetup(0, 1, 5_000, 5_000_000, 1_000_000) { TradeSharePermille = share } },
            army.ToArray());
    }

    /// <summary>An infantry army marches through a foreign market town carrying 300 ‰ of polity 1's trade: the owner's revenue
    /// rate drops exactly at the pulse start at which the army stands in the town (start + 7 d) and returns exactly at the
    /// pulse start at which it has left (start + 14 d) — pinned at the instant and one ms either side — and polity 1's
    /// treasury is exactly floor((R0 x 7 d + R1 x 7 d) / (Y x 1000)) at the second instant.</summary>
    [Fact]
    public void Occupation_CutsTheTradeShare_FromThePulseStartItBegins_ToThePulseStartItEnds()
    {
        long start = Start(0, 10), trade = 1_000_000;
        var rig = Build(OccupationSetup(march: true, trade, share: 300), new[] { WarOf(0, start, start + 6 * W, 0, 1) }, 2, start);
        long r0 = trade * 1000, r1 = trade * 700;
        Assert.Equal(r0, rig.W.RevenueRateScaledOf(1));
        rig.R.RunUntil(start + W - 1);
        Assert.Equal(r0, rig.W.RevenueRateScaledOf(1));
        Assert.Equal(-1, rig.W.OccupiedLocalityOf(0));
        rig.R.RunUntil(start + W);
        Assert.Equal(0, rig.W.OccupiedLocalityOf(0));
        Assert.Equal(r1, rig.W.RevenueRateScaledOf(1));
        Assert.Equal((long)((BigInteger)r0 * W / (Y * 1000)), rig.W.TreasuryOf(1));
        rig.R.RunUntil(start + W + 1);
        Assert.Equal(r1, rig.W.RevenueRateScaledOf(1));
        rig.R.RunUntil(start + 2 * W - 1);
        Assert.Equal(r1, rig.W.RevenueRateScaledOf(1));
        rig.R.RunUntil(start + 2 * W);
        Assert.Equal(-1, rig.W.OccupiedLocalityOf(0));
        Assert.Equal(r0, rig.W.RevenueRateScaledOf(1));
        Assert.Equal((long)(((BigInteger)r0 * W + (BigInteger)r1 * W) / (Y * 1000)), rig.W.TreasuryOf(1));
        var occ = rig.W.Events.Where(e => e.Kind is CascadeKind.Occupation or CascadeKind.OccupationEnded).ToArray();
        Assert.Equal(new[] { (start + W, CascadeKind.Occupation), (start + 2 * W, CascadeKind.OccupationEnded) },
                     occ.Select(e => (e.AtMs, e.Kind)).ToArray());
    }

    /// <summary>Two armies in the same town cut its share once; blockade and occupation together cut at most the whole trade
    /// income; an occupying army that disbands at a pulse end gives the share back at exactly that instant (±1 ms pinned).</summary>
    [Fact]
    public void Occupation_CountedOncePerTown_CappedWithBlockade_EndsAtDisbandment()
    {
        long start = Start(0, 10), trade = 1_000_000;
        var two = Build(OccupationSetup(march: false, trade, share: 300, armies: 2, position: 150_000), new[] { WarOf(0, start, start + 3 * W, 0, 1) }, 2, start);
        two.R.RunUntil(start);
        Assert.Equal(trade * 700, two.W.RevenueRateScaledOf(1));

        var capped = Build(OccupationSetup(march: false, trade, share: 300, blockade: 800, position: 150_000), new[] { WarOf(0, start, start + 3 * W, 0, 1) }, 2, start);
        Assert.Equal(trade * 200, capped.W.RevenueRateScaledOf(1));   // blockade alone before the army's first pulse
        capped.R.RunUntil(start);
        Assert.Equal(0, capped.W.RevenueRateScaledOf(1));

        // DisbandStrengthPermille 1000: the army disbands at its first pulse end (men <= 100 % of the initial strength).
        var gone = Build(OccupationSetup(march: false, trade, share: 300, disbandPermille: 1000, position: 150_000), new[] { WarOf(0, start, start + 3 * W, 0, 1) }, 2, start);
        gone.R.RunUntil(start + W - 1);
        Assert.Equal(trade * 700, gone.W.RevenueRateScaledOf(1));
        gone.R.RunUntil(start + W);
        Assert.Equal(ArmyCondition.Disbanded, gone.W.ConditionOf(0));
        Assert.Equal(-1, gone.W.OccupiedLocalityOf(0));
        Assert.Equal(trade * 1000, gone.W.RevenueRateScaledOf(1));
        Assert.Contains(gone.W.Events, e => e.Kind == CascadeKind.OccupationEnded && e.AtMs == start + W);
        Assert.Equal((long)((BigInteger)trade * 700 * W / (Y * 1000)), gone.W.TreasuryOf(1));
    }

    /// <summary>Contributions are a money levy on the occupied place, raised whatever the army's grain want: an army whose
    /// train is full (it forages nothing) still levies, at its pulse start, exactly the 7-day stock draw at the per-year rate
    /// (fix round 1, E-V1: each day takes floor(left x 5,378 / 360,000); independent loop below) into its pay chest.</summary>
    [Fact]
    public void Contributions_LeviedWhateverTheGrainWant()
    {
        long start = Start(0, 10);
        var rig = Build(OccupationSetup(march: false, trade: 0, share: 0, position: 150_000), new[] { WarOf(0, start, start + W, 0, 1) }, 2, start);
        rig.R.RunUntil(start);
        var c = rig.W.Events.Single(e => e.Kind == CascadeKind.Contribution);
        long left = 1_000_000;
        for (int d = 0; d < 7; d++) left -= (long)((BigInteger)left * R.ContributionPermilleYear / (1000 * 360));
        Assert.Equal(1_000_000 - left, c.Value);
        Assert.Equal(99_996, c.Value);   // the calibration: about the former 10 % per 7-day pulse
        Assert.Equal(1_000_000 - c.Value, rig.W.LocalityMoneyOf(0));
        rig.R.RunUntil(start + W);
        Assert.Equal(0, rig.W.Rows.Single().Foraged);
        Assert.Equal(0, rig.W.Rows.Single().HungerPpm);
    }

    // ------------------------------------------------------------------------------------------------ 7. terrain and water

    /// <summary>Lane TR's Marsh, Ford and Building are covered: every terrain class has grazing and wear entries; a march over
    /// Marsh is exactly floor(W x 20 km/day / 3.0) at the Slice C cost 3000; a Building segment is refused (impassable).</summary>
    [Fact]
    public void Terrain_EveryClassCovered_MarshPassableAtItsCost_BuildingRefused()
    {
        var t = TerrainLogistics.Proposal();
        for (int c = 0; c < TerrainCostTable.ClassCount; c++)
        {
            Assert.InRange(t.GrazePermille((TerrainClass)c), 0, 1000);
            Assert.True(t.WearFactor((TerrainClass)c) >= 0);
        }
        var foot = new ArmyComposition(1000, 0, 0, 0, 0, 0, 0, 0, false);
        var cap = new PolityCapability(WarTech.None);
        var marsh = new Route(new[] { Seg(500, TerrainClass.Marsh) });
        var plan = ArmyLogistics.PlanMarch(marsh, 0, W, foot, cap, R, t);
        Assert.Equal(3000, t.MarchCost(TerrainClass.Marsh));
        Assert.Equal((long)((BigInteger)W * R.MarchFootMPerDay * 1000 / ((BigInteger)3000 * Day)), plan.DistanceM);
        Assert.True(ArmyLogistics.PlanMarch(new Route(new[] { Seg(500, TerrainClass.Ford) }), 0, W, foot, cap, R, t).DistanceM > 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Route(new[] { Seg(5, TerrainClass.Building) }));
    }

    /// <summary>A land army halts at the water's edge and a fleet at the shore (embarkation is not modelled); supply crosses
    /// Water by boat; a fleet must start afloat and a land army ashore.</summary>
    [Fact]
    public void Water_LandArmyHaltsAtTheShore_FleetAtTheCoast()
    {
        var t = TerrainLogistics.Proposal();
        var foot = new ArmyComposition(1000, 0, 0, 0, 0, 0, 0, 0, false);
        var cap = new PolityCapability(WarTech.None) { Naval = NavalCapability.Sails };
        var landThenSea = new Route(new[] { Seg(30, TerrainClass.Open), Seg(500, TerrainClass.Water) });
        Assert.Equal(30_000, ArmyLogistics.PlanMarch(landThenSea, 0, W, foot, cap, R, t).DistanceM);
        Assert.Equal(0, ArmyLogistics.PlanMarch(landThenSea, 30_000, W, foot, cap, R, t).DistanceM);
        var seaThenLand = new Route(new[] { Seg(100, TerrainClass.Water), Seg(50, TerrainClass.Open) });
        Assert.Equal(100_000, ArmyLogistics.PlanMarch(seaThenLand, 0, W, Fleet(DomainProfiles.Cog, 3), cap, R, t).DistanceM);
        Assert.Equal(CarriageMode.Water, ArmyLogistics.ModeOf(landThenSea[1], cap, foot));

        long start = Start(0, 10);
        Assert.Throws<ArgumentException>(() => Build(NavalSetup(Fleet(DomainProfiles.Cog, 3), NavalCapability.Sails, route: seaThenLand, position: 120_000),
            new[] { WarOf(0, start, start + W, 0, 1) }, 2, start));
        var landSetup = NavalSetup(Fleet(DomainProfiles.Cog, 3), NavalCapability.Sails) with
        {
            Armies = new[] { new ArmySetup(0, 0, 0, 0, foot, landThenSea, 200_000, false, 0, 0, 0, Array.Empty<(long, long)>()) },
        };
        Assert.Throws<ArgumentException>(() => Build(landSetup, new[] { WarOf(0, start, start + W, 0, 1) }, 2, start));
    }
}

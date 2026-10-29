using System.Globalization;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>Why a recruitment or a re-equipment is refused — the unit charter §6.5 / §6.6 predicate item that failed,
/// in the order they are checked. <see cref="Ok"/> is 0.</summary>
public enum RecruitVerdict
{
    Ok = 0,
    /// <summary>sim.json military is absent or off, or no unit-family content is loaded.</summary>
    Disabled = 1,
    /// <summary>The issuer is not a roster Empire.</summary>
    NotEmpire = 2,
    /// <summary>§6.5 item 3: not a settlement the issuer controls.</summary>
    NotControlled = 3,
    /// <summary>The identity key names no identity, or the identity carries no recruitment data.</summary>
    UnknownIdentity = 4,
    /// <summary>§6.5 item 1: the knowledge gate does not hold.</summary>
    NotKnown = 5,
    /// <summary>§6.5 item 7: the family has no live consumer in this build (a mobile agent: no movement, basing or water
    /// edges yet).</summary>
    NoRuleSupport = 6,
    /// <summary>§6.5 item 7: the identity names a missing system or ruling (recruitment.blockedBy).</summary>
    BlockedBySystem = 7,
    /// <summary>§6.5 item 4: an input is a PLANNED good goods.json does not carry yet (its EC packet named).</summary>
    BlockedByMissingGood = 8,
    /// <summary>§6.5 item 3: the required building is not standing at the settlement.</summary>
    NoBuilding = 9,
    /// <summary>§6.5 item 2: a later mainline identity of the same family is realizable here (latest realizable wins).</summary>
    Superseded = 10,
    /// <summary>§6.5 item 4: the purse does not hold an input beyond construction's set-asides.</summary>
    MaterialsShort = 11,
    /// <summary>§6.5 item 5: the settlement has too few adults (at least one stays).</summary>
    PersonnelShort = 12,
    /// <summary>§6.5 item 6: the purse's free grain cannot carry the upkeep (military.supportYears).</summary>
    Unsupported = 13,
    /// <summary>§6.6: the formation is not the issuer's, or does not exist.</summary>
    NotOwned = 14,
    /// <summary>§6.6: the formation is not stationed at a settlement (in transit / the field).</summary>
    NotStationed = 15,
    /// <summary>§6.6: the formation is already re-equipping.</summary>
    AlreadyReequipping = 16,
    /// <summary>§6.6: the transition class is refused (identical target, composition change, or OD-2).</summary>
    TransitionRefused = 17,
    /// <summary>A cancel of a re-equipment the formation does not have.</summary>
    NothingPending = 18,
}

/// <summary>A verdict with its reason — the words the UI shows and the tests pin.</summary>
public readonly record struct RecruitCheck(RecruitVerdict Verdict, string Reason)
{
    public bool Ok => Verdict == RecruitVerdict.Ok;
    public static RecruitCheck Pass { get; } = new(RecruitVerdict.Ok, "");
}

/// <summary>The §6.6 transition class of a permitted re-equipment.</summary>
public enum ReequipClass
{
    None = 0,
    /// <summary>(a) another family of the same composition — a role change; experience × the transfer fraction.</summary>
    RoleChange = 1,
    /// <summary>(b) the same family, mainline to a branch identity; experience kept.</summary>
    ToBranch = 2,
    /// <summary>(c) the same family, branch to branch; experience kept.</summary>
    BranchToBranch = 3,
}

/// <summary>The tables a predicate reads that the military system mutates within a step: the stockpile, the set-asides,
/// the buckets, the formations and their personnel, and the pending re-equipments. The UI and the AI read the world's own
/// (<see cref="RecruitmentQuery.ViewOf"/>); MilitarySystem passes its NEXT tables, so each order is judged against what earlier
/// orders of the same step left.</summary>
public readonly record struct MilitaryView(
    IReadOnlyTable<GoodStockRow> Stocks, IReadOnlyTable<ConstructionHoldRow> Holds, IReadOnlyTable<BucketRow> Buckets,
    IReadOnlyTable<MilitaryUnitRow> Units, IReadOnlyTable<FormationPersonnelRow> Personnel, IReadOnlyTable<ReequipRow> Reequips);

/// <summary>
/// ADR-055 — THE RECRUITMENT AND RE-EQUIPMENT PREDICATES (unit charter §6.5, §6.6; D-065 R1). ONE predicate serves
/// MilitarySystem (on PREV for knowledge and control, NEXT for what the step consumes), the action surface and the AI — so
/// what the UI offers is what the simulation applies. Pure and read-only; integer arithmetic for every stock comparison; no
/// dictionary, no LINQ, no RNG. <b>THE AGE IS NEVER READ</b> (Law 4; D-039 A5): an identity's Age is modernization metadata
/// (ADR-031) and nothing here asks which Age the polity is in.
/// </summary>
public static class RecruitmentQuery
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Whether the military rules apply: sim.json military on and unit-family content loaded.</summary>
    public static bool Enabled(SimConfig cfg) => cfg.Military is { Enabled: true } && cfg.UnitFamilies is not null;

    /// <summary>The first canonical schema whose build ran the military layer (ADR-055, v39).</summary>
    public const int FirstMilitarySchema = 39;

    /// <summary>
    /// Whether a SESSION recorded under schema <paramref name="schemaVersion"/> was played without the military layer — every
    /// session before <see cref="FirstMilitarySchema"/> (DiscoveryQuery.RecordedWithoutDiscovery's rule, four schemas later):
    /// a recorded session (seed + order log) is replayed under the rules it was played by — its founding warband a token — so
    /// the identity proof in the recorded schema's form can hold. UiSession.Resume and `sim inspect` apply it. A v37 SNAPSHOT
    /// loads into this build's rules with every saved formation a token (no personnel row).
    /// </summary>
    public static bool RecordedWithoutMilitary(int schemaVersion) => schemaVersion < FirstMilitarySchema;

    /// <summary><paramref name="cfg"/> with the military layer off — the config a session <see cref="RecordedWithoutMilitary"/>
    /// replays under.</summary>
    public static SimConfig WithoutMilitary(SimConfig cfg) =>
        cfg.Military is { Enabled: true } m ? cfg with { Military = m with { Enabled = false } } : cfg;

    public static MilitaryView ViewOf(IReadOnlyWorldState world) =>
        new(world.GoodStocks, world.ConstructionHolds, world.Buckets, world.MilitaryUnits, world.FormationPersonnel, world.Reequips);

    // ------------------------------------------------------------------ knowledge (§6.5 item 1)

    /// <summary>§6.5 item 1: the identity's knowledge gate on <paramref name="completed"/> (the polity's completed nodes). A
    /// baseline identity is known from the start; otherwise its realizedBy entity must be knowledge-eligible AND its requires
    /// expression must hold (each where given). Without research content only a baseline identity is known.</summary>
    public static bool KnowledgeMet(ResearchContent? research, bool[]? completed, UnitIdentity identity)
    {
        if (identity.Baseline is not null) return true;
        if (research is null || completed is null) return false;
        if (identity.RealizedBy is { } entity)
        {
            int e = research.EntityIndexOf(entity);
            if (e < 0 || !ResearchQuery.IsKnowledgeEligible(research, e, completed)) return false;
        }
        if (identity.RequiresSource is not null)
        {
            if (identity.Requires is not { } requirement || !ResearchQuery.RequirementMet(research, requirement, completed)) return false;
        }
        return identity.RealizedBy is not null || identity.RequiresSource is not null;
    }

    /// <summary>The words of an identity's knowledge gate ("needs unit.spearmen", "needs iron_bloomery AND standing_army").</summary>
    public static string GateText(UnitIdentity identity)
    {
        if (identity.Baseline is { } b) return "known from the start (" + b + ")";
        if (identity.RealizedBy is { } e && identity.RequiresSource is { } r) return e + " AND (" + r + ")";
        return identity.RealizedBy ?? identity.RequiresSource ?? "no gate";
    }

    // ------------------------------------------------------------------ the §6.5 predicate

    /// <summary>
    /// THE RECRUITMENT PREDICATE (§6.5, all seven items) for <paramref name="polity"/> raising identity
    /// <paramref name="identityKey"/> at <paramref name="settlement"/>. Knowledge and control are read from
    /// <paramref name="world"/>; stocks, set-asides, buckets, formations and personnel from <paramref name="view"/>.
    /// </summary>
    public static RecruitCheck CheckRecruit(
        IReadOnlyWorldState world, SimConfig cfg, MilitaryView view, PolityId polity, SettlementId settlement, int identityKey,
        bool[]? completed = null)
    {
        if (!Enabled(cfg)) return new(RecruitVerdict.Disabled, "military recruitment is off in this configuration");
        if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, polity, out _))
            return new(RecruitVerdict.NotEmpire, "not a registered Empire");
        if (!SettlementExists(world, settlement) || !EmpireQuery.ControlsSettlement(world, polity, settlement))
            return new(RecruitVerdict.NotControlled, "not a settlement you control");
        UnitFamilyContent families = cfg.UnitFamilies!;
        UnitIdentity? identity = families.IdentityByKey(identityKey);
        if (identity?.Recruitment is null || families.FamilyByKey(identity.FamilyKey) is not { } family)
            return new(RecruitVerdict.UnknownIdentity, "no recruitable identity " + identityKey.ToString(Inv));
        completed ??= cfg.Research is { } research ? ResearchQuery.CompletedMask(world, research, polity) : null;

        RecruitCheck own = OwnPredicate(world, cfg, view, polity, settlement, identity, family, identity.Recruitment.Personnel, completed);
        if (!own.Ok) return own;
        // §6.5 item 2: the latest realizable identity in the family's line order. A later MAINLINE identity whose own
        // predicate holds supersedes a mainline one; a branch is recruitable alongside whatever the mainline realizes.
        if (!identity.Branch && LaterRealizable(world, cfg, view, polity, settlement, identity, family, completed) is { } later)
            return new(RecruitVerdict.Superseded, later.Name + " is the latest realizable " + family.Name + " here");
        return RecruitCheck.Pass;
    }

    /// <summary>The first mainline identity after <paramref name="identity"/> in <paramref name="family"/>'s line order whose
    /// own predicate holds at the settlement, or null.</summary>
    public static UnitIdentity? LaterRealizable(
        IReadOnlyWorldState world, SimConfig cfg, MilitaryView view, PolityId polity, SettlementId settlement,
        UnitIdentity identity, UnitFamily family, bool[]? completed)
    {
        UnitIdentity? latest = null;
        bool after = false;
        for (int i = 0; i < family.Line.Count; i++)
        {
            UnitIdentity candidate = family.Line[i];
            if (candidate.Key == identity.Key) { after = true; continue; }
            if (!after || candidate.Branch || candidate.Recruitment is null) continue;
            if (OwnPredicate(world, cfg, view, polity, settlement, candidate, family, candidate.Recruitment.Personnel, completed).Ok)
                latest = candidate;
        }
        return latest;
    }

    /// <summary>§6.5 items 1 and 3–7 for one identity (item 2 is <see cref="CheckRecruit"/>'s), in this order: knowledge; a
    /// named missing system (blockedBy); a planned good; the building; rule support (a live consumer for the family); the
    /// materials; <paramref name="personnel"/> adults; support for the owner's formations plus <paramref name="personnel"/>
    /// more people. The content blockers come before rule support so a blocked identity names what it waits for.</summary>
    public static RecruitCheck OwnPredicate(
        IReadOnlyWorldState world, SimConfig cfg, MilitaryView view, PolityId polity, SettlementId settlement,
        UnitIdentity identity, UnitFamily family, long personnel, bool[]? completed)
    {
        UnitRecruitment r = identity.Recruitment!;
        if (!KnowledgeMet(cfg.Research, completed, identity))
            return new(RecruitVerdict.NotKnown, "not known: needs " + GateText(identity));
        if (r.BlockedBy is { } blocked) return new(RecruitVerdict.BlockedBySystem, "waits for " + blocked);
        for (int i = 0; i < r.Inputs.Count; i++)
            if (r.Inputs[i].IsPlanned)
                return new(RecruitVerdict.BlockedByMissingGood,
                    "needs " + r.Inputs[i].Good + ", which does not exist yet (" + r.Inputs[i].PlannedPacket + ")");
        if (r.Building is { } building && !BuildingStands(world, cfg, settlement, building))
            return new(RecruitVerdict.NoBuilding, BuildingBuildable(cfg, building)
                ? "needs a completed " + building + " here"
                : "needs a completed " + building + " here, and no goods.json project builds one yet");
        // Item 7 after the content blockers, so a blocked identity names the good, building or system it waits for first.
        if (family.Composition != UnitComposition.ArmyFormation)
            return new(RecruitVerdict.NoRuleSupport, family.Name + " is a mobile agent: no movement, basing or water edges exist in this build (M7.2 / M7.6)");
        SettlementId[] holders = EmpireStockpile.Holders(world, settlement);
        for (int i = 0; i < r.Inputs.Count; i++)
        {
            var good = new GoodId(r.Inputs[i].GoodId);
            long free = DiscoveryQuery.FreeHolding(world, cfg, view.Stocks, view.Holds, holders, good, out _);
            if (free < r.Inputs[i].Qty)
                return new(RecruitVerdict.MaterialsShort, "needs " + r.Inputs[i].Qty.ToString(Inv) + " " + r.Inputs[i].Good
                    + " (the purse has " + free.ToString(Inv) + " free)");
        }
        if (personnel > 0)
        {
            long adults = BandViews.Adults(view.Buckets, settlement);
            if (adults <= personnel)
                return new(RecruitVerdict.PersonnelShort, "needs " + personnel.ToString(Inv) + " adults here (has "
                    + adults.ToString(Inv) + "; at least one stays)");
        }
        long upkeep = SupportNeed(cfg, view, polity, personnel);
        if (upkeep > 0 && cfg.Goods is { } goods)
        {
            long grain = DiscoveryQuery.FreeHolding(world, cfg, view.Stocks, view.Holds, holders, new GoodId(goods.GrainId), out _);
            long need = upkeep + InputQty(r, goods.GrainId);
            if (grain < need)
                return new(RecruitVerdict.Unsupported, "cannot carry the upkeep: " + need.ToString(Inv) + " "
                    + goods.ById(goods.GrainId).Name + " needed for a year of every formation (the purse has " + grain.ToString(Inv) + " free)");
        }
        return RecruitCheck.Pass;
    }

    /// <summary>§6.5 item 6 — the grain the owner's formations eat in military.supportYears, the
    /// <paramref name="extraPersonnel"/> new people included, rounded up to whole units.</summary>
    public static long SupportNeed(SimConfig cfg, MilitaryView view, PolityId polity, long extraPersonnel)
    {
        MilitaryConfig m = cfg.Military!;
        long people = checked(OwnerPersonnel(view, polity) + Math.Max(0, extraPersonnel));
        double need = people * m.UpkeepGrainPerPersonYear * m.SupportYears;
        return need <= 0.0 ? 0 : (long)Math.Ceiling(need);
    }

    /// <summary>The people every formation <paramref name="polity"/> owns holds (FormationPersonnel joined to MilitaryUnits).</summary>
    public static long OwnerPersonnel(MilitaryView view, PolityId polity)
    {
        long total = 0;
        for (int i = 0; i < view.Personnel.Count; i++)
        {
            int u = UnitIndex(view.Units, view.Personnel[i].Unit);
            if (u >= 0 && view.Units[u].Owner.Value == polity.Value) total = checked(total + view.Personnel[i].Count.Value);
        }
        return total;
    }

    private static long InputQty(UnitRecruitment r, int good)
    {
        for (int i = 0; i < r.Inputs.Count; i++) if (r.Inputs[i].GoodId == good) return r.Inputs[i].Qty;
        return 0;
    }

    // ------------------------------------------------------------------ the §6.6 re-equipment predicate

    /// <summary>
    /// THE TRANSITION POLICY (§6.6; D-065 R1) — the ONE function that decides which identity changes a re-equipment may make.
    /// Refused: the identity it already is; a change of composition (a disband followed by a recruitment); and the same
    /// family to a MAINLINE identity — "OD-2: same-line catch-up is not ruled" (it lies between D-065 R1 and D-047 ruling 18,
    /// escalated in the unit charter §16.4; isolated here so a ruling changes one line). Permitted: (a) another family,
    /// (b) the same family to a branch, (c) branch to branch.
    /// </summary>
    public static (ReequipClass Class, string? Refusal) TransitionPolicy(UnitFamilyContent content, UnitIdentity source, UnitIdentity target)
    {
        if (source.Key == target.Key) return (ReequipClass.None, "the formation already is " + target.Name);
        UnitFamily? from = content.FamilyByKey(source.FamilyKey), to = content.FamilyByKey(target.FamilyKey);
        if (from is null || to is null) return (ReequipClass.None, "unknown family");
        if (from.Composition != to.Composition)
            return (ReequipClass.None, "a change of composition (" + from.Name + " to " + to.Name + ") is a disband and a recruitment, not a re-equipment");
        if (from.Key != to.Key) return (ReequipClass.RoleChange, null);
        if (!target.Branch) return (ReequipClass.None, OD2Refusal);
        return (source.Branch ? ReequipClass.BranchToBranch : ReequipClass.ToBranch, null);
    }

    /// <summary>The refusal reason for a same-family move to a mainline identity (OD-2, unresolved).</summary>
    public const string OD2Refusal = "OD-2: same-line catch-up is not ruled";

    /// <summary>
    /// THE RE-EQUIPMENT PREDICATE (§6.6 legality) for <paramref name="polity"/> re-equipping formation <paramref name="unitId"/>
    /// to <paramref name="targetKey"/>: the issuer owns it; it is stationed at a settlement the issuer controls and not already
    /// re-equipping; the transition class is permitted (<see cref="TransitionPolicy"/>); and the target passes §6.5 items 1,
    /// 3, 4, 6 and 7 there, with the personnel difference to the target's strength as item 5.
    /// </summary>
    /// <summary>
    /// The ownership gate of every formation-addressed order (ReequipFormation, its cancel, DisbandFormation): the formation
    /// <paramref name="unitId"/> exists AND is <paramref name="polity"/>'s. A formation that does not exist and one another
    /// polity owns are refused with the SAME verdict and words ("not your formation"), so the answer tells the issuer nothing
    /// of anyone else's formations (P7). <paramref name="unit"/> is the issuer's own row when it passes, default otherwise.
    /// </summary>
    public static bool IsOwnFormation(IReadOnlyTable<MilitaryUnitRow> units, PolityId polity, int unitId, out MilitaryUnitRow unit)
    {
        int u = UnitIndex(units, unitId);
        unit = u >= 0 && units[u].Owner.Value == polity.Value ? units[u] : default;
        return u >= 0 && units[u].Owner.Value == polity.Value;
    }

    /// <summary>ADR-055 — the cancel of formation <paramref name="unitId"/>'s pending re-equipment (ReequipFormation, Amount 0):
    /// the layer is on, the formation is the issuer's (<see cref="IsOwnFormation"/>), and a re-equipment is pending.</summary>
    public static RecruitCheck CheckCancelReequip(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, int unitId)
    {
        if (!Enabled(cfg)) return new(RecruitVerdict.Disabled, "military re-equipment is off in this configuration");
        if (!IsOwnFormation(world.MilitaryUnits, polity, unitId, out _)) return new(RecruitVerdict.NotOwned, "not your formation");
        if (PendingReequip(world, unitId) is null) return new(RecruitVerdict.NothingPending, "no re-equipment is pending");
        return RecruitCheck.Pass;
    }

    /// <summary>ADR-055 — DisbandFormation of formation <paramref name="unitId"/>: the layer is on, the formation is the issuer's
    /// (<see cref="IsOwnFormation"/>) and it is stationed at a settlement (its people return there).</summary>
    public static RecruitCheck CheckDisband(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, int unitId)
    {
        if (!Enabled(cfg)) return new(RecruitVerdict.Disabled, "military disbandment is off in this configuration");
        if (!IsOwnFormation(world.MilitaryUnits, polity, unitId, out MilitaryUnitRow unit)) return new(RecruitVerdict.NotOwned, "not your formation");
        if (unit.Location.Value < 0 || !SettlementExists(world, unit.Location))
            return new(RecruitVerdict.NotStationed, "not stationed at a settlement");
        return RecruitCheck.Pass;
    }

    public static RecruitCheck CheckReequip(
        IReadOnlyWorldState world, SimConfig cfg, MilitaryView view, PolityId polity, int unitId, int targetKey,
        out ReequipClass transition, bool[]? completed = null)
    {
        transition = ReequipClass.None;
        if (!Enabled(cfg)) return new(RecruitVerdict.Disabled, "military re-equipment is off in this configuration");
        if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, polity, out _))
            return new(RecruitVerdict.NotEmpire, "not a registered Empire");
        int u = UnitIndex(view.Units, unitId);
        if (u < 0 || view.Units[u].Owner.Value != polity.Value) return new(RecruitVerdict.NotOwned, "not your formation");
        MilitaryUnitRow unit = view.Units[u];
        if (unit.Location.Value < 0 || !SettlementExists(world, unit.Location))
            return new(RecruitVerdict.NotStationed, "not stationed at a settlement");
        if (!EmpireQuery.ControlsSettlement(world, polity, unit.Location))
            return new(RecruitVerdict.NotControlled, "stationed at a settlement you do not control");
        if (ReequipIndex(view.Reequips, unitId) >= 0) return new(RecruitVerdict.AlreadyReequipping, "already re-equipping");
        UnitFamilyContent families = cfg.UnitFamilies!;
        UnitIdentity? target = families.IdentityByKey(targetKey);
        UnitIdentity? source = families.IdentityByKey(unit.Identity);
        if (target?.Recruitment is null || families.FamilyByKey(target.FamilyKey) is not { } family)
            return new(RecruitVerdict.UnknownIdentity, "no recruitable identity " + targetKey.ToString(Inv));
        if (source is null) return new(RecruitVerdict.UnknownIdentity, "the formation's identity is unknown to this content");
        (ReequipClass cls, string? refusal) = TransitionPolicy(families, source, target);
        if (refusal is not null) return new(RecruitVerdict.TransitionRefused, refusal);
        completed ??= cfg.Research is { } research ? ResearchQuery.CompletedMask(world, research, polity) : null;
        long extra = Math.Max(0, target.Recruitment.Personnel - PersonnelOf(view.Personnel, unitId));
        RecruitCheck own = OwnPredicate(world, cfg, view, polity, unit.Location, target, family, extra, completed);
        if (!own.Ok) return own;
        transition = cls;
        return RecruitCheck.Pass;
    }

    /// <summary>The re-equipment's duration in whole days: the target's recruitment years × the year length, rounded up, at
    /// least one day (a rate × dtYears accrued as dtDays — Law 3, exact in integers).</summary>
    public static long RequiredDays(UnitIdentity target) =>
        Math.Max(1L, (long)Math.Ceiling(target.Recruitment!.Years * Kernel.SimClock.YearDays));

    /// <summary>Experience after a re-equipment of class <paramref name="cls"/>: kept for (b) and (c); × the transfer fraction
    /// for (a); never increased.</summary>
    public static double ExperienceAfter(SimConfig cfg, ReequipClass cls, double experience) =>
        cls == ReequipClass.RoleChange ? Math.Min(experience, experience * cfg.Military!.ReequipExperienceTransfer) : experience;

    /// <summary>The class a pending re-equipment completes with, read from the formation's CURRENT family (after any
    /// modernization of the completing step): another family is a role change (a); the same family is (b) or (c).</summary>
    public static ReequipClass CompletionClass(UnitFamilyContent content, int sourceFamily, int sourceIdentity, int targetIdentity)
    {
        UnitIdentity? target = content.IdentityByKey(targetIdentity);
        if (target is null || target.FamilyKey != sourceFamily) return ReequipClass.RoleChange;
        UnitIdentity? source = content.IdentityByKey(sourceIdentity);
        return source is { Branch: true } ? ReequipClass.BranchToBranch : ReequipClass.ToBranch;
    }

    // ------------------------------------------------------------------ embark (aviation-progression §9.1)

    /// <summary>The one embark predicate (carrier class × identity): an aircraft identity marked <c>any</c> embarks on any
    /// carrier identity; one marked <c>jet_deck</c> only on a carrier whose deck takes jets; <c>no</c> never. Declarative —
    /// no basing system consumes it in this build.</summary>
    public static bool CanEmbark(UnitFamilyContent content, UnitIdentity aircraft, UnitIdentity carrier)
    {
        UnitFamily? carriers = content.FamilyById("carriers");
        if (carriers is null || carrier.FamilyKey != carriers.Key) return false;
        return aircraft.CarrierCapable switch
        {
            CarrierCapability.Any => true,
            CarrierCapability.JetDeck => carrier.JetDeck,
            _ => false,
        };
    }

    // ------------------------------------------------------------------ people: draw and return

    /// <summary>
    /// How <paramref name="qty"/> adults are drawn from <paramref name="settlement"/>'s adult buckets (cohorts 15–59, every
    /// culture, religion and class): proportionally to each row's count, floors first, the remainder one by one to the rows
    /// with the largest integer remainder of qty × count / total, ties to the lower row index — exact integer arithmetic.
    /// Returns per-row amounts indexed by bucket row (zero elsewhere), or null when the adults cannot cover it.
    /// </summary>
    public static long[]? AllocateDraw(IReadOnlyTable<BucketRow> buckets, SettlementId settlement, long qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        long total = BandViews.Adults(buckets, settlement);
        if (qty > total) return null;
        return Proportional(buckets, settlement, qty, total);
    }

    /// <summary>
    /// How <paramref name="qty"/> people return to <paramref name="settlement"/>'s adult buckets: proportionally to the current
    /// adult counts (the <see cref="AllocateDraw"/> rule); when the settlement has no adult left, all of them to its first adult
    /// bucket row. Null when the settlement has no adult bucket row at all.
    /// </summary>
    public static long[]? AllocateReturn(IReadOnlyTable<BucketRow> buckets, SettlementId settlement, long qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        long total = BandViews.Adults(buckets, settlement);
        if (total > 0) return Proportional(buckets, settlement, qty, total);
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Settlement != settlement || !BandViews.IsAdult(buckets[i].CohortIdx)) continue;
            var result = new long[buckets.Count];
            result[i] = qty;
            return result;
        }
        return null;
    }

    private static long[] Proportional(IReadOnlyTable<BucketRow> buckets, SettlementId settlement, long qty, long total)
    {
        var take = new long[buckets.Count];
        var rem = new long[buckets.Count];
        long given = 0;
        checked
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                BucketRow b = buckets[i];
                if (b.Settlement != settlement || !BandViews.IsAdult(b.CohortIdx) || b.Count.Value <= 0) { rem[i] = -1; continue; }
                Int128 product = (Int128)qty * b.Count.Value;
                take[i] = (long)(product / total);
                rem[i] = (long)(product % total);
                given += take[i];
            }
        }
        long left = qty - given;
        while (left > 0)
        {
            int best = -1;
            for (int i = 0; i < buckets.Count; i++)
                if (rem[i] >= 0 && (best < 0 || rem[i] > rem[best])) best = i;
            if (best < 0) break;
            take[best]++;
            rem[best] = -1;
            left--;
        }
        return take;
    }

    // ------------------------------------------------------------------ lookups and the published-state invariant

    public static int UnitIndex(IReadOnlyTable<MilitaryUnitRow> units, int id)
    {
        for (int i = 0; i < units.Count; i++) if (units[i].Id == id) return i;
        return -1;
    }

    public static int PersonnelIndex(IReadOnlyTable<FormationPersonnelRow> rows, int unit)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Unit == unit) return i;
        return -1;
    }

    public static long PersonnelOf(IReadOnlyTable<FormationPersonnelRow> rows, int unit)
    {
        int i = PersonnelIndex(rows, unit);
        return i < 0 ? 0 : rows[i].Count.Value;
    }

    public static int ReequipIndex(IReadOnlyTable<ReequipRow> rows, int unit)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Unit == unit) return i;
        return -1;
    }

    /// <summary>The pending re-equipment of formation <paramref name="unit"/>, if any.</summary>
    public static ReequipRow? PendingReequip(IReadOnlyWorldState world, int unit)
    {
        int i = ReequipIndex(world.Reequips, unit);
        return i < 0 ? null : world.Reequips[i];
    }

    /// <summary>The next formation id: one past the highest id the formation table, the modernization log and the
    /// re-equipment log have ever recorded — an id is never reissued (a disbandment is always logged, cause 3).</summary>
    public static int NextUnitId(IReadOnlyTable<MilitaryUnitRow> units, IReadOnlyTable<UnitConversionRow> conversions, IReadOnlyTable<ReequipLogRow> log)
    {
        int max = 0;
        for (int i = 0; i < units.Count; i++) if (units[i].Id > max) max = units[i].Id;
        for (int i = 0; i < conversions.Count; i++) if (conversions[i].Unit > max) max = conversions[i].Unit;
        for (int i = 0; i < log.Count; i++) if (log[i].Unit > max) max = log[i].Unit;
        return checked(max + 1);
    }

    /// <summary>Whether a completed structure at <paramref name="settlement"/> realizes research building entity
    /// <paramref name="building"/> (a goods.json project whose entity it is, with a positive built count).</summary>
    public static bool BuildingStands(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, string building)
    {
        if (cfg.Goods?.Projects is not { } projects) return false;
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            if (row.Settlement != settlement || row.Count <= 0) continue;
            for (int p = 0; p < projects.Length; p++)
                if (projects[p].Id == row.ProjectId && string.Equals(projects[p].Entity, building, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Whether any goods.json project builds <paramref name="building"/>.</summary>
    public static bool BuildingBuildable(SimConfig cfg, string building)
    {
        foreach (ConstructionProjectEntry p in cfg.Goods?.Projects ?? [])
            if (string.Equals(p.Entity, building, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool SettlementExists(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++) if (world.Settlements[i].Id.Value == settlement.Value) return true;
        return false;
    }

    /// <summary>
    /// THE v39 INVARIANT (Snapshot.Load refuses a save breaking it; MilitarySystem asserts it at the end of every step):
    /// personnel rows strictly ascending by Unit, Count ≥ 0, each naming a formation; re-equipment rows strictly ascending by
    /// Unit, each naming a formation, RequiredDays ≥ 1 and 0 ≤ ElapsedDays &lt; RequiredDays; escrow rows strictly ascending
    /// (Unit, Good), Units &gt; 0, Good ≥ 0, each belonging to a pending re-equipment; log causes 1–3. Null when it holds.
    /// </summary>
    public static string? InvariantViolation(IReadOnlyWorldState world) =>
        InvariantViolation(world.MilitaryUnits, world.FormationPersonnel, world.Reequips, world.ReequipEscrow, world.ReequipLog);

    /// <summary><see cref="InvariantViolation(IReadOnlyWorldState)"/> over explicit tables (MilitarySystem's NEXT tables).</summary>
    public static string? InvariantViolation(
        IReadOnlyTable<MilitaryUnitRow> units, IReadOnlyTable<FormationPersonnelRow> personnel, IReadOnlyTable<ReequipRow> reequips,
        IReadOnlyTable<ReequipEscrowRow> escrow, IReadOnlyTable<ReequipLogRow> log)
    {
        var world = (Units: units, FormationPersonnel: personnel, Reequips: reequips, ReequipEscrow: escrow, ReequipLog: log, MilitaryUnits: units);
        for (int i = 0; i < world.FormationPersonnel.Count; i++)
        {
            FormationPersonnelRow r = world.FormationPersonnel[i];
            if (i > 0 && world.FormationPersonnel[i - 1].Unit >= r.Unit)
                return string.Create(Inv, $"formation personnel rows out of order or duplicated at row {i} (unit {r.Unit})");
            if (r.Count.Value < 0) return string.Create(Inv, $"formation {r.Unit} holds {r.Count.Value} people");
            if (UnitIndex(world.MilitaryUnits, r.Unit) < 0) return string.Create(Inv, $"personnel row for formation {r.Unit}, which does not exist");
        }
        for (int i = 0; i < world.Reequips.Count; i++)
        {
            ReequipRow r = world.Reequips[i];
            if (i > 0 && world.Reequips[i - 1].Unit >= r.Unit)
                return string.Create(Inv, $"re-equipment rows out of order or duplicated at row {i} (unit {r.Unit})");
            if (UnitIndex(world.MilitaryUnits, r.Unit) < 0) return string.Create(Inv, $"re-equipment of formation {r.Unit}, which does not exist");
            if (r.RequiredDays < 1 || r.ElapsedDays < 0 || r.ElapsedDays >= r.RequiredDays)
                return string.Create(Inv, $"re-equipment of formation {r.Unit} at {r.ElapsedDays} of {r.RequiredDays} days");
        }
        for (int i = 0; i < world.ReequipEscrow.Count; i++)
        {
            ReequipEscrowRow r = world.ReequipEscrow[i];
            if (i > 0)
            {
                ReequipEscrowRow p = world.ReequipEscrow[i - 1];
                if (p.Unit > r.Unit || (p.Unit == r.Unit && p.Good >= r.Good))
                    return string.Create(Inv, $"escrow rows out of order or duplicated at row {i} (unit {r.Unit}, good {r.Good})");
            }
            if (r.Good < 0 || r.Units.Value <= 0) return string.Create(Inv, $"escrow row {i} holds {r.Units.Value} of good {r.Good}");
            if (ReequipIndex(world.Reequips, r.Unit) < 0) return string.Create(Inv, $"escrow for formation {r.Unit}, which is not re-equipping");
        }
        for (int i = 0; i < world.ReequipLog.Count; i++)
            if (world.ReequipLog[i].Cause is < 1 or > 3) return string.Create(Inv, $"re-equipment log row {i} has cause {world.ReequipLog[i].Cause}");
        return null;
    }
}

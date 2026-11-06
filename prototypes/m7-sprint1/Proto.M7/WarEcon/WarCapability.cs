namespace Proto.M7.WarEcon;

/// <summary>
/// Technologies and institutions that change how a war is supplied and paid for. A polity HOLDS a flag because its
/// computed state says so (research completed, an institution founded): Law 4, no calendar gate. Nothing in WarEcon reads
/// a date, an era band or an era label to decide a capability; the tests pin that the same flags give the same economics
/// at any instant (<c>WarEconLawTests.Capability_IsStateDerived_NotDateDerived</c>).
/// </summary>
[Flags]
public enum WarTech : ulong
{
    None = 0,
    /// <summary>Minted coin and a fiscal apparatus: soldiers can be paid in money. Without it pay is in kind (grain).</summary>
    Coinage = 1UL << 0,
    /// <summary>Public credit (banks, bond markets): the polity can borrow for war against future revenue.</summary>
    Banking = 1UL << 1,
    /// <summary>Firearms and artillery: such units exist only with it, and only they consume munitions.</summary>
    Gunpowder = 1UL << 2,
    /// <summary>A standing commissariat with depots (magazine system): supply-line losses are lower.</summary>
    Magazines = 1UL << 3,
    /// <summary>Railways: rail segments carry troops and supply.</summary>
    Railways = 1UL << 4,
    /// <summary>Internal-combustion motor transport: trucks exist only with it, and only they consume fuel (oil).</summary>
    MotorTransport = 1UL << 5,
    /// <summary>Camp sanitation and field medicine: lower disease mortality.</summary>
    Sanitation = 1UL << 7,
    /// <summary>Antiscorbutics issued at sea (citrus juice or another proven preventive): scurvy mortality is cut. A polity
    /// holds it from computed state (the knowledge and the supply system), never from a date.</summary>
    Antiscorbutics = 1UL << 8,
}

/// <summary>
/// The war-economy capability of a polity, DERIVED from its computed state. In the campaign this is a projection of the
/// research and institution tables (ResearchQuery knowledge eligibility, institution lifecycles); in this prototype the
/// caller passes the set of held technologies, which stands in for that projection. There is no constructor from a date.
/// </summary>
public readonly record struct PolityCapability(WarTech Held)
{
    /// <summary>Naval capability (lane N's <see cref="Proto.M7.Tactical.NavalCapability"/>: oars, sails, steam power, ...),
    /// from the same computed state. It decides which vessel classes a polity can man (lane N <c>NavalDesign</c>).</summary>
    public Proto.M7.Tactical.NavalCapability Naval { get; init; }

    public bool Has(WarTech t) => (Held & t) == t;

    public bool HasNaval(Proto.M7.Tactical.NavalCapability n) => (Naval & n) == n;

    /// <summary>Derive from the polity's known technology ids (a stand-in for the research table). Unknown ids are ignored.</summary>
    public static PolityCapability FromKnown(ReadOnlySpan<int> knownTechIds, ReadOnlySpan<int> techIdOfFlagBit)
    {
        WarTech held = WarTech.None;
        for (int b = 0; b < techIdOfFlagBit.Length && b < 64; b++)
            for (int k = 0; k < knownTechIds.Length; k++)
                if (knownTechIds[k] == techIdOfFlagBit[b]) { held |= (WarTech)(1UL << b); break; }
        return new PolityCapability(held);
    }
}

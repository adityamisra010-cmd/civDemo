using Sim.Core.State;

namespace Sim.Ui.World;

/// <summary>
/// M6 E1.4 — THE OBJECT LAYER'S STYLE REGISTER, four rungs of built material (D-066 A3: "the register is driven by
/// REALIZED state; the Age banner is the only Age-keyed element"; Law 4: no calendar gates; D-038 H5: the sprite is a READ
/// of state). Earthen (mud, thatch, round huts) → Timber (framed walls, shingle) → Masonry (stone, tile) → Engineered
/// (dressed stone, slate, flat roofs).
/// </summary>
public enum ObjectRegister { Earthen = 0, Timber = 1, Masonry = 2, Engineered = 3 }

/// <summary>
/// M6 E1.4 — THE REGISTER AS A PURE FUNCTION OF REALIZED STATE (<see cref="Of"/>). Its only inputs are what the
/// settlement HAS BUILT and what reaches it, as the projection carries them: its structure rows (counted), its FOUNDED
/// institutions (counted), and the best road class ACHIEVED on a travelled route that ends at it — never the Age, a date,
/// the era, the knowledge mask or anything researched-but-unbuilt (a capability the settlement does not exercise draws
/// nothing; Law 4). Each rung names the state that earns it:
/// <list type="bullet">
/// <item>TIMBER — any built structure, or a route of trackway class or better;</item>
/// <item>MASONRY — a founded institution, <see cref="MasonryStructures"/> structures in all, or a built road (built or
/// paved class);</item>
/// <item>ENGINEERED — a macadam road or highway, or <see cref="EngineeredInstitutions"/> founded institutions.</item>
/// </list>
/// The register is the MAX of the rungs its state earns: monotone — building more never lowers it. The painters switch
/// on the register's tokens (<see cref="RoofOf"/>, <see cref="MapInk.WallOf"/>, <see cref="MapInk.RoofOf"/>), never on
/// an Age or era number (the source guard of <c>ObjectLayerTests</c>).
/// </summary>
public static class StyleRegister
{
    /// <summary>Structures (all kinds, summed) that earn the masonry rung on their own.</summary>
    public const long MasonryStructures = 10;

    /// <summary>Founded institutions (all types, summed) that earn the engineered rung on their own — a campus-scale
    /// presence (D-043 D2's "campus" stage).</summary>
    public const long EngineeredInstitutions = 20;

    /// <summary>The register of <paramref name="s"/>, from its realized state only.</summary>
    public static ObjectRegister Of(SettlementLensView s) =>
        Of(StructureTotal(s), InstitutionTotal(s), s.TopRouteClass);

    /// <summary>The register from its three inputs: built structures (summed), founded institutions (summed) and the best
    /// achieved road class of a travelled route ending at the settlement (<see cref="EdgeTypes"/>; 0 or
    /// <see cref="EdgeTypes.DirtPath"/> for none).</summary>
    public static ObjectRegister Of(long structures, long institutions, int topRouteClass)
    {
        int rung = 0;
        if (structures > 0 || topRouteClass >= EdgeTypes.Trackway) rung = 1;
        if (institutions > 0 || structures >= MasonryStructures || topRouteClass >= EdgeTypes.BuiltRoad) rung = 2;
        if (institutions >= EngineeredInstitutions || topRouteClass >= EdgeTypes.MacadamRoad) rung = 3;
        return (ObjectRegister)rung;
    }

    /// <summary>The roof form of the register's dwellings: round huts (a dome) when earthen, pitched roofs when timber or
    /// masonry, flat roofs when engineered.</summary>
    public static RoofForm RoofOf(ObjectRegister r) => r switch
    {
        ObjectRegister.Earthen => RoofForm.Dome,
        ObjectRegister.Engineered => RoofForm.Flat,
        _ => RoofForm.Gable,
    };

    private static long StructureTotal(SettlementLensView s)
    {
        long n = 0;
        foreach (StructureView st in s.Structures) n += Math.Max(0, st.Count);
        return n;
    }

    private static long InstitutionTotal(SettlementLensView s)
    {
        long n = 0;
        foreach (InstitutionView iv in s.Institutions) n += Math.Max(0, iv.Count);
        return n;
    }
}

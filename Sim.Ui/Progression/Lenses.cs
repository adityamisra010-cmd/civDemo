using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>The seven lenses of the Director ledger §3, in ledger order. Technology and Civics
/// both live under <see cref="KnowledgeAndTechnology"/> (ruling 11).</summary>
public enum Lens { KnowledgeAndTechnology = 0, Techniques, Institutions, Infrastructure, Industry, Military, Applications }

/// <summary>How much of a lens the simulation actually backs today.</summary>
public enum LensStatus { Functional, PartialData, NotYetSimulated }

/// <remarks>M5 polish: <c>Subjects</c> (parallel to <c>Items</c>, optional) is each item's info subject for Shift+click.</remarks>
public sealed record LensSection(string Heading, string Note, IReadOnlyList<string> Items, IReadOnlyList<InfoSubject?>? Subjects = null);

public sealed record LensPage(Lens Lens, string Title, string Purpose, LensStatus Status, string StatusNote, IReadOnlyList<LensSection> Sections);

/// <summary>
/// The lens bar's pages. Every item is read from the loaded content and the completed-knowledge
/// set via <see cref="ResearchQuery"/>; where no system produces a lens's facts yet the page
/// says so instead of showing anything invented.
/// </summary>
public static class Lenses
{
    public static IReadOnlyList<Lens> All { get; } =
        [Lens.KnowledgeAndTechnology, Lens.Techniques, Lens.Institutions, Lens.Infrastructure, Lens.Industry, Lens.Military, Lens.Applications];

    public static string Label(Lens l) => l switch
    {
        Lens.KnowledgeAndTechnology => "KNOWLEDGE & TECHNOLOGY",
        Lens.Techniques => "TECHNIQUES",
        Lens.Institutions => "INSTITUTIONS",
        Lens.Infrastructure => "INFRASTRUCTURE",
        Lens.Industry => "INDUSTRY",
        Lens.Military => "MILITARY",
        Lens.Applications => "APPLICATIONS",
        _ => l.ToString(),
    };

    /// <remarks>ADR-057 (M6 CIV, P5/P6): with the session's <paramref name="cfg"/> the INSTITUTIONS lens also shows every
    /// institution type's stage (Unresearched → Available → Active, with the locked reason), the regime modules and the
    /// DERIVED government label, and the government forms ("Available - adoption in M8", D-066 A10). Without it (a content
    /// preview) those sections are omitted.</remarks>
    public static LensPage Page(Lens lens, IReadOnlyWorldState world, ResearchContent content, PolityId polity, SimConfig? cfg = null)
    {
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        switch (lens)
        {
            case Lens.KnowledgeAndTechnology:
                return new LensPage(lens, "Knowledge & Technology", "What the civilization knows: the Technology and Civics trees.",
                    LensStatus.Functional, "Simulated by the ResearchSystem.", []);
            case Lens.Techniques:
            {
                var items = new List<string>();
                var subjects = new List<InfoSubject?>();
                for (int i = 0; i < completed.Length; i++)
                    if (completed[i]) foreach (string t in content.Nodes[i].Techniques) { items.Add(t + "  -  " + content.Nodes[i].Name); subjects.Add(InfoSubject.Node(content.Nodes[i].Key)); }
                return new LensPage(lens, "Techniques", "Practices the completed knowledge makes possible.",
                    LensStatus.PartialData, "Listed from completed research. Technique adoption and diffusion are not yet simulated.",
                    [new LensSection("Known techniques", items.Count + " from completed nodes", items, subjects)]);
            }
            case Lens.Institutions:
            {
                var civics = new List<string>();
                var civicSubjects = new List<InfoSubject?>();
                for (int i = 0; i < completed.Length; i++)
                    if (completed[i] && content.Nodes[i].Tree == ResearchTree.Civics) { civics.Add(content.Nodes[i].Name); civicSubjects.Add(InfoSubject.Node(content.Nodes[i].Key)); }
                // D-D / I-2 (CivicState): a completed civic is RESEARCHED — its knowledge holds — never "adopted"; adoption is M8.
                return new LensPage(lens, "Institutions", "How the society is organised: researched civics and the institutions they allow.",
                    LensStatus.PartialData, "Researched civics are completed Civics nodes: their knowledge holds. No civic or government form is adopted before M8. Universities are simulated: one is founded when its building is completed in a town that can sustain it (see INSTITUTIONS); other institutions are knowledge eligibility only.",
                    [new LensSection("Researched civics", civics.Count + " of " + content.CivicsCount, civics, civicSubjects),
                     .. InstitutionSections(world, cfg, polity),
                     Eligible("Institutions within reach of knowledge", "knowledge eligibility only", world, content, polity, ResearchEntityKind.Institution)]);
            }
            case Lens.Infrastructure:
                return new LensPage(lens, "Infrastructure", "Roads, works and networks.",
                    LensStatus.PartialData, "Roads are simulated: routes are built and modernized class by class through the road-development order, from real materials. Listed here: the infrastructure your knowledge now allows.",
                    [Eligible("Infrastructure within reach of knowledge", "", world, content, polity, ResearchEntityKind.Infrastructure)]);
            case Lens.Military:
                return new LensPage(lens, "Military", "Arms, units and the art of war.",
                    LensStatus.PartialData, "Knowledge eligibility only - recruitment and battle arrive with the Battle Layer (M7).",
                    [Eligible("Units within reach of knowledge", "", world, content, polity, ResearchEntityKind.Unit)]);
            case Lens.Applications:
            {
                var apps = new List<string>();
                for (int i = 0; i < completed.Length; i++)
                    if (completed[i]) foreach (string a in content.Nodes[i].Applications) apps.Add(a);
                return new LensPage(lens, "Applications", "What knowledge is used for.",
                    LensStatus.PartialData, "Declared by completed nodes; realised by their owning systems.",
                    [new LensSection("Applications", "", apps),
                     new LensSection("Capabilities unlocked", "", ResearchQuery.UnlockedCapabilities(world, content, polity))]);
            }
            default:
                return new LensPage(lens, "Industry", "Workshops, manufactories and production at scale.",
                    LensStatus.NotYetSimulated, "Crafting is simulated as a labour sector turning inputs into goods by recipe (see the Work of each settlement); workshops, manufactories and industrial production at scale are not yet simulated, so this lens lists nothing.", []);
        }
    }

    /// <summary>ADR-057 (P5/P6): the institution types (each founded by a project: universities and the law court) with their
    /// stage and locked reason; the regime modules with the derived label; the government forms. Read through
    /// InstitutionsQuery and RegimeQuery — nothing here is computed twice.</summary>
    private static IEnumerable<LensSection> InstitutionSections(IReadOnlyWorldState world, SimConfig? cfg, PolityId polity)
    {
        if (cfg is null) yield break;
        var types = new List<string>();
        var typeSubjects = new List<InfoSubject?>();
        foreach (InstitutionTypeInfo t in InstitutionContent.Types(cfg))
            if (InstitutionsQuery.TypeReading(world, cfg, polity, t.Key) is { } r)
            {
                types.Add(r.Name + ": " + r.Line);
                typeSubjects.Add(InfoSubject.OfUniversity(t.Key));
            }
        if (types.Count > 0)
            yield return new LensSection("Institutions", "Unresearched -> Available -> Active (founded by construction)", types, typeSubjects);
        if (cfg.Regime is null) yield break;
        var modules = new List<string>();
        foreach (RegimeModuleView m in RegimeQuery.Modules(world, cfg, polity)) modules.Add(m.Name + ": " + m.Reason);
        modules.Add("Government label: " + RegimeQuery.Label(world, cfg, polity) + " (derived from the modules; it changes nothing)");
        yield return new LensSection("Regime modules", "derived from what exists; nothing is adopted before M8", modules);
        var forms = new List<string>();
        var formSubjects = new List<InfoSubject?>();
        foreach (GovernmentFormView f in RegimeQuery.Forms(world, cfg, polity))
        {
            forms.Add(f.Name + ": " + f.Line);
            formSubjects.Add(InfoSubject.Node(f.Key));
        }
        int researched = 0;
        foreach (GovernmentFormView f in RegimeQuery.Forms(world, cfg, polity)) if (f.State == CivicState.Researched) researched++;
        yield return new LensSection("Government forms", researched + " of " + forms.Count + " researched - adoption in M8", forms, formSubjects);
    }

    private static LensSection Eligible(string heading, string note, IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchEntityKind kind)
    {
        string[] ids = ResearchQuery.KnowledgeEligibleEntities(world, content, polity);
        var result = new List<string>();
        var subjects = new List<InfoSubject?>();
        foreach (string id in ids)
        {
            int e = content.EntityIndexOf(id);
            if (e < 0 || content.Entities[e].Kind != kind) continue;
            result.Add(content.Entities[e].Name ?? id);
            subjects.Add(InfoSubject.OfEntity(id));
        }
        return new LensSection(heading, note, result, subjects);
    }
}

using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>The seven lenses of the Director ledger §3, in ledger order. Technology and Civics
/// both live under <see cref="KnowledgeAndTechnology"/> (ruling 11).</summary>
public enum Lens { KnowledgeAndTechnology = 0, Techniques, Institutions, Infrastructure, Industry, Military, Applications }

/// <summary>How much of a lens the simulation actually backs today.</summary>
public enum LensStatus { Functional, PartialData, NotYetSimulated }

public sealed record LensSection(string Heading, string Note, IReadOnlyList<string> Items);

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

    public static LensPage Page(Lens lens, IReadOnlyWorldState world, ResearchContent content, PolityId polity)
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
                for (int i = 0; i < completed.Length; i++)
                    if (completed[i]) foreach (string t in content.Nodes[i].Techniques) items.Add(t + "  -  " + content.Nodes[i].Name);
                return new LensPage(lens, "Techniques", "Practices the completed knowledge makes possible.",
                    LensStatus.PartialData, "Listed from completed research. Technique adoption and diffusion are not yet simulated.",
                    [new LensSection("Known techniques", items.Count + " from completed nodes", items)]);
            }
            case Lens.Institutions:
            {
                var civics = new List<string>();
                for (int i = 0; i < completed.Length; i++)
                    if (completed[i] && content.Nodes[i].Tree == ResearchTree.Civics) civics.Add(content.Nodes[i].Name);
                return new LensPage(lens, "Institutions", "How the society is organised: adopted civics and the institutions they allow.",
                    LensStatus.PartialData, "Adopted civics are completed Civics nodes. Establishing an institution belongs to its owning system, not yet simulated.",
                    [new LensSection("Adopted civics", civics.Count + " of " + content.CivicsCount, civics),
                     new LensSection("Institutions within reach of knowledge", "knowledge eligibility only", Eligible(world, content, polity, ResearchEntityKind.Institution))]);
            }
            case Lens.Infrastructure:
                return new LensPage(lens, "Infrastructure", "Roads, works and networks.",
                    LensStatus.PartialData, "Knowledge eligibility only - construction of these is not yet simulated.",
                    [new LensSection("Infrastructure within reach of knowledge", "", Eligible(world, content, polity, ResearchEntityKind.Infrastructure))]);
            case Lens.Military:
                return new LensPage(lens, "Military", "Arms, units and the art of war.",
                    LensStatus.PartialData, "Knowledge eligibility only - recruitment and battle are not yet simulated.",
                    [new LensSection("Units within reach of knowledge", "", Eligible(world, content, polity, ResearchEntityKind.Unit))]);
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
                    LensStatus.NotYetSimulated, "Not yet simulated. No system produces industry state yet, so this lens has nothing true to show.", []);
        }
    }

    private static List<string> Eligible(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchEntityKind kind)
    {
        string[] ids = ResearchQuery.KnowledgeEligibleEntities(world, content, polity);
        var result = new List<string>();
        foreach (string id in ids)
        {
            int e = content.EntityIndexOf(id);
            if (e >= 0 && content.Entities[e].Kind == kind) result.Add(content.Entities[e].Name ?? id);
        }
        return result;
    }
}

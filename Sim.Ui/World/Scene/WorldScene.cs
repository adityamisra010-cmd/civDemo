using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>
/// THE SCENE — projects a <see cref="WorldView"/> through a camera into one frame: it picks
/// the level of detail, computes the shared screen geometry once, asks each per-kind painter
/// for its layer, and concatenates the layers in the fixed <see cref="WorldLayer"/> order.
/// It paints nothing itself and decides nothing about the world. Pure: the same view,
/// content, camera and UI state give the same commands, every time.
/// </summary>
public static class WorldScene
{
    public static WorldFrame Paint(
        WorldView view, WorldMorphology morph, WorldProjection proj, WorldUiState ui, WorldSceneOptions options,
        ITextMeasure measure, string? stepLabel = null, WorldBackdrop? backdrop = null)
    {
        double ppu = morph.Sprite.PxPerUnit(proj.PxPerWorldUnit);
        WorldLod lod = morph.Lod.For(ppu);
        var geometry = new SceneGeometry(view, morph, proj, lod, ppu);
        var c = new SceneContext(view, morph, proj, lod, ppu, ui, options, measure, geometry, stepLabel);
        var hits = new HitIndex();

        var layers = new (WorldLayer Layer, DrawList List)[9];
        for (int i = 0; i < layers.Length; i++) layers[i] = ((WorldLayer)i, new DrawList());

        if (options.DrawBackground && backdrop is not null) BackgroundPainter.Paint(c, layers[(int)WorldLayer.Background].List, backdrop);
        FootprintPainter.Paint(c, layers[(int)WorldLayer.SettlementFootprint].List, hits);
        InfrastructurePainter.Paint(c, layers[(int)WorldLayer.Infrastructure].List, hits);
        BuildingPainter.Paint(c, layers[(int)WorldLayer.Buildings].List, hits);
        ResourcePainter.Paint(c, layers[(int)WorldLayer.Resources].List, hits);
        AgentPainter.Paint(c, layers[(int)WorldLayer.MobileAgents].List, hits);
        LabelPainter.Paint(c, layers[(int)WorldLayer.Labels].List);
        SelectionPainter.Paint(c, layers[(int)WorldLayer.Selection].List);
        PanelPainter.Paint(c, layers[(int)WorldLayer.TransientUi].List);

        var frame = new DrawList();
        var spans = new List<LayerSpan>(layers.Length);
        foreach ((WorldLayer layer, DrawList list) in layers)
        {
            spans.Add(new LayerSpan(layer, frame.Commands.Count, list.Commands.Count));
            foreach (DrawCmd cmd in list.Commands) frame.Add(cmd);
        }
        return new WorldFrame(frame, spans, hits, lod, ppu);
    }
}

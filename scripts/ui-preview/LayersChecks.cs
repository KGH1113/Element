using System.Collections.Generic;
using System.Linq;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.ElementBar;
using Element.Presentation.OverlayEditor.Canvas.Layers;
using Element.Presentation.Ui.Popovers;
using UnityEngine;

namespace Element.Editor
{
  // The layers panel above the toolbar: cards front-most first, groups as
  // cards that open, picking, carrying a card to a new place, grouping,
  // renaming and moving forward and back, each one undo step.
  public static partial class SettingsPreview
  {
    private static void CheckLayers(
      OverlayEditorScreen editor,
      OverlayCanvasHost host,
      Camera camera,
      RenderTexture texture,
      Canvas[] canvases,
      string name
    )
    {
      List<OverlayLayoutSettings> layouts = host.LayoutSettings;
      CanvasSelection selection = Selection(editor);
      ICanvasActions canvas = editor;
      selection.Clear();
      Assert(
        layouts.Where(l => l.Id.StartsWith("progress-bar")).All(l => l.Group == OverlayElementDefaults.ProgressBarGroup)
          && layouts.Single(l => l.Id == OverlayElementDefaults.ComboId).Group == OverlayElementDefaults.ComboGroup,
        "the progress bar and the combo start as groups"
      );
      Assert(
        LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          < LayoutLayers.IndexOf(layouts, "song-title"),
        "the progress bar draws behind the texts, as before layers"
      );

      // Opening: above the toolbar from its Order button, the button lit.
      canvas.Layers();
      Call(editor, "UpdateFrames");
      Settle();
      LayersPanel panel = Field<LayersPanel>(editor, "_layersPanel");
      Assert(panel.Visible && ToolbarModel(editor).LayersOpen.Value, "Order opens the layers panel and stays lit");
      Call(editor, "UpdateFrames");
      Settle();
      LayersViewModel layers = panel.ViewModel;
      LayerRowViewModel[] rows = layers.Rows;
      int bar = System.Array.FindIndex(rows, row => row.Header && row.Group == OverlayElementDefaults.ProgressBarGroup);
      int combo = System.Array.FindIndex(rows, row => row.Header && row.Group == OverlayElementDefaults.ComboGroup);
      int title = System.Array.FindIndex(rows, row => row.Id == "song-title");
      Assert(
        bar >= 0 && combo >= 0 && title >= 0 && bar > title && rows[bar].Count == "3",
        "a card per element, front-most first: the progress bar's group card, with 3, below the texts"
      );
      Assert(
        rows[bar].Name == L("진행도 바", "Progress bar") && rows[combo].Name == L("콤보", "Combo"),
        "built-in groups are named in the player's language"
      );
      SaveFrame(camera, texture, canvases, name + "-layers");
      Assert(PopoverInside(editor, panel.Root, Screen.width, Screen.height), "the layers panel stays on screen");
      Rect panelRect = PopoverPlacement.ScreenRect(panel.Root);
      Assert(
        panelRect.yMax <= editor.ToolbarBounds.y + .5f || panelRect.y >= editor.ToolbarBounds.yMax - .5f,
        "the layers panel opens above the toolbar, or below it without room: "
          + panelRect
          + " / "
          + editor.ToolbarBounds
      );
      RectTransform titleCard = ByName<RectTransform>(panel.Root, rows[title].Key);
      Assert(
        titleCard != null
          && titleCard.GetComponentsInChildren<Transform>(true).Any(child => child.name.StartsWith("Snapshot:")),
        "a card shows the element as it looks, small"
      );

      // A card picks what it stands for: a group's card, the whole group.
      layers.Pick(rows[combo], false);
      Call(editor, "UpdateFrames");
      Settle();
      Assert(
        selection.Group == OverlayElementDefaults.ComboGroup && selection.Count == 2,
        "a group's card picks the whole group"
      );
      Assert(
        ElementBar(editor).Model is ArrangeViewModel arrange && arrange.GroupName.Value == L("콤보", "Combo"),
        "a picked group shows its name and Ungroup on the element toolbar"
      );
      SaveFrame(camera, texture, canvases, name + "-layers-group");

      // On the canvas: a press picks the whole group, a second press the element in it.
      IOverlayLayoutTarget comboText = host.LayoutTargets.Single(target => target.Id == OverlayElementDefaults.ComboId);
      selection.Clear();
      SetField(Drag(editor), "_lastClickTime", -10f);
      Call(editor, "PressElement", IndexOf(editor, comboText), LayoutHandle.Move, comboText.Bounds.center);
      Drag(editor).Release();
      Assert(selection.Group == OverlayElementDefaults.ComboGroup, "a press on a grouped element takes its group");
      SetField(Drag(editor), "_lastClickTime", -10f);
      Call(editor, "PressElement", IndexOf(editor, comboText), LayoutHandle.Move, comboText.Bounds.center);
      Drag(editor).Release();
      Assert(selection.Count == 1 && selection.Primary == comboText, "a second press picks the element inside");
      Call(editor, "UpdateFrames");
      Settle();
      rows = layers.Rows;
      Assert(
        rows.Any(row => row.Child && row.Id == OverlayElementDefaults.ComboId && row.Selected),
        "picking an element inside a group opens the group's card and marks the element"
      );
      SaveFrame(camera, texture, canvases, name + "-layers-open");

      // Carrying the progress bar's card to the top brings it in front, as one undo step.
      rows = layers.Rows;
      LayerRowViewModel barCard = rows.Single(row =>
        row.Header && row.Group == OverlayElementDefaults.ProgressBarGroup
      );
      LayerDrag carried = layers.DragOf(barCard);
      layers.Over(carried, 0);
      Settle();
      Assert(
        LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          > LayoutLayers.IndexOf(layouts, layers.Rows[0].Id ?? "song-title"),
        "while carried, the canvas shows the new order: fill "
          + LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          + " of "
          + layouts.Count
          + ", first card "
          + layers.Rows[0].Key
          + " at "
          + LayoutLayers.IndexOf(layouts, layers.Rows[0].Id ?? "")
          + ", carried "
          + string.Join(",", carried.Ids)
      );
      Assert(layers.Rows[0].InsertAbove, "a line shows where the card lands");
      SaveFrame(camera, texture, canvases, name + "-layers-drag");
      layers.Leave();
      Assert(
        LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          < LayoutLayers.IndexOf(layouts, "song-title"),
        "letting it go outside brings the order back"
      );
      int steps = Session(editor).History.Version;
      layers.Over(carried, 0);
      layers.Drop(carried, 0);
      Assert(
        LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          > LayoutLayers.IndexOf(layouts, "song-title")
          && Session(editor).History.Version > steps,
        "letting it go on top brings the group in front, one undo step: fill "
          + LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          + ", title "
          + LayoutLayers.IndexOf(layouts, "song-title")
          + ", history "
          + steps
          + " -> "
          + Session(editor).History.Version
      );
      Assert(
        layouts
          .Where(l => l.Group == OverlayElementDefaults.ProgressBarGroup)
          .Select(l => LayoutLayers.IndexOf(layouts, l.Id))
          .OrderBy(i => i)
          .Aggregate((ok: true, last: -2), (s, i) => (s.ok && (s.last < 0 || i == s.last + 1), i))
          .ok,
        "a group's elements stay together"
      );
      Call(editor, "Undo");
      Assert(
        LayoutLayers.IndexOf(layouts, OverlayElementDefaults.ProgressFillId)
          < LayoutLayers.IndexOf(layouts, "song-title"),
        "undo puts it back behind"
      );

      // Grouping two texts, naming the group, and taking it apart again.
      List<IOverlayLayoutTarget> texts = host
        .LayoutTargets.OfType<TextOverlayElement>()
        .Where(t => t.Available && t.Enabled && string.IsNullOrEmpty(LayoutGroups.Of(layouts, t.Id)))
        .Cast<IOverlayLayoutTarget>()
        .Take(2)
        .ToList();
      selection.SelectOnly(IndexOf(editor, texts[0]));
      selection.Toggle(IndexOf(editor, texts[1]));
      canvas.Group();
      Call(editor, "UpdateFrames");
      Settle();
      string made = selection.Group;
      Assert(
        made != null && LayoutGroups.Members(layouts, made).Count == 2,
        "Group makes the two one group, still picked"
      );
      LayerRowViewModel madeCard = layers.Rows.Single(row => row.Header && row.Group == made);
      Assert(madeCard.Name == L("묶음 1", "Group 1"), "a new group is named Group 1: " + madeCard.Name);
      layers.StartRename(madeCard);
      layers.RenameText = "Score";
      layers.EndRename(false);
      Assert(GroupNames.Of(layouts, made) == "Score", "a group's card takes a new name");
      Call(editor, "UpdateFrames");
      Settle();
      SaveFrame(camera, texture, canvases, name + "-layers-new-group");
      canvas.Ungroup();
      Assert(LayoutGroups.Members(layouts, made).Count == 0 && selection.Count == 2, "Ungroup keeps both, picked");
      Call(editor, "Undo");
      Assert(LayoutGroups.Members(layouts, made).Count == 2, "undo brings the group back");
      Call(editor, "Undo");
      Call(editor, "Undo");
      Assert(LayoutGroups.Members(layouts, made).Count == 0, "and before it, the texts stood alone");

      // Forward and back only past what overlaps: a lone text far from others says so.
      selection.SelectOnly(IndexOf(editor, host.LayoutTargets.Single(t => t.Id == "fps")));
      canvas.Arrange(true, false);
      Assert(
        Status(editor).Text.Value == L("겹친 것 중 이미 맨 앞이에요.", "Already in front of what it overlaps."),
        "forward with nothing overlapping in front says so: " + Status(editor).Text.Value
      );

      canvas.Layers();
      Call(editor, "UpdateFrames");
      Assert(!panel.Visible && !ToolbarModel(editor).LayersOpen.Value, "Order closes the panel again");
      selection.Clear();
      Call(editor, "UpdateFrames");
    }
  }
}

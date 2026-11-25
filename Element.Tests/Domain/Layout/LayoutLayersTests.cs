using System.Collections.Generic;
using System.Linq;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Layout;

internal static class LayoutLayersTests
{
  [Suite("the layout list is the drawing order, and groups stay together in it")]
  public static void Run()
  {
    OldLayoutsDrawAsBefore();
    PlacesAndSteps();
    GroupsStayTogether();
    UndoKeepsTheOrder();
  }

  private static OverlayLayoutSettings Item(string id, string template = OverlayElementKinds.Text, string group = "") =>
    new()
    {
      Id = id,
      TemplateId = template,
      Enabled = true,
      Group = group,
    };

  private static string Order(List<OverlayLayoutSettings> layouts) => string.Join(",", layouts.Select(l => l.Id));

  private static void OldLayoutsDrawAsBefore()
  {
    ElementSettings settings = new()
    {
      SettingsVersion = 9,
      OverlayLayouts = new()
      {
        Item("hand", OverlayElementKinds.HandKeyViewer),
        Item("title"),
        Item("box", OverlayElementKinds.Shape),
        Item("bars", "Sample/bars"),
        Item(OverlayElementDefaults.ComboId),
        Item("fps"),
        Item(OverlayElementDefaults.ComboTitleId),
        Item(OverlayElementDefaults.ProgressBarId, OverlayElementKinds.Shape),
        Item(OverlayElementDefaults.ProgressTrackId, OverlayElementKinds.Shape),
        Item(OverlayElementDefaults.ProgressFillId, OverlayElementKinds.Shape),
      },
    };
    SettingsMigrator.Migrate(settings);
    List<OverlayLayoutSettings> layouts = settings.OverlayLayouts;
    Check(
      Order(layouts) == "box,progress-bar,progress-bar-track,progress-bar-fill,bars,title,fps,combo,combo-title,hand",
      "a layout saved before layers draws as it did: shapes, other mods' elements, texts, key viewers: "
        + Order(layouts)
    );
    Check(
      layouts.Where(l => l.Id.StartsWith("progress-bar")).All(l => l.Group == OverlayElementDefaults.ProgressBarGroup)
        && layouts.Find(l => l.Id == "combo").Group == OverlayElementDefaults.ComboGroup
        && layouts.Find(l => l.Id == "combo-title").Group == OverlayElementDefaults.ComboGroup
        && layouts.Find(l => l.Id == "fps").Group == "",
      "the progress bar and the combo start as groups; the rest stand alone"
    );
    Check(settings.SettingsVersion == ElementSettings.CurrentSettingsVersion, "the settings reach the current version");

    // A second load keeps the player's own order.
    layouts.Reverse();
    string mine = Order(layouts);
    SettingsMigrator.Migrate(settings);
    Check(Order(settings.OverlayLayouts) == mine, "a layout already in layers keeps the order it was given");

    // A new install starts with the bar at the back, as one group.
    ElementSettings fresh = new();
    SettingsMigrator.Migrate(fresh);
    int bar = fresh.OverlayLayouts.FindIndex(l => l.Id == OverlayElementDefaults.ProgressFillId);
    int text = fresh.OverlayLayouts.FindIndex(l => l.Id == "song-title");
    Check(bar >= 0 && text > bar, "a new install draws the progress bar behind the texts");
  }

  private static void PlacesAndSteps()
  {
    List<OverlayLayoutSettings> layouts = new() { Item("a"), Item("b"), Item("c"), Item("d") };
    LayoutLayers.PlaceInFrontOf(layouts, new[] { "a" }, "c");
    Check(Order(layouts) == "b,c,a,d", "a card let go between two goes just in front of the one below");
    LayoutLayers.PlaceInFrontOf(layouts, new[] { "d" }, null);
    Check(Order(layouts) == "d,b,c,a", "null puts it at the very back");

    // Only overlapping elements count: each step shows a change.
    layouts = new() { Item("a"), Item("b"), Item("c"), Item("d") };
    HashSet<string> overlapping = new() { "d" };
    Check(
      LayoutLayers.Step(layouts, new[] { "a" }, true, overlapping.Contains),
      "a step forward finds the overlapping one"
    );
    Check(Order(layouts) == "b,c,d,a", "it passes everything up to the next element it overlaps: " + Order(layouts));
    Check(
      !LayoutLayers.Step(layouts, new[] { "a" }, true, overlapping.Contains),
      "nothing overlapping in front: no step"
    );
    Check(LayoutLayers.ToEnd(layouts, new[] { "a" }, false) && Order(layouts) == "a,b,c,d", "to the very back");

    // One place up passes the next shown element; hidden ones don't count.
    layouts = new() { Item("a"), Item("hidden"), Item("b") };
    layouts[1].Enabled = false;
    Check(LayoutLayers.Shift(layouts, new[] { "a" }, 1) && Order(layouts) == "hidden,b,a", "one place up passes the next shown element: " + Order(layouts));
    Check(!LayoutLayers.Shift(layouts, new[] { "a" }, 1), "already in front: no place to go");

    // Inside a group, a step stays among its elements.
    layouts = new() { Item("x"), Item("g1", group: "g"), Item("g2", group: "g"), Item("g3", group: "g"), Item("y") };
    Check(
      LayoutLayers.ToEnd(layouts, new[] { "g1" }, true) && Order(layouts) == "x,g2,g3,g1,y",
      "an element inside a group goes to the front of its group, not of everything"
    );
    Check(
      LayoutLayers.ToEnd(layouts, new[] { "g2", "g3", "g1" }, false) && Order(layouts) == "g2,g3,g1,x,y",
      "a whole group moves as one"
    );
  }

  private static void GroupsStayTogether()
  {
    List<OverlayLayoutSettings> layouts = new() { Item("a"), Item("b"), Item("c"), Item("d") };
    string group = LayoutGroups.Make(layouts, new[] { "a", "c" }, "Group 1");
    Check(group != null && Order(layouts) == "b,a,c,d", "a new group gathers where its front-most element is");
    Check(
      layouts[1].Group == group && layouts[2].GroupName == "Group 1",
      "each element knows its group and the group's name"
    );
    Check(LayoutGroups.Make(layouts, new[] { "a" }, "x") == null, "one element is no group");

    LayoutGroups.Rename(layouts, group, "Bar");
    Check(
      LayoutGroups.Members(layouts, group).Count == 2 && layouts[1].GroupName == "Bar",
      "renaming names every element"
    );

    // Deleting one of two leaves no group.
    layouts[1].Enabled = false;
    LayoutGroups.DropLonely(layouts);
    Check(layouts[2].Group == "" && layouts[1].Group == "", "a group left with one shown element is dropped");

    layouts = new() { Item("a", group: "g"), Item("b"), Item("c", group: "g") };
    LayoutGroups.KeepTogether(layouts);
    Check(Order(layouts) == "b,a,c", "split groups gather at their front-most element");

    LayoutGroups.Assign(layouts, "b", "g");
    LayoutGroups.KeepTogether(layouts);
    Check(LayoutGroups.Members(layouts, "g").Count == 3, "an element let go inside a group joins it");
    LayoutGroups.Dissolve(layouts, "g");
    Check(layouts.All(l => l.Group == ""), "ungrouping keeps every element and its place");
  }

  private static void UndoKeepsTheOrder()
  {
    List<OverlayLayoutSettings> layouts = new() { Item("a"), Item("b"), Item("c") };
    LayoutHistory history = new();
    history.Reset(layouts);
    LayoutLayers.PlaceInFrontOf(layouts, new[] { "a" }, "c");
    LayoutGroups.Make(layouts, new[] { "b", "c" }, "G");
    history.Commit(layouts);
    Check(history.CanUndo, "a new order or group is an undo step");
    history.Undo(layouts);
    Check(Order(layouts) == "a,b,c" && layouts.All(l => l.Group == ""), "undo brings back the order and the groups");
    history.Redo(layouts);
    Check(layouts[2].Group == "" && layouts[0].Group != "" && Order(layouts) == "b,c,a", "redo applies them again");
  }
}

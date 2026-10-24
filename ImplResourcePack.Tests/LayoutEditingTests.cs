using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Layout;

namespace ImplResourcePack.Tests;

internal static class LayoutEditingTests
{
  public static void Run()
  {
    XmlSerializer serializer = new(typeof(OverlayLayoutSettings));
    using StringReader legacyXml = new("<OverlayLayoutSettings><Id>fps</Id><Scale>1.5</Scale></OverlayLayoutSettings>");
    OverlayLayoutSettings legacy = (OverlayLayoutSettings)serializer.Deserialize(legacyXml);
    Check(legacy.Enabled && legacy.Scale == 1.5f, "older XML settings without Enabled preserve visible elements");
    legacy.Enabled = false;
    using StringWriter savedXml = new();
    serializer.Serialize(savedXml, legacy);
    using StringReader restoredXml = new(savedXml.ToString());
    Check(
      !((OverlayLayoutSettings)serializer.Deserialize(restoredXml)).Enabled,
      "deleted elements remain deleted after a save roundtrip"
    );

    List<OverlayLayoutSettings> settings = new()
    {
      new() { Id = "progress" },
      new() { Id = "dmnote-hand" },
    };
    LayoutHistory history = new();
    history.Reset(settings);
    Check(settings[0].Enabled && settings[1].Enabled, "new and migrated layout entries are enabled by default");
    settings[0].Customized = true;
    settings[0].CenterX = .3f;
    settings[0].Scale = 2f;
    history.Commit(settings);
    history.Commit(settings);
    history.Undo(settings);
    Check(
      !settings[0].Customized && settings[0].Scale == 1 && !history.CanUndo,
      "one gesture is one undo step; no-op commits are ignored"
    );
    history.Redo(settings);
    Check(
      settings[0].Customized && settings[0].CenterX == .3f && settings[0].Scale == 2,
      "normalized state survives redo independently of viewport"
    );
    settings[1].Customized = true;
    settings[1].CenterY = .8f;
    history.Commit(settings);
    history.Undo(settings);
    Check(!settings[1].Customized && settings[0].Scale == 2, "native and Unity viewers share history");
    settings[0].CenterX = .7f;
    history.Commit(settings);
    Check(!history.CanRedo, "editing after undo discards obsolete redo branch");
    LayoutSnapshot snapshot = new(settings);
    settings[0].Scale = 4;
    snapshot.Restore(settings);
    Check(settings[0].Scale == 2, "snapshots do not alias mutable settings");

    history.Reset(settings);
    settings[0].Enabled = false;
    settings[1].Enabled = false;
    history.Commit(settings);
    history.Undo(settings);
    Check(settings[0].Enabled && settings[1].Enabled, "undo deletion restores both Unity and native elements");
    history.Redo(settings);
    Check(!settings[0].Enabled && !settings[1].Enabled, "redo deletion restores the disabled state");
    LayoutSnapshot deleted = new(settings);
    settings[1].Enabled = true;
    history.Commit(settings);
    history.Undo(settings);
    Check(!settings[1].Enabled, "adding an element is an undoable change");
    settings[0].Enabled = settings[1].Enabled = true;
    deleted.Restore(settings);
    Check(!settings[0].Enabled && !settings[1].Enabled, "cancel restores element membership with layout");

    history.Reset(settings);
    LayoutSnapshot beforeAdd = new(settings);
    OverlayLayoutSettings copy = new()
    {
      Id = "progress:copy",
      TemplateId = "progress",
      CenterX = .25f,
    };
    settings.Add(copy);
    history.Commit(settings);
    history.Undo(settings);
    Check(!copy.Enabled && !settings[0].Enabled, "undoing a new instance disables only that instance");
    history.Commit(settings);
    Check(history.CanRedo, "a disabled post-snapshot instance does not create a no-op history branch");
    history.Redo(settings);
    Check(
      copy.Enabled && copy.TemplateId == "progress" && copy.CenterX == .25f,
      "redo restores a new instance and its independent placement"
    );
    OverlayLayoutSettings secondCopy = new() { Id = "progress:second", TemplateId = "progress" };
    settings.Add(secondCopy);
    beforeAdd.Restore(settings);
    Check(!copy.Enabled && !secondCopy.Enabled, "cancel disables all instances created after opening the editor");

    List<LayoutBounds> others = new() { new(300, 220, 100, 50) };
    LayoutBounds near = LayoutSnap.Move(
      new LayoutBounds(197, 169, 100, 50),
      others,
      1000,
      600,
      7,
      out float x,
      out float y
    );
    Check(near.X == 200 && near.Y == 170 && x == 300 && y == 220, "nearest overlay edges snap with guide coordinates");
    LayoutBounds center = LayoutSnap.Move(
      new LayoutBounds(448, 251, 100, 100),
      Array.Empty<LayoutBounds>(),
      1000,
      600,
      7,
      out x,
      out y
    );
    Check(center.X == 450 && center.Y == 250 && x == 500 && y == 300, "screen center snapping");
    LayoutBounds free = LayoutSnap.Move(
      new LayoutBounds(120, 140, 100, 100),
      Array.Empty<LayoutBounds>(),
      1000,
      600,
      7,
      out x,
      out y
    );
    Check(
      free.X == 120 && free.Y == 140 && float.IsNaN(x) && float.IsNaN(y),
      "outside tolerance stays one-to-one with the pointer"
    );
    CheckEqualSpacing();
    CheckAnchoredHistory();
    CheckFactoryReset();
  }

  private static void CheckFactoryReset()
  {
    List<OverlayLayoutSettings> values = new()
    {
      new()
      {
        Id = "fps",
        Enabled = false,
        Customized = true,
        Scale = 3,
        LayoutVersion = 1,
        Anchor = Anchor9.Right,
        OffsetX = 42,
      },
      new()
      {
        Id = "dmnote-hand",
        Enabled = false,
        CenterY = .8f,
      },
      new()
      {
        Id = "fps:copy",
        TemplateId = "fps",
        Customized = true,
        Scale = 2,
      },
    };
    LayoutSnapshot before = new(values);
    LayoutHistory history = new();
    history.Reset(values);
    OverlayLayoutDefaults.Restore(values, new HashSet<string> { "fps", "dmnote-hand" });
    history.Commit(values);
    Check(
      values[0].Enabled && values[1].Enabled && !values[2].Enabled,
      "factory reset restores deleted defaults and removes added instances"
    );
    foreach (var value in values)
      Check(
        !value.Customized
          && value.Scale == 1
          && value.LayoutVersion == 0
          && value.Anchor == Anchor9.Center
          && value.OffsetX == 0
          && value.OffsetY == 0
          && value.CenterX == .5f
          && value.CenterY == .5f,
        "factory reset clears all legacy and anchored placement fields"
      );
    history.Undo(values);
    Check(
      before.Matches(new LayoutSnapshot(values)),
      "one undo restores the complete pre-reset composition and placement"
    );
    history.Redo(values);
    Check(values[0].Enabled && !values[2].Enabled, "redo reapplies the complete reset");
    before.Restore(values);
    Check(before.Matches(new LayoutSnapshot(values)), "cancel recovers the exact pre-reset layout");
  }

  private static void CheckAnchoredHistory()
  {
    OverlayLayoutSettings element = new()
    {
      Id = "song-title",
      Customized = true,
      CenterX = .8f,
      CenterY = .9f,
      Scale = 1.25f,
    };
    List<OverlayLayoutSettings> values = new() { element };
    LayoutSnapshot opening = new(values);
    LayoutHistory history = new();
    history.Reset(values);
    // A first explicit edit creates the new format. Undo must restore the old
    // format itself, not merely a visually similar location on today's screen.
    element.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
    element.Anchor = Anchor9.BottomRight;
    element.OffsetX = -40;
    element.OffsetY = -24;
    history.Commit(values);
    history.Undo(values);
    Check(
      element.LayoutVersion == 0 && element.CenterX == .8f && element.Scale == 1.25f,
      "undo first anchored edit restores the untouched legacy placement"
    );
    history.Redo(values);
    Check(
      element.LayoutVersion == 1
        && element.Anchor == Anchor9.BottomRight
        && element.OffsetX == -40
        && element.OffsetY == -24,
      "redo restores anchor and both logical margins"
    );

    // Anchor and offsets each independently identify a history state, including
    // changes which leave legacy centers and individual scale untouched.
    element.Anchor = Anchor9.Bottom;
    history.Commit(values);
    element.OffsetX = 12;
    history.Commit(values);
    element.OffsetY = -48;
    history.Commit(values);
    history.Undo(values);
    Check(element.OffsetY == -24 && element.OffsetX == 12, "vertical offset edits participate in history");
    history.Undo(values);
    Check(element.OffsetX == -40 && element.Anchor == Anchor9.Bottom, "horizontal offset edits participate in history");
    history.Undo(values);
    Check(element.Anchor == Anchor9.BottomRight, "anchor-only edits participate in history");

    OverlayLayoutSettings copy = new()
    {
      Id = "song-title:copy",
      TemplateId = "song-title",
      Customized = true,
      LayoutVersion = 1,
      Anchor = Anchor9.Top,
      OffsetX = 9,
      OffsetY = 30,
      Scale = 8.5f,
    };
    values.Add(copy);
    history.Commit(values);
    history.Undo(values);
    Check(!copy.Enabled, "undo new anchored instance removes it from the rendered layout");
    copy.Anchor = Anchor9.Left;
    copy.OffsetY = 400;
    history.Redo(values);
    Check(
      copy.Enabled && copy.Anchor == Anchor9.Top && copy.OffsetX == 9 && copy.OffsetY == 30 && copy.Scale == 8.5f,
      "redo new instance restores its own complete placement without scale clamping"
    );
    copy.Enabled = false;
    history.Commit(values);
    history.Undo(values);
    Check(copy.Enabled && copy.OffsetY == 30, "deletion preserves anchored placement for undo");

    element.Customized = false;
    element.LayoutVersion = 0;
    element.Anchor = Anchor9.Center;
    element.OffsetX = element.OffsetY = 0;
    history.Commit(values);
    history.Undo(values);
    Check(
      element.Customized
        && element.LayoutVersion == 1
        && element.Anchor == Anchor9.BottomRight
        && element.OffsetX == -40
        && element.OffsetY == -24,
      "undo reset restores complete anchored placement"
    );
    opening.Restore(values);
    Check(
      element.LayoutVersion == 0
        && element.Anchor == Anchor9.Center
        && element.OffsetX == 0
        && element.OffsetY == 0
        && element.CenterX == .8f
        && !copy.Enabled,
      "cancel returns legacy metadata and disables all new instances"
    );

    // Saving and restarting preserves the history-restored persistent intent.
    history.Redo(values);
    history.Undo(values);
    XmlSerializer serializer = new(typeof(List<OverlayLayoutSettings>));
    using StringWriter output = new();
    serializer.Serialize(output, values);
    using StringReader input = new(output.ToString());
    var restarted = (List<OverlayLayoutSettings>)serializer.Deserialize(input);
    Check(
      new LayoutSnapshot(values).Matches(new LayoutSnapshot(restarted)),
      "save/restart preserves all placement and membership fields after undo"
    );
  }

  private static void CheckEqualSpacing()
  {
    List<LayoutBounds> row = new() { new(220, 100, 120, 40), new(100, 100, 80, 40) };
    float after = LayoutSpacingSnap.Find(new(377, 100, 60, 40), row, false, 1000, 7, out var guide);
    Check(after == 380 && guide.Visible, "extends an unordered row using edge gaps despite unequal widths");
    Check(
      guide.Start1 == 180 && guide.End1 == 220 && guide.Start2 == 340 && guide.End2 == 380,
      "guides measure the reference and new 40px gaps"
    );
    Check(
      LayoutSpacingSnap.Find(new(3, 100, 60, 40), row, false, 1000, 7, out guide) == 0 && guide.Visible,
      "equal spacing works before a row"
    );
    List<LayoutBounds> separated = new() { new(100, 100, 80, 40), new(340, 100, 120, 40) };
    Check(
      LayoutSpacingSnap.Find(new(233, 100, 60, 40), separated, false, 1000, 7, out guide) == 230,
      "centers a differently sized element in the empty gap"
    );
    Check(
      guide.End1 - guide.Start1 == 50 && guide.End2 - guide.Start2 == 50,
      "between-element guides describe equal edge gaps, not equal center distances"
    );
    Check(
      !guide.At(new(230, 200, 60, 40), false).Visible,
      "moving out of the common row clears an obsolete spacing guide"
    );
    List<LayoutBounds> column = new() { new(100, 100, 40, 80), new(100, 220, 40, 120) };
    Check(
      LayoutSpacingSnap.Find(new(100, 383, 40, 60), column, true, 1000, 7, out guide) == 380
        && guide.Visible
        && guide.Cross == 120,
      "vertical spacing uses the same edge-gap rules"
    );
    LayoutSpacingSnap.Find(new(390, 100, 60, 40), row, false, 1000, 7, out guide);
    Check(!guide.Visible, "spacing does not pull the pointer outside tolerance");
    LayoutSpacingSnap.Find(new(377, 200, 60, 40), row, false, 1000, 7, out guide);
    Check(!guide.Visible, "unrelated rows do not attract each other");
    row.Add(new(193, 100, 10, 40));
    LayoutSpacingSnap.Find(new(377, 100, 60, 40), row, false, 1000, 7, out guide);
    Check(!guide.Visible, "occupied reference gaps cannot produce misleading spacing guides");
    row.RemoveAt(2);
    LayoutBounds snapped = LayoutSnap.Move(
      new(377, 100, 60, 40),
      row,
      1000,
      600,
      7,
      out float x,
      out _,
      out guide,
      out _
    );
    Check(snapped.X == 380 && guide.Visible && float.IsNaN(x), "spacing integrates with ordinary alignment snapping");
    row.Add(new(400, 300, 20, 40));
    snapped = LayoutSnap.Move(new(376, 100, 50, 40), row, 1000, 600, 7, out x, out _, out guide, out _);
    Check(snapped.X == 375 && !guide.Visible && x == 400, "closer edge alignment wins over a spacing candidate");
    List<LayoutBounds> screenEdge = new() { new(900, 100, 20, 40), new(940, 100, 20, 40) };
    LayoutSpacingSnap.Find(new(977, 100, 300, 40), screenEdge, false, 1000, 7, out guide);
    Check(!guide.Visible, "spacing respects the minimum ten percent on-screen visibility");
    List<LayoutBounds> touching = new() { new(100, 100, 80, 40), new(180, 100, 120, 40) };
    LayoutSpacingSnap.Find(new(302, 100, 60, 40), touching, false, 1000, 7, out guide);
    Check(!guide.Visible, "touching elements do not create zero-length spacing markers");
  }

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}

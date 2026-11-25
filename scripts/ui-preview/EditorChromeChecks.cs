using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Formula;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.EditorBar;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.TextEditor;
using Element.Domain.Overlay.Values;
using ElementUI;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Theme;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Editor
{
  // The text and shape editors' chrome (the top line with Cancel and Done,
  // the bar, Grow from's popover, the input dock) is ElementUI screens on
  // panels the editor's frame makes: these reach their view models and the
  // controls they draw, found by the names the views give them.
  public static partial class SettingsPreview
  {
    private static EditorFramePanel EditorScreen(object editor) => Field<EditorFramePanel>(editor, "_frame");

    /// <summary>The text and shape editors' property column.</summary>
    private static InspectorPanel EditorInspector(object editor) => Field<InspectorPanel>(editor, "_inspector");

    // The text and shape editors have a property column; the mod editor keeps a bar.
    private static bool HasInspector(object editor) => editor.GetType().GetField("_inspector", Private) != null;

    /// <summary>Where an editor's properties are drawn: the text and shape editors' column, the mod editor's bar.</summary>
    private static RectTransform EditorBarRoot(object editor) =>
      HasInspector(editor) ? EditorInspector(editor).Root : Field<EditorBarPanel>(editor, "_bar").Root;

    /// <summary>The control a property's row (or the mod editor's bar button) is drawn as, once the screen caught up.</summary>
    private static Pressable BarButton(object editor, PropertyButtonViewModel button)
    {
      Settle();
      RectTransform rect = HasInspector(editor)
        ? EditorInspector(editor).RectOf(button)
        : Field<EditorBarPanel>(editor, "_bar").RectOf(button);
      return rect != null ? rect.GetComponent<Pressable>() : null;
    }

    /// <summary>
    /// A field picked from a fixed set (a font, a point) as the dock shows
    /// it: the column keeps its rows, the dock's line names the field
    /// (<paramref name="whose"/> <paramref name="name"/>) and shows its one
    /// choice, the list's place says it is picked on the right, and
    /// <paramref name="drawn"/> stands where the input method is, over Remove
    /// (which rests) and Apply, under "<paramref name="method"/>".
    /// </summary>
    private static void AssertInputMethod(object editor, RectTransform drawn, string method, string name, string whose)
    {
      Settle();
      InspectorPanel inspector = EditorInspector(editor);
      DockProbe dock = DockOf(editor);
      Assert(inspector.ViewModel.OpenTool == null && ByName<Pressable>(inspector.Root, "Inspector back") == null,
        "the column keeps its rows while " + name + " is open");
      LineViewModel line = dock.Vm.Line;
      Assert(line.Open && line.Choosing && line.Name == name && line.Whose == whose && line.ChoiceLabel.Length > 0
        && ByName<TextMeshProUGUI>(dock.Root, "Choice").text == line.ChoiceLabel,
        "the line names " + name + " and shows what it holds: " + line.Whose + " " + line.Name + " = " + line.ChoiceLabel);
      TextMeshProUGUI title = ByName<TextMeshProUGUI>(dock.Root, "Input method"), rest = ByName<TextMeshProUGUI>(dock.Root, "Values rest");
      Assert(title.text == method && !dock.Values.Listing.Value && rest != null && rest.text.Contains(L("오른쪽 입력기", "at the right")),
        "the place is named " + method + ", and the list says it is picked there: " + title.text + " / " + rest?.text);
      Pressable remove = DockKey(dock, "Key remove"), done = DockKey(dock, "Key done");
      Assert(!remove.IsInteractable() && done.IsInteractable(), name + " can be applied; nothing of it is removed");
      Rect toolRect = ScreenRectOf(drawn), doneRect = ScreenRectOf((RectTransform)done.transform),
        removeRect = ScreenRectOf((RectTransform)remove.transform), titleRect = ScreenRectOf(title.rectTransform);
      Assert(Mathf.Abs(toolRect.xMin - removeRect.xMin) < 1.5f && toolRect.xMax <= doneRect.xMax + 1.5f
        // The title's words start a little under the top of their row.
        && toolRect.yMin >= doneRect.yMax - 1.5f && toolRect.yMax <= titleRect.yMax + 4 && InsideScreen(toolRect),
        name + " stands where the input method is, over Remove and Apply: " + toolRect + " / " + doneRect + " / " + titleRect);
      Graphic toolSurface = drawn.GetComponent<Graphic>();
      Assert(toolSurface == null || toolSurface.color.a < .01f, name + " stands on the screen with no box around it");
    }

    /// <summary>Apply on a field picked from a fixed set: its input method closes and the dock rests.</summary>
    private static void ApplyChoice(object editor)
    {
      DockProbe dock = DockOf(editor);
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(!dock.Vm.Line.Choosing && dock.Vm.Choice.Value == DockMethod.Rest, "Apply closes the field's input method");
    }

    private static Rect BarButtonRect(object editor, PropertyButtonViewModel button) =>
      ScreenRectOf((RectTransform)BarButton(editor, button).transform);

    /// <summary>Clicks a bar button as the player does, and lets the screen catch up.</summary>
    private static void PressBar(object editor, PropertyButtonViewModel button)
    {
      Pressable drawn = BarButton(editor, button);
      Assert(drawn != null, "the bar draws " + button.Name);
      Press(drawn);
      Settle();
    }

    /// <summary>
    /// <paramref name="count"/> frames as the game runs them: the editor's
    /// own tick, then ElementUI's shared animation and layout clock at a
    /// deterministic 60 Hz. Like the game's driver, this flushes only while
    /// awake and goes to sleep once nothing is left: a change that doesn't
    /// wake the driver never shows.
    /// </summary>
    private static void Frames(System.Action tick, int count)
    {
      Driver driver = (Driver)typeof(Driver)
        .GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        .GetValue(null);
      for (int frame = 0; frame < count; frame++)
      {
        tick();
        if (driver != null && driver.enabled)
        {
          ElementUI.Runtime.Flush(1f / 60);
          driver.enabled = ElementUI.Runtime.HasWork;
        }
      }
      Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// A frame of the text editor with no pointer or key (the preview's Unity
    /// reads input through the Input System, which the editor doesn't use):
    /// the top line's notices and the dock's blinking caret.
    /// </summary>
    private static void QuietFrame(Element.Presentation.OverlayEditor.TextEditor.TextEditorScreen text)
    {
      EditorScreen(text).Tick();
      DockOf(text).Blink(true);
    }

    /// <summary>
    /// An editor's input dock as the checks see it: where it is drawn
    /// (<see cref="InputDockPanel"/>) and what it shows (<see cref="InputDockViewModel"/>).
    /// </summary>
    private sealed class DockProbe
    {
      public DockProbe(InputDockPanel panel, InputDockViewModel vm)
      {
        Panel = panel;
        Vm = vm;
      }

      public readonly InputDockPanel Panel;
      public readonly InputDockViewModel Vm;

      public RectTransform Root => Panel.Root;
      public ValueBrowserViewModel Values => Vm.Values;
      public string Owner => Vm.Owner.Value;

      /// <summary>What the line says to do, while nothing is edited.</summary>
      public string HintText => Vm.Line.Open ? "" : Vm.Line.Hint;

      /// <summary>The line's pieces as words, a condition's slots in a row: "192 × 콤보".</summary>
      public string FormulaText => Vm.Line.Open ? LineText(Vm.Line.Root) : "";

      public string ResultText => Vm.Line.Result;
      public bool IsTyping => ElementUI.Ugui.Keyboard.IsTaken;
      public bool Contains(Vector2 screen) => Panel.Contains(screen);
      public void Tick() => Panel.Tick();

      /// <summary>The dock has no caret of its own any more: what is typed shows on its piece.</summary>
      public void Blink(bool visible) { }

      private static string LineText(LineSlotViewModel slot)
      {
        if (slot.Pieces.Length == 0)
          return "[" + slot.Placeholder + "]";
        System.Text.StringBuilder text = new();
        foreach (LinePieceViewModel piece in slot.Pieces)
        {
          if (text.Length > 0)
            text.Append(' ');
          switch (piece.Kind)
          {
            case LinePieceKind.Condition:
              text.Append(piece.Label).Append(' ')
                .Append(LineText(piece.Slots[0])).Append(' ')
                .Append(piece.Compare).Append(' ')
                .Append(LineText(piece.Slots[2])).Append(" → ")
                .Append(LineText(piece.Slots[3])).Append(" / ")
                .Append(LineText(piece.Slots[4]));
              break;
            case LinePieceKind.Group:
              text.Append('(').Append(LineText(piece.Slots[0])).Append(')');
              break;
            default:
              if (piece.HasSwatch)
                text.Append("● ");
              text.Append(piece.Label);
              break;
          }
        }
        return text.ToString();
      }
    }

    /// <summary>A dock with nowhere to send what is done in it, for docks the checks make on their own.</summary>
    private sealed class NoDockActions : IInputDockActions
    {
      public static readonly NoDockActions Instance = new();

      public void Press(char key) { }

      public void SelectAt(IReadOnlyList<FormulaStep> route, int index) { }

      public void Words(string words) { }

      public void Rename(string name) { }

      public void Switch(bool value) { }

      public void Remove() { }

      public void OpenMade() { }

      public void Done() { }

      public void Pick(OverlayValueDefinition value) { }

    }

    /// <summary>The open editor's input dock.</summary>
    private static DockProbe DockOf(object editor) =>
      new DockProbe(Field<InputDockPanel>(editor, "_dockPanel"), Field<InputDockViewModel>(editor, "_dock"));

    /// <summary>A key of the dock's keypad as it is drawn now, without letting the dock catch up first.</summary>
    private static Pressable DockKeyAsIs(DockProbe dock, string name) =>
      dock.Root.GetComponentsInChildren<Pressable>(true).Single(key => key.name == name);

    /// <summary>The dock's keys as drawn, row by row, then AC and Done, once the dock caught up.</summary>
    private static Pressable[] DockKeys(DockProbe dock)
    {
      Settle();
      return dock.Root.GetComponentsInChildren<Pressable>(true).Where(key => key.name.StartsWith("Key ")).ToArray();
    }

    /// <summary>A key of the dock's keypad by name: "Key 7", "Key backspace", "Key clear", "Key done".</summary>
    private static Pressable DockKey(DockProbe dock, string name) => DockKeys(dock).Single(key => key.name == name);

    /// <summary>Where the keypad is drawn: around its keys.</summary>
    private static Rect KeypadRect(DockProbe dock)
    {
      Rect[] keys = DockKeys(dock).Select(key => ScreenRectOf((RectTransform)key.transform)).ToArray();
      return Rect.MinMaxRect(keys.Min(key => key.xMin), keys.Min(key => key.yMin), keys.Max(key => key.xMax), keys.Max(key => key.yMax));
    }

    /// <summary>The value chips the dock shows now, once it caught up: the open place's, or what the search found.</summary>
    private static Pressable[] DockChips(DockProbe dock)
    {
      Settle();
      dock.Tick();
      return dock.Root.GetComponentsInChildren<Pressable>(false).Where(chip => chip.name.StartsWith("Value ")).ToArray();
    }

    /// <summary>A value's chip in the list: every section shows at once, so a listed value is there to press.</summary>
    private static Pressable DockChip(DockProbe dock, string name)
    {
      Pressable[] chips = DockChips(dock);
      if (!chips.Any(chip => chip.name == name))
        throw new System.InvalidOperationException("The list does not show " + name
        + "; title=" + dock.Values.Title.Value + ", listing=" + dock.Values.Listing.Value
        + ", listed=" + string.Join(",", chips.Select(chip => chip.name)));
      return chips.Single(chip => chip.name == name);
    }

    /// <summary>Whether a value is listed now: it fits where values go, and so its section shows too.</summary>
    private static bool DockListed(DockProbe dock, string name)
    {
      Settle();
      ValueBrowserViewModel values = dock.Values;
      ValueFolderViewModel section = values.Sections.First(each => each.Chips.Any(chip => chip.Name == name));
      bool listed = values.Listing.Value && section.Chips.Single(chip => chip.Name == name).Fits.Value;
      Assert(!listed || (section.Shown.Value && DockSection(dock, section.Key) != null), "a value that is listed has its section's heading: " + name);
      return listed;
    }

    /// <summary>A section's heading in the dock's list: "Judgement", "mod:TUFOverlay"; null while it isn't drawn.</summary>
    private static TextMeshProUGUI DockSection(DockProbe dock, string key)
    {
      Settle();
      return dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).FirstOrDefault(label => label.name == "Section " + key);
    }

    /// <summary>Presses the open editor's Cancel or Done, named after the editor.</summary>
    private static void PressEditorButton(object editor, string name) =>
      Press(ByName<Pressable>(EditorScreen(editor).Frame, name));

    /// <summary>
    /// The header: what is edited on the left, Cancel and Done at its right
    /// end, opaque across the top so the game's HUD never shows through the
    /// editor's words; its buttons never keep the keyboard, so Enter and Esc
    /// stay the editor's. A notice shows for a while on the dock's line (warm
    /// for a warning), or in the header for an editor without a dock.
    /// </summary>
    private static void CheckEditorTopLine(object editor, string name, string title)
    {
      EditorFramePanel screen = EditorScreen(editor);
      // A notice an earlier step left is read first, and shown again after.
      string notice = screen.ViewModel.NoticeText;
      bool warning = screen.ViewModel.Ink.Value.Equals(ElementUI.Colors.Danger);
      SetField(screen.ViewModel, "_noticeUntil", -1f);
      screen.Tick();
      Settle();
      RectTransform frame = screen.Frame;
      RectTransform header = ByName<RectTransform>(frame, "Header");
      TextMeshProUGUI heading = ByName<TextMeshProUGUI>(frame, "Editor title");
      Pressable cancel = ByName<Pressable>(frame, name + " cancel"), done = ByName<Pressable>(frame, name + " done");
      Assert(heading != null && heading.text == title && Shown(cancel) && Shown(done)
        && cancel.GetComponentInChildren<TextMeshProUGUI>().text == L("취소", "Cancel")
        && done.GetComponentInChildren<TextMeshProUGUI>().text == L("완료", "Done"),
        "the header says what is edited, with Cancel and Done: " + heading?.text);
      Rect headerRect = ScreenRectOf(header), headingRect = ScreenRectOf(heading.rectTransform),
        cancelRect = ScreenRectOf((RectTransform)cancel.transform), doneRect = ScreenRectOf((RectTransform)done.transform);
      float contentLeft = screen.ContentLeft * screen.Scale, contentRight = (screen.ContentLeft + screen.ContentWidth) * screen.Scale;
      Assert(header.GetComponent<RoundedRect>() == null && Mathf.Abs(headerRect.xMin - contentLeft) < 1 && Mathf.Abs(headerRect.xMax - contentRight) < 1
        && Mathf.Abs(headerRect.yMax - Screen.height) < 1 && Mathf.Abs(headerRect.height - EditorFramePanel.TopBar * screen.Scale) < 1,
        "the header has no fill of its own (the dim is behind it) and is as wide as the content: " + headerRect);
      Assert(headingRect.xMax <= cancelRect.xMin && cancelRect.xMax < doneRect.xMin && Mathf.Abs(cancelRect.center.y - doneRect.center.y) < .5f
        && Mathf.Abs(headingRect.center.y - doneRect.center.y) < 2 && Mathf.Abs(doneRect.center.y - headerRect.center.y) < 1 && InsideScreen(doneRect)
        && Mathf.Abs(headingRect.xMin - contentLeft) < 1 && Mathf.Abs(doneRect.xMax - contentRight) < 1,
        "what is edited on the left, Cancel and Done at the right end, in line with the content: " + headingRect + " / " + doneRect);
      if (screen.ViewModel.InDock)
      {
        DockProbe headerDock = DockOf(editor);
        Assert(Mathf.Abs(ScreenRectOf(headerDock.Root).xMin - headingRect.xMin) < 1 && Mathf.Abs(KeypadRect(headerDock).xMax - doneRect.xMax) < 1,
          "the title starts where the dock does and Done ends with the keypad");
      }
      Assert(headerRect.yMin >= ScreenRectOf(EditorBarRoot(editor)).yMax - .5f, "the header sits above the properties");
      Assert(done.GetComponent<RoundedRect>().color.maxColorComponent < .5f && cancel.navigation.mode == Navigation.Mode.None
        && done.navigation.mode == Navigation.Mode.None, "Done is the tinted main action; neither keeps the keyboard");
      foreach (TextMeshProUGUI label in new[] { heading }.Concat(cancel.GetComponentsInChildren<TextMeshProUGUI>()).Concat(done.GetComponentsInChildren<TextMeshProUGUI>()))
      {
        label.ForceMeshUpdate();
        Assert(label.font == screen.Measure.font && !label.isTextTruncated, "header label fits: " + label.text);
      }
      bool docked = screen.ViewModel.InDock;
      TextMeshProUGUI headerNotice = ByName<TextMeshProUGUI>(frame, "Header notice");
      TextMeshProUGUI line = docked ? ByName<TextMeshProUGUI>(DockOf(editor).Root, "Notice") : headerNotice;
      string plain = line.text;
      Color plainInk = line.color;
      screen.ViewModel.Notice("Preview warning", true);
      Settle();
      Assert(screen.ViewModel.NoticeText == "Preview warning" && line.text == "Preview warning" && line.color == UiPalette.Error,
        "a warning shows " + (docked ? "under the dock's line" : "in the header") + ", warm");
      if (docked)
      {
        DockProbe noticeDock = DockOf(editor);
        Rect noticeRect = ScreenRectOf(line.rectTransform), keypadRect = KeypadRect(noticeDock);
        Assert(noticeRect.yMax <= ScreenRectOf(noticeDock.Root.GetComponentsInChildren<RectTransform>(false).First(part => part.name == "Formula line" || part.name == "Line hint")).yMin + .5f && noticeRect.yMin >= keypadRect.yMax - .5f,
          "the notice sits under the line, over the values and the input method: " + noticeRect);
      }
      Assert(!docked || headerNotice.text.Length == 0, "the header stays quiet while the dock shows the notices");
      screen.ViewModel.Notice("Preview note", false);
      Settle();
      Assert(line.text == "Preview note" && line.color == UiPalette.Foreground, "a note reads white");
      SetField(screen.ViewModel, "_noticeUntil", -1f);
      screen.Tick();
      Settle();
      Assert(screen.ViewModel.NoticeText == "" && line.text == plain && (plain.Length == 0 || line.color == plainInk),
        "what the line said comes back once a notice has been read");
      if (notice.Length > 0)
        screen.ViewModel.Notice(notice, warning);
    }

    /// <summary>
    /// Grow from's input method as drawn: the nine points as keys, Auto and
    /// a line saying what the choice does; a point's press picks it and Auto
    /// puts Auto back. Leaves the growth as it found it.
    /// </summary>
    private static void CheckGrowPopover(GrowFrom grow)
    {
      GrowPopoverPanel popover = Field<GrowPopoverPanel>(grow, "_popover");
      OverlayGrowth before = grow.Growth;
      Settle();
      Pressable[] points = popover.Root.GetComponentsInChildren<Pressable>(true).Where(point => point.name.StartsWith("Grow from ")).ToArray();
      Assert(points.Select(point => point.name).SequenceEqual(Enumerable.Range(0, 9).Select(i => "Grow from " + (OverlayGrowth)i)),
        "the input method offers the nine points, row by row");
      Rect first = ScreenRectOf((RectTransform)points[0].transform), last = ScreenRectOf((RectTransform)points[8].transform);
      Assert(Mathf.Abs(first.width - first.height) < .5f && first.xMax < last.xMin && first.yMin > last.yMax,
        "the points are square keys, three by three: " + first + " / " + last);
      Pressable auto = ByName<Pressable>(popover.Root, "Grow automatically");
      TextMeshProUGUI caption = ByName<TextMeshProUGUI>(popover.Root, "Caption");
      Assert(Shown(auto) && caption != null && caption.text == popover.ViewModel.Says.Value && caption.text.Length > 0
        && ScreenRectOf((RectTransform)auto.transform).yMax <= last.yMin + .5f,
        "Auto sits under the points, and a line says what the choice does");
      foreach (TextMeshProUGUI label in popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "grow input method label fits: " + label.text);
      }
      Rect box = ScreenRectOf(popover.Root);
      Assert(points.All(point => box.Contains(ScreenRectOf((RectTransform)point.transform).center)) && box.Contains(ScreenRectOf(caption.rectTransform).min),
        "its points and caption sit inside it");
      Press(points[(int)OverlayGrowth.BottomRight]);
      Settle();
      GrowPickerViewModel model = popover.ViewModel;
      Assert(grow.Growth == OverlayGrowth.BottomRight && model.Points[8].Look.Value == GrowLook.Chosen && !model.IsAuto.Value
        && model.Points[0].Look.Value != GrowLook.Chosen && caption.text.Contains(L("오른쪽 아래", "bottom right")),
        "a point's press picks it: its key alone holds, the caption names it: " + caption.text);
      Press(auto);
      Settle();
      Assert(grow.Growth == OverlayGrowth.Auto && caption.text.StartsWith(L("자동", "Auto")) && model.IsAuto.Value,
        "Auto follows where the element sits, and holds instead of a point: " + caption.text);
      Call(grow, "Pick", before);
    }
  }
}

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
using Element.Domain.Overlay.Variables;
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
    /// A field picked from a list of its own (a font) as the dock shows it:
    /// the column keeps its rows, the dock's line names the field
    /// (<paramref name="whose"/> <paramref name="name"/>) and shows its one
    /// choice, no values are listed, and <paramref name="drawn"/> stands
    /// where the input method is, with nothing under it.
    /// </summary>
    private static void AssertInputMethod(object editor, RectTransform drawn, DockMethod method, string name, string whose)
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
      Assert(dock.Vm.Method.Value == method && !dock.Values.Listing.Value && DockChips(dock).Length == 0,
        "the place is " + name + "'s input method, and no values are listed for it");
      Assert(RemoveKey(dock) == null, "nothing of " + name + " is taken out: it is swapped for another");
      Rect toolRect = ScreenRectOf(drawn), place = ScreenRectOf(dock.MethodRoot);
      Assert(Mathf.Abs(toolRect.xMin - place.xMin) < 1.5f && toolRect.xMax <= place.xMax + 1.5f
        && toolRect.yMin >= place.yMin - 1.5f && toolRect.yMax <= place.yMax + 1.5f && InsideScreen(toolRect),
        name + " stands where the input method is: " + toolRect + " / " + place);
      Graphic toolSurface = drawn.GetComponent<Graphic>();
      Assert(toolSurface == null || toolSurface.color.a < .01f, name + " stands on the screen with no box around it");
    }

    /// <summary>Enter on a field picked from a fixed set: its input method closes and the dock rests.</summary>
    private static void ApplyChoice(object editor)
    {
      DockProbe dock = DockOf(editor);
      Finish(editor);
      Assert(!dock.Vm.Line.Choosing && dock.Vm.Choice.Value == DockMethod.Rest, "Enter closes the field's input method");
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

      /// <summary>The left part: the line and the values.</summary>
      public RectTransform Root => Panel.Root;

      /// <summary>The right part: the input method.</summary>
      public RectTransform MethodRoot => Panel.MethodRoot;
      public ValueBrowserViewModel Values => Vm.Values;
      public string Owner => Vm.Owner.Value;

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

      public void Switch(bool value) { }

      public void Point(OverlayGrowth point) { }

      public void Digits(int digits) { }

      public void Enter(int slot) { }

      public void Hop(int slot) { }

      public void Leave() { }

      public void Remove() { }

      public void Done() { }

      public void Pick(OverlayValueDefinition value) { }

    }

    /// <summary>
    /// The editor is two rows with one line across between them: above it
    /// the preview and the property column, below it the line and values and
    /// the input method. No line runs between the columns.
    /// </summary>
    private static void CheckTwoRows(object editor, string what)
    {
      DockProbe dock = DockOf(editor);
      InputDockPanel panel = Field<InputDockPanel>(editor, "_dockPanel");
      InspectorPanel inspector = Field<InspectorPanel>(editor, "_inspector");
      EditorFramePanel frame = Field<EditorFramePanel>(editor, "_frame");
      RectTransform controls = frame.Frame;
      Image[] rowLines = controls.GetComponentsInChildren<Image>(true).Where(line => line.name == "Row line").ToArray();
      Assert(rowLines.Length == 1 && !controls.GetComponentsInChildren<Image>(true).Any(line => line.name == "Column line"),
        what + ": one line between the rows, none between the columns");
      Rect row = ScreenRectOf(rowLines[0].rectTransform), left = ScreenRectOf(dock.Root), method = ScreenRectOf(dock.MethodRoot), column = ScreenRectOf(inspector.Root);
      Assert(row.height < 4 && Mathf.Abs(row.xMin - left.xMin) < 1 && Mathf.Abs(row.xMax - method.xMax) < 1,
        what + ": the line runs across the content, from the line's left to the input method's right: " + row + " / " + left + " / " + method);
      Assert(left.yMax <= row.yMin + .5f && method.yMax < row.yMin - 4 && column.yMin > row.yMax + 4,
        what + ": the line and values and the input method are under it, the property column over it, clear of it: " + row + " / " + method + " / " + column);
    }

    /// <summary>The open editor's input dock.</summary>
    private static DockProbe DockOf(object editor) =>
      new DockProbe(Field<InputDockPanel>(editor, "_dockPanel"), Field<InputDockViewModel>(editor, "_dock"));

    /// <summary>A key of the dock's keypad as it is drawn now, without letting the dock catch up first.</summary>
    private static Pressable DockKeyAsIs(DockProbe dock, string name) =>
      ShownKeys(dock.MethodRoot.GetComponentsInChildren<Pressable>(true)).Single(key => key.name == name);

    // An input method the dock isn't showing now is kept, hidden (the keypad as large as the whole
    // place, and the lower one under a folded condition): of two keys of a name, the one shown is meant.
    private static Pressable[] ShownKeys(Pressable[] keys) =>
      keys.Where(key => key.gameObject.activeInHierarchy
        || !keys.Any(other => other != key && other.name == key.name
          && (other.gameObject.activeInHierarchy || System.Array.IndexOf(keys, other) < System.Array.IndexOf(keys, key)))).ToArray();

    /// <summary>The dock's keys as drawn, row by row, then AC and Done, once the dock caught up.</summary>
    private static Pressable[] DockKeys(DockProbe dock)
    {
      Settle();
      return ShownKeys(dock.MethodRoot.GetComponentsInChildren<Pressable>(true).Where(key => key.name.StartsWith("Key ")).ToArray());
    }

    /// <summary>
    /// Enter, as the player finishes what is open: the editor's own keyboard
    /// takes it (refused with a shake while the field doesn't fit).
    /// </summary>
    private static void Finish(object editor)
    {
      if (editor is TextEditorScreen text)
        Field<TextKeyboardInput>(text, "_keyboard").Key(KeyDown(KeyCode.Return, '\n'));
      else
        Field<Element.Presentation.OverlayEditor.ShapeEditor.ShapeInput>(editor, "_input").Key(KeyDown(KeyCode.Return, '\n'));
      Settle();
    }

    /// <summary>The × on the line's picked piece; null while none shows.</summary>
    private static Pressable RemoveKey(DockProbe dock)
    {
      Settle();
      return dock.Root.GetComponentsInChildren<Pressable>(false).SingleOrDefault(key => key.name == "Remove piece");
    }

    /// <summary>A key of the dock's keypad by name: "Key 7", "Key backspace", "Key ×".</summary>
    private static Pressable DockKey(DockProbe dock, string name) => DockKeys(dock).Single(key => key.name == name);

    /// <summary>Where the keypad is drawn: around its keys; with none shown, the input method's place.</summary>
    private static Rect KeypadRect(DockProbe dock)
    {
      Rect[] keys = DockKeys(dock).Where(key => key.gameObject.activeInHierarchy).Select(key => ScreenRectOf((RectTransform)key.transform)).ToArray();
      if (keys.Length == 0)
        return ScreenRectOf(dock.MethodRoot);
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
        + "; listing=" + dock.Values.Listing.Value
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
      if (docked)
      {
        // An editor with a dock says nothing in words: what can't be done shakes instead.
        Assert(headerNotice.text.Length == 0 && ByName<TextMeshProUGUI>(DockOf(editor).Root, "Notice") == null,
          "an editor with a dock has no line of notices");
        return;
      }
      string plain = headerNotice.text;
      Color plainInk = headerNotice.color;
      screen.ViewModel.Notice("Preview warning", true);
      Settle();
      Assert(screen.ViewModel.NoticeText == "Preview warning" && headerNotice.text == "Preview warning" && headerNotice.color == UiPalette.Error,
        "a warning shows in the header, warm");
      screen.ViewModel.Notice("Preview note", false);
      Settle();
      Assert(headerNotice.text == "Preview note" && headerNotice.color == UiPalette.Foreground, "a note reads white");
      SetField(screen.ViewModel, "_noticeUntil", -1f);
      screen.Tick();
      Settle();
      Assert(screen.ViewModel.NoticeText == "" && headerNotice.text == plain && (plain.Length == 0 || headerNotice.color == plainInk),
        "what the line said comes back once a notice has been read");
      if (notice.Length > 0)
        screen.ViewModel.Notice(notice, warning);
    }

    /// <summary>
    /// The grow point as a field: opened, the dock's line names it and shows
    /// the point it holds, the values list offers a point or a condition to
    /// make, and the input method is the nine points as keys with Auto under
    /// them. A key's press picks its point and Auto puts Auto back. Leaves
    /// the field open on Auto.
    /// </summary>
    private static void CheckGrowField(object editor, GrowFrom grow)
    {
      Settle();
      DockProbe dock = DockOf(editor);
      LineViewModel line = dock.Vm.Line;
      Assert(dock.Owner == GrowFrom.Key && line.Open && !line.Choosing && line.Type == OverlayValueType.Point
        && line.Name == L("기준점", "Grow from") && line.Whose == "" && line.Root.Pieces.Length == 1,
        "the grow point opens in the dock like any field, of the point type: " + line.Whose + line.Name);
      Assert(dock.Vm.Method.Value == DockMethod.Points && grow.Button.Active, "a point's input method is the nine points; the field is lit");
      Assert(DockListed(dock, "Value make-point") && DockListed(dock, "Value make-condition") && !DockListed(dock, "Value combo")
        && !DockListed(dock, "Value make-group"), "only a point or a condition that gives points goes there");
      Pressable[] points = dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Where(point => point.name.StartsWith("Grow from ")).ToArray();
      Assert(points.Select(point => point.name).SequenceEqual(Enumerable.Range(0, 9).Select(i => "Grow from " + (OverlayGrowth)i)),
        "the input method offers the nine points, row by row");
      Rect first = ScreenRectOf((RectTransform)points[0].transform), last = ScreenRectOf((RectTransform)points[8].transform),
        place = ScreenRectOf(dock.MethodRoot);
      Assert(Mathf.Abs(first.width - first.height) < .5f && first.xMax < last.xMin && first.yMin > last.yMax
        && place.Contains(first.center) && place.Contains(last.center),
        "the points are square keys, three by three, in the input method's place: " + first + " / " + last);
      Pressable auto = ByName<Pressable>(dock.MethodRoot, "Grow automatically");
      Assert(Shown(auto) && ScreenRectOf((RectTransform)auto.transform).yMax <= last.yMin + .5f
        && ScreenRectOf((RectTransform)auto.transform).yMin >= place.yMin - .5f,
        "Auto sits under the points, in the input method's place");
      Assert(!dock.MethodRoot.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.name == "Caption"),
        "no sentence explains the points");
      Press(points[(int)OverlayGrowth.BottomRight]);
      Settle();
      GrowPickerViewModel model = dock.Vm.Points;
      Assert(grow.Growth(null) == OverlayGrowth.BottomRight && model.Points[8].Look.Value == GrowLook.Chosen && !model.IsAuto.Value
        && model.Points[0].Look.Value != GrowLook.Chosen && grow.Button.Value.Contains(L("오른쪽 아래", "Bottom right")),
        "a point's press picks it: its key alone holds, and the field names it: " + grow.Button.Value);
      Press(auto);
      Settle();
      Assert(grow.Growth(null) == OverlayGrowth.Auto && model.IsAuto.Value && grow.Button.Value.Contains(L("자동", "Auto")),
        "Auto follows where the element sits, and holds instead of a point: " + grow.Button.Value);
      Assert(RemoveKey(dock) == null, "a point is swapped for another, never removed: its pill has no ×");
    }
  }
}

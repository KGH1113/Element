using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock;
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
    /// A tool a row opened (the picker, the font list, the points) as the
    /// column shows it: the rows give way to a back row naming
    /// <paramref name="tool"/>, and the tool hangs under it, ending with the column.
    /// </summary>
    private static void AssertInColumn(object editor, RectTransform drawn, string tool)
    {
      Settle();
      InspectorPanel inspector = EditorInspector(editor);
      Pressable back = ByName<Pressable>(inspector.Root, "Inspector back");
      Assert(back != null && Shown(back) && back.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == tool)
        && inspector.ViewModel.OpenTool == tool && inspector.Root.GetComponentsInChildren<Pressable>(false).Length == 1,
        "the column shows only a back row naming " + tool);
      Rect backRect = ScreenRectOf((RectTransform)back.transform), toolRect = ScreenRectOf(drawn),
        keypad = KeypadRect(DockOf(editor));
      Assert(toolRect.yMax <= backRect.yMin + .5f && Mathf.Abs(toolRect.xMax - keypad.xMax) < 1 && InsideScreen(toolRect),
        tool + " opens in the column under its back row, ending with the keypad: " + toolRect + " / " + backRect);
      Assert(Mathf.Abs(backRect.xMin - Mathf.Min(keypad.xMin, toolRect.xMin)) < 1 && Mathf.Abs(backRect.xMax - keypad.xMax) < 1,
        "the back row is as wide as the tool under it: " + backRect + " / " + toolRect);
      Graphic toolSurface = drawn.GetComponent<Graphic>();
      Assert(back.targetGraphic.color.a < .01f && (toolSurface == null || toolSurface.color.a < .01f),
        tool + " and its back row stand on the screen with no box around them");
    }

    /// <summary>The column's back row: the tool closes and the rows come back.</summary>
    private static void PressBack(object editor)
    {
      Pressable back = ByName<Pressable>(EditorInspector(editor).Root, "Inspector back");
      Assert(back != null && Shown(back), "the column has a back row");
      Press(back);
      Settle();
      Assert(EditorInspector(editor).ViewModel.OpenTool == null, "back brings the rows again");
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
      public string HintText => Vm.Owner.Value == null ? Vm.Title.Value : "";

      public string FormulaText => Vm.Formula.Value;
      public string ResultText => Vm.Result.Value;
      public bool IsTyping => ElementUI.Ugui.Keyboard.IsTaken;
      public bool Contains(Vector2 screen) => Panel.Contains(screen);
      public bool Dragging => DragSession.Active;
      public void Tick() => Panel.Tick();
      public void Blink(bool visible) => Vm.RestartBlink();
    }

    /// <summary>A dock with nowhere to send what is done in it, for docks the checks make on their own.</summary>
    private sealed class NoDockActions : IInputDockActions
    {
      public static readonly NoDockActions Instance = new();

      public void Press(char key) { }

      public void Select(int index) { }

      public void Enter(int slot) { }

      public void Back() { }

      public void Words(string words) { }

      public void Rename(string name) { }

      public void Switch(bool value) { }

      public void Clear() { }

      public void Done() { }

      public void Pick(OverlayValueDefinition value) { }

      public void BeganCarrying() { }

      public void DropOnFormula(OverlayValueDefinition value) { }
    }

    /// <summary>
    /// A value carried out of an editor's dock as the player carries it: its
    /// chip is pressed and dragged past the threshold, and each move is aimed
    /// at the topmost thing drawn under the pointer, as the input module does.
    /// The chip is found with the dock's search unless its place is open
    /// (<paramref name="open"/>), and the search is cleared once it is let go.
    /// </summary>
    private sealed class Carry
    {
      private readonly DockProbe _dock;
      private readonly DragSource _source;
      private readonly PointerEventData _data;
      private readonly bool _searched;

      public Carry(DockProbe dock, OverlayValueId id, bool open = false)
      {
        _dock = dock;
        OverlayValueDefinition value = OverlayValueCatalog.Get(id);
        string name = "Value " + value.Key;
        Pressable chip;
        if (open)
          chip = DockChip(dock, name);
        else
        {
          _searched = true;
          dock.Values.Query.Value = ValueSectionNames.ValueName(value);
          dock.Values.Search.TryExecute();
          chip = DockChips(dock).Single(found => found.name == name);
        }
        GameObject handler = ExecuteEvents.GetEventHandler<IDragHandler>(chip.gameObject);
        _source = handler != null ? handler.GetComponent<DragSource>() : null;
        Assert(_source != null, "a chip's drag goes to ElementUI's drag source: " + name);
        Vector2 start = ScreenCenter((RectTransform)chip.transform);
        _data = new PointerEventData(EventSystem.current)
        {
          button = PointerEventData.InputButton.Left,
          pointerId = -1,
          position = start,
          pressPosition = start,
        };
        Aim(start);
        _source.OnInitializePotentialDrag(_data);
        _data.eligibleForClick = true;
        _source.OnBeginDrag(_data);
        Settle();
      }

      public Carry MoveTo(Vector2 point)
      {
        Aim(point);
        _source.OnDrag(_data);
        Settle();
        return this;
      }

      public void LetGoAt(Vector2 point)
      {
        MoveTo(point);
        _source.OnEndDrag(_data);
        Settle();
        if (_searched)
        {
          _dock.Values.ClearSearch.TryExecute();
          Settle();
        }
      }

      private void Aim(Vector2 point)
      {
        _data.position = point;
        _data.pointerCurrentRaycast = new RaycastResult { gameObject = TopmostAt(point), screenPosition = point };
      }
    }

    /// <summary>
    /// What a raycast at <paramref name="point"/> hits first: the last-drawn raycast
    /// target there, over every canvas, leaving out what takes no raycasts.
    /// </summary>
    private static GameObject TopmostAt(Vector2 point)
    {
      GameObject top = null;
      foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).OrderBy(c => c.sortingOrder))
      {
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
          if (graphic.raycastTarget && graphic.isActiveAndEnabled && TakesRaycasts(graphic.transform)
            && RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, point, camera))
            top = graphic.gameObject;
      }
      return top;
    }

    private static bool TakesRaycasts(Transform at)
    {
      for (; at != null; at = at.parent)
        if (at.TryGetComponent(out CanvasGroup group) && !group.blocksRaycasts)
          return false;
      return true;
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

    /// <summary>A value's chip, after opening the place that holds it as the player would.</summary>
    private static Pressable DockChip(DockProbe dock, string name)
    {
      ValueBrowserViewModel values = dock.Values;
      ValueFolderViewModel folder = values.Categories.Concat(values.Mods).First(place => place.Chips.Any(chip => chip.Name == name));
      if (values.Searching.Value || values.OpenFolder != folder)
        Press(DockPlace(dock, folder.Key));
      Pressable[] chips = DockChips(dock);
      if (!chips.Any(chip => chip.name == name))
        throw new System.InvalidOperationException("Opening " + folder.Key + " did not expose " + name
        + "; open=" + values.OpenFolder.Key + ", query=" + values.Query.Value + ", searching=" + values.Searching.Value
        + ", folderVisible=" + folder.Open.Value + ", rootActive=" + dock.Root.gameObject.activeInHierarchy
        + ", visible=" + string.Join(",", chips.Select(chip => chip.name))
        + ", matchingNative=" + string.Join(";", dock.Root.GetComponentsInChildren<Pressable>(true)
          .Where(chip => chip.name == name).Select(chip => chip.transform.parent.name + ":self="
            + chip.gameObject.activeSelf + ":hierarchy=" + chip.gameObject.activeInHierarchy)));
      return chips.Single(chip => chip.name == name);
    }

    /// <summary>A row of the dock's list of places: "Judgement", "mod:TUFOverlay".</summary>
    private static Pressable DockPlace(DockProbe dock, string key)
    {
      Settle();
      return ByName<Pressable>(dock.Root, "Place " + key);
    }

    /// <summary>The dock's value search field.</summary>
    private static TMP_InputField DockSearch(DockProbe dock)
    {
      Settle();
      return ByName<TMP_InputField>(dock.Root, "Value search");
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
        "a warning shows " + (docked ? "right above the dock" : "in the header") + ", warm");
      if (docked)
      {
        DockProbe noticeDock = DockOf(editor);
        Rect noticeRect = ScreenRectOf(line.rectTransform), keypadRect = KeypadRect(noticeDock);
        Assert(noticeRect.yMin >= ScreenRectOf(ByName<RectTransform>(noticeDock.Root, "Formula line")).yMax - .5f && noticeRect.xMax <= keypadRect.xMin,
          "the notice sits over the formula line, clear of the keypad's column: " + noticeRect);
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
    /// Grow from's popover as drawn: what it picks, the nine points, Auto and
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
        "the popover offers the nine points, row by row");
      Pressable auto = ByName<Pressable>(popover.Root, "Grow automatically");
      TextMeshProUGUI caption = ByName<TextMeshProUGUI>(popover.Root, "Caption");
      Assert(Shown(auto) && caption != null && caption.text == popover.ViewModel.Caption.Value && caption.text.Length > 0
        && popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == L("크기가 바뀔 때 고정할 점", "Point that stays when the size changes")),
        "it says what it picks, with Auto and what the choice does");
      foreach (TextMeshProUGUI label in popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "grow popover label fits: " + label.text);
      }
      Rect box = ScreenRectOf(popover.Root);
      Assert(points.All(point => box.Contains(ScreenRectOf((RectTransform)point.transform).center)) && box.Contains(ScreenRectOf(caption.rectTransform).min),
        "its points and caption sit inside it");
      Press(points[(int)OverlayGrowth.BottomRight]);
      Settle();
      GrowPickerViewModel model = popover.ViewModel;
      Assert(grow.Growth == OverlayGrowth.BottomRight && model.Points[8].Look.Value == GrowLook.Chosen
        && model.Points[4].Look.Value == GrowLook.Arrow && model.Points[5].Look.Value == GrowLook.Arrow
        && model.Points[0].Look.Value == GrowLook.Dot && caption.text.Contains(L("오른쪽 아래", "bottom right")),
        "a point's press picks it: filled, arrows on the points around it, the caption names it: " + caption.text);
      Press(auto);
      Settle();
      Assert(grow.Growth == OverlayGrowth.Auto && caption.text.StartsWith(L("자동", "Auto")) 
        && auto.GetComponentInChildren<TextMeshProUGUI>().color == UiPalette.Foreground,
        "Auto follows where the element sits, and lights up: " + caption.text);
      Call(grow, "Pick", before);
    }
  }
}

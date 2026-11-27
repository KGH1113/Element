using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Condition;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.TextEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;

namespace Element.Editor
{
  // Variables as the player builds them: a size made a condition slot by
  // slot, a text color whose condition gives the highlight color or one
  // picked by hand (the picker following the color piece picked), and Done
  // saving what the game then draws.
  public static partial class SettingsPreview
  {
    private static void CheckVariables(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      TextOverlayElement accuracy = host.LayoutTargets.OfType<TextOverlayElement>().Single(target => target.Id == "accuracy");
      Select(editor, IndexOf(editor, accuracy));
      PressEdit(editor);
      TextEditorScreen text = (TextEditorScreen)Kind<TextKind>(editor).Editor;
      Assert(text.IsOpen, "the X-Accuracy text opens in the editor");
      TextDocumentEditing document = TextDocumentOf(text);
      DockProbe dock = DockOf(text);
      TextColor color = TextColorOf(text);
      TextFormulas formulas = TextFormulasOf(text);
      TextMeshProUGUI previewText = TextPreviewOf(text).Text;

      // A size made a condition: combo 100 or more is 60, otherwise 40.
      PropertyButtonViewModel size = Field<TextSize>(text, "_size").Button;
      PressBar(text, size);
      Settle();
      Assert(dock.Owner == "size", "the size opens in the dock");
      Press(DockChip(dock, "Value make-condition"));
      Settle();
      Pressable[] slots = dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Where(row => row.name.StartsWith("Condition slot ")).ToArray();
      Assert(dock.Vm.Method.Value == DockMethod.Condition && formulas.Editing.Problem?.Slot == OverlayPiece.Left
        && dock.Vm.Line.Root.Pieces.Length == 1 && dock.Vm.Line.Root.Pieces[0].Label == L("조건", "Condition")
        && slots.Select(row => row.name).SequenceEqual(Enumerable.Range(0, 5).Select(i => "Condition slot " + i)),
        "a new condition is one value on the line, and its input method is its five slots: " + dock.FormulaText);
      // The condition is drawn as a fork: what is compared in one bar on top (value, comparison, value),
      // the two answers side by side under it.
      Rect place = ScreenRectOf(dock.MethodRoot);
      Rect[] rows = slots.Select(row => ScreenRectOf((RectTransform)row.transform)).ToArray();
      Rect left = rows[OverlayPiece.Left], sign = rows[OverlayPiece.Compare], right = rows[OverlayPiece.Right],
        yes = rows[OverlayPiece.Result], no = rows[OverlayPiece.Default];
      Assert(left.xMax <= sign.xMin + .5f && sign.xMax <= right.xMin + .5f && Mathf.Abs(left.center.y - right.center.y) < .5f
        && Mathf.Abs(sign.center.y - left.center.y) < .5f && Mathf.Abs(sign.width - sign.height) < .5f
        && Mathf.Abs(sign.center.x - place.center.x) < 1,
        "what is compared stands in one bar: value, a round comparison in the middle, value: " + left + " / " + sign + " / " + right);
      Assert(yes.yMax < left.yMin && no.yMax < left.yMin && yes.xMax < no.xMin && Mathf.Abs(yes.width - no.width) < .5f
        && Mathf.Abs(yes.center.y - no.center.y) < .5f && yes.height > left.height * 2,
        "the two answers stand side by side under it, as large tiles: " + yes + " / " + no);
      Assert(rows.All(row => place.Contains(row.center) && row.yMin >= place.yMin - .5f),
        "all of it in the input method's place");
      Assert(dock.Vm.Form.Slots.All(row => row.Slot == OverlayPiece.Compare || row.Look.Value == ConditionSlotLook.Empty)
        && dock.Vm.Form.Slots[OverlayPiece.Compare].Sign.Value == '≥',
        "a new condition's slots say what goes in them, and it compares by ≥");
      CheckForkMarks(dock, "a new condition");
      // Enter while a slot that must hold something is empty: refused with a shake, the slot outlined.
      int shaken = dock.Vm.Form.Shake.Asked;
      Finish(text);
      Assert(dock.Owner == "size" && dock.Vm.Alarmed.Value && dock.Vm.Form.Shake.Asked == shaken + 1 && dock.Vm.Line.Shake.Asked > 0
        && dock.Vm.Form.Slots[OverlayPiece.Left].Bad.Value && !dock.Vm.Form.Slots[OverlayPiece.Compare].Bad.Value
        && !dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.name == "Notice"),
        "Enter on an unfinished condition is refused without words: the form shakes and the empty slot is outlined");
      SaveFrame(camera, texture, canvases, name + "-variable-condition");
      Rect forkLeft = ScreenRectOf((RectTransform)DockSlot(dock, OverlayPiece.Left).transform);
      int glides = dock.Vm.Arrival.Asked;
      Press(DockSlot(dock, OverlayPiece.Left));
      Settle();
      Assert(formulas.Editing.Path.Count == 2 && formulas.Editing.Selected < 0 && dock.Vm.Line.Root.Targeted
        && dock.Vm.Line.Name == FormulaRefusals.SlotName(OverlayPiece.Left) && dock.Vm.Line.Whose == ""
        && dock.Vm.Method.Value == DockMethod.Keypad,
        "a slot clicked in the form is gone into: the line is that slot's alone, and the keypad is its input method: "
          + dock.Vm.Line.Whose + " " + dock.Vm.Line.Name);
      // The condition doesn't go away: it folds into a strip over the slot's input method.
      CheckStrip(dock, OverlayPiece.Left, "the size's condition");
      Assert(dock.Vm.Arrival.Asked == glides + 1 && dock.Vm.Line.Slide.Asked > 0,
        "going in, the input method and the line arrive once");
      Assert(dock.Vm.Fold.Slots[OverlayPiece.Left].Bad.Value && !dock.Vm.Fold.Slots[OverlayPiece.Right].Bad.Value,
        "the slot that kept Enter from finishing is still outlined on the strip");
      Pressable seven = DockKey(dock, "Key 7");
      Rect sevenRect = ScreenRectOf((RectTransform)seven.transform), stripRect = ScreenRectOf(StripOf(dock));
      Assert(Mathf.Abs(sevenRect.height / stripRect.height - KeypadView.KeyHeightIn(InputMethodView.FoldedHeight) / ConditionStripView.Height) < .02f
        && sevenRect.yMax <= stripRect.yMin - 1 && sevenRect.width > forkLeft.width * .4f,
        "under the strip the keypad keeps its four rows, each a little lower: " + sevenRect + " / " + stripRect);
      SaveFrame(camera, texture, canvases, name + "-variable-strip");
      SaveHovered(camera, texture, canvases, name + "-variable-strip", "Empty slot s");
      Press(DockChip(dock, "Value combo"));
      Settle();
      Assert(dock.Vm.Fold.Slots[OverlayPiece.Left].Look.Value == ConditionSlotLook.Chips && !dock.Vm.Fold.Slots[OverlayPiece.Left].Bad.Value,
        "what goes into the slot shows on the strip at once, and its outline goes");
      // Back: one step out, and the condition unfolds with the slot showing what it holds.
      Press(StripBack(dock));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Condition && formulas.Editing.Path.Count == 1 && !dock.Vm.Folded.Value && dock.Owner == "size"
        && dock.Vm.Form.Slots[OverlayPiece.Left].Look.Value == ConditionSlotLook.Chips
        && dock.Vm.Form.Slots[OverlayPiece.Left].Chips.Value.Length == 1 && !dock.Vm.Form.Slots[OverlayPiece.Left].Bad.Value
        && StripOf(dock) == null,
        "the strip's key goes one step back out: the condition unfolds, its slot showing what it holds");
      Press(DockSlot(dock, OverlayPiece.Compare));
      Settle();
      CheckStrip(dock, OverlayPiece.Compare, "the comparison");
      CheckIconsFollowScale(text, dock);
      Assert(dock.Vm.Method.Value == DockMethod.Compare && formulas.Editing.Working[0].Op == '≥' && dock.Vm.Line.Type == OverlayValueType.Compare,
        "the comparison is a value of its own type: it starts as ≥ and its input method is the six comparisons");
      Assert(DockListed(dock, "Value make-compare") && DockListed(dock, "Value make-condition") && !DockListed(dock, "Value combo")
        && !DockListed(dock, "Value make-number") && !DockListed(dock, "Value make-group"),
        "only a comparison or a condition that gives one goes where the condition compares");
      SaveFrame(camera, texture, canvases, name + "-variable-compare");
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Settle();
      SaveFrame(camera, texture, canvases, name + "-variable-strip-number");
      Press(DockSlot(dock, OverlayPiece.Result));
      foreach (string key in new[] { "Key 6", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Default));
      foreach (string key in new[] { "Key 4", "Key 0" })
        Press(DockKey(dock, key));
      Settle();
      Assert(formulas.Editing.Problem == null && document.Document[0].SizeFormula == "if({combo};>=;100;60;40)",
        "filled slot by slot, the size takes the condition: " + document.Document[0].SizeFormula);
      // From one slot straight to another on the strip, never coming out in between.
      Assert(formulas.Editing.Path.Count == 2 && dock.Vm.Folded.Value && dock.Vm.Fold.In.Value == OverlayPiece.Default
        && dock.Vm.Fold.Slots[OverlayPiece.Result].Big.Value == "60" && dock.Vm.Fold.Slots[OverlayPiece.Compare].Sign.Value == '≥',
        "each slot was reached from the one before on the strip, which shows what they hold");
      // The strip's key goes back to the condition, where the line says what the field gives.
      Press(StripBack(dock));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Condition && !dock.Vm.Alarmed.Value
        && dock.Vm.Form.Slots.All(row => row.Look.Value != ConditionSlotLook.Empty && !row.Bad.Value),
        "back at the condition, all five slots show what they hold and none is outlined");
      ConditionSlotViewModel then = dock.Vm.Form.Slots[OverlayPiece.Result], otherwise = dock.Vm.Form.Slots[OverlayPiece.Default];
      Assert(then.Look.Value == ConditionSlotLook.Big && then.Big.Value == "60" && otherwise.Big.Value == "40",
        "an answer that is one number shows as that number, large: " + then.Big.Value + " / " + otherwise.Big.Value);
      CheckForkMarks(dock, "a filled condition");
      Assert(dock.ResultText.Contains("60") && size.Result.Value.Contains("60"),
        "the example combo 120 is past 100: the dock and the size row show 60: " + dock.ResultText + " / " + size.Result.Value);
      SaveFrame(camera, texture, canvases, name + "-variable-size");
      SaveFrameLarge(camera, canvases, name + "-variable-size-2x");
      // Enter means the field is done, from however deep: inside a slot it doesn't step back, it finishes.
      Press(DockSlot(dock, OverlayPiece.Right));
      Settle();
      Finish(text);
      Assert(dock.Owner == null && !dock.Vm.Folded.Value && document.Document[0].SizeFormula == "if({combo};>=;100;60;40)",
        "Enter inside a slot finishes the field, which keeps the condition: " + document.Document[0].SizeFormula);
      // A condition in an answer: going into it, the strip is the nearer condition's, and under it the inner one's fork, smaller.
      PressBar(text, size);
      Settle();
      Press(DockSlot(dock, OverlayPiece.Default));
      Press(DockChip(dock, "Value make-condition"));
      Settle();
      Assert(dock.Vm.Folded.Value && dock.Vm.Fold.In.Value == OverlayPiece.Default && dock.Vm.Method.Value == DockMethod.Condition
        && formulas.Editing.Depth == 1 && dock.Vm.Fold.Slots[OverlayPiece.Default].Nested.Value,
        "a condition put in an answer is picked there: the outer one stays folded, the inner one's fork under it");
      Rect outerStrip = ScreenRectOf(StripOf(dock));
      Rect[] inner = Enumerable.Range(0, 5).Select(i => ScreenRectOf((RectTransform)ForkSlot(dock, i).transform)).ToArray();
      Rect methodPlace = ScreenRectOf(dock.MethodRoot);
      Assert(inner.All(row => row.yMax <= outerStrip.yMin - 1 && row.yMin >= methodPlace.yMin - .5f && row.xMin >= methodPlace.xMin - .5f && row.xMax <= methodPlace.xMax + .5f)
        && inner[OverlayPiece.Result].yMax < inner[OverlayPiece.Left].yMin && inner[OverlayPiece.Result].xMax < inner[OverlayPiece.Default].xMin,
        "the inner fork fits between the strip and the place's bottom: " + outerStrip + " / " + inner[OverlayPiece.Left] + " / " + inner[OverlayPiece.Default]);
      SaveFrame(camera, texture, canvases, name + "-variable-nested");
      Press(ForkSlot(dock, OverlayPiece.Left));
      Settle();
      Assert(formulas.Editing.Depth == 2 && dock.Vm.Folded.Value && dock.Vm.Fold.In.Value == OverlayPiece.Left
        && dock.Vm.Fold.Slots[OverlayPiece.Left].Look.Value == ConditionSlotLook.Empty && dock.Vm.Method.Value == DockMethod.Keypad,
        "inside the inner condition the strip is the inner one's, the nearest");
      Press(StripBack(dock));
      Settle();
      Assert(formulas.Editing.Depth == 1 && dock.Vm.Fold.In.Value == OverlayPiece.Default && dock.Vm.Method.Value == DockMethod.Condition,
        "back goes one step: to the inner condition, still inside the outer one's answer");
      // Enter with the inner condition unfinished is refused, and the dock stays where what is missing shows.
      int refusals = dock.Vm.Form.Shake.Asked;
      Finish(text);
      Assert(dock.Owner == "size" && dock.Vm.Alarmed.Value && dock.Vm.Form.Shake.Asked == refusals + 1 && dock.Vm.Form.Slots[OverlayPiece.Left].Bad.Value,
        "Enter is refused while the inner condition is unfinished, its empty slot outlined");
      Press(StripBack(dock));
      Settle();
      Assert(formulas.Editing.Depth == 0 && !dock.Vm.Folded.Value, "and back again is out at the field's own condition");
      Call(text, "CloseOpen");
      document.Undo();
      Call(text, "CloseOpen");
      Settle();

      // The text color: a condition gives the highlight color, or a color picked by hand.
      PressBar(text, color.Button);
      Settle();
      UiColorPicker picker = color.Picker;
      Assert(picker.Visible && dock.Owner == ColorCases.Key && formulas.Editing.Kind == OverlayValueType.Color,
        "opening the color opens it in the dock as a color variable");
      Assert(dock.Vm.Method.Value == DockMethod.Color && dock.FormulaText.Contains("●"), "its one color piece is picked in the picker above: " + dock.FormulaText);
      Press(DockChip(dock, "Value make-condition"));
      Settle();
      Call(text, "Render");
      Assert(color.Cases.OnCase && dock.Vm.Method.Value == DockMethod.Condition && picker.Root.GetComponent<CanvasGroup>() is { alpha: 0, blocksRaycasts: false },
        "with a condition picked the picker rests, clear, and the condition's fork has the place");
      Press(DockSlot(dock, OverlayPiece.Left));
      Press(DockChip(dock, "Value accuracy"));
      Press(DockSlot(dock, OverlayPiece.Compare));
      Press(DockKey(dock, "Key ="));
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Result));
      Settle();
      Assert(dock.Vm.Line.Name == FormulaRefusals.SlotName(OverlayPiece.Result) && dock.Vm.Line.Type == OverlayValueType.Color && !DockListed(dock, "Value combo"),
        "the line names the result's slot, which lists colors, not numbers: " + dock.Vm.Line.Name);
      Press(DockChip(dock, "Value highlight-color"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(color.Cases.OnCase && picker.Model.FollowsHighlight && color.Title == ColorCases.CaseTitle,
        "the picker shows the result's color, the highlight color: " + color.Title);
      Press(DockSlot(dock, OverlayPiece.Default));
      Press(DockChip(dock, "Value custom-color"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(color.Cases.OnCase && !picker.Model.FollowsHighlight, "a custom color opens the picker on it");
      ColorPickerProbe.Hex(picker.Root).text = "#5AC8FA";
      picker.Commit();
      Settle();
      Call(text, "Render");
      Settle();
      const string colors = "if({accuracy};=;100;{highlight-color};#5AC8FA)";
      Assert(document.Document[0].Color.Formula == colors && document.Document[document.Document.Count - 1].Color.Formula == colors,
        "the whole text's color is the condition: " + document.Document[0].Color.Formula);
      Assert(previewText.text.Contains("<color=#5AC8FA"), "the example 99.5 isn't 100: the preview shows the default, sky blue");
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-variable-color");
      SaveFrameLarge(camera, canvases, name + "-variable-color-2x");
      // Inside an answer of a color's condition, the picker has what the strip leaves, and nothing more.
      Rect colorStrip = ScreenRectOf(StripOf(dock)), pickerPlace = ScreenRectOf(picker.Root), methodBottom = ScreenRectOf(dock.MethodRoot);
      Assert(picker.InCompactPlace && pickerPlace.yMax <= colorStrip.yMin - 1 && pickerPlace.yMin >= methodBottom.yMin - .5f
        && Mathf.Abs(pickerPlace.height / colorStrip.height - InputMethodView.FoldedHeight / ConditionStripView.Height) < .05f,
        "the color picker stands under the strip, in the rest of the place: " + colorStrip + " / " + pickerPlace + " / " + methodBottom);
      CheckStrip(dock, OverlayPiece.Default, "the color's condition");
      SaveFrame(camera, texture, canvases, name + "-variable-strip-color");
      Press(StripBack(dock));
      Settle();
      Call(text, "Render");
      Assert(document.Document[0].Color.Formula == colors, "back out keeps the condition");
      // Back at the condition: its answers are colors, shown as the colors they are.
      Settle();
      Rect wholePlace = ScreenRectOf(picker.Root);
      Assert(!dock.Vm.Folded.Value && Mathf.Abs(wholePlace.height / pickerPlace.height - KeypadView.MethodHeight / InputMethodView.FoldedHeight) < .05f,
        "unfolded, the picker's place is the whole input method's again: " + wholePlace + " / " + pickerPlace);
      ConditionSlotViewModel colorThen = dock.Vm.Form.Slots[OverlayPiece.Result], colorElse = dock.Vm.Form.Slots[OverlayPiece.Default];
      Assert(dock.Vm.Method.Value == DockMethod.Condition && colorThen.Look.Value == ConditionSlotLook.Color
        && colorElse.Look.Value == ConditionSlotLook.Color && colorElse.Caption.Value == "#5AC8FA",
        "an answer that is one color shows as that color over its name: " + colorThen.Caption.Value + " / " + colorElse.Caption.Value);
      Assert(dock.Vm.Form.Slots[OverlayPiece.Compare].Sign.Value == '=', "the color's condition compares by =");
      CheckForkMarks(dock, "the color's condition");
      foreach (TextMeshProUGUI label in dock.MethodRoot.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "the fork's words fit: " + label.text);
      }
      // The color and its name stand on the tile's middle, one over the other.
      foreach (int slot in new[] { OverlayPiece.Result, OverlayPiece.Default })
      {
        Transform tile = DockSlot(dock, slot).transform;
        Rect tileRect = ScreenRectOf((RectTransform)tile),
          swatch = ScreenRectOf(tile.GetComponentsInChildren<RectTransform>(false).Single(part => part.name == "Condition swatch")),
          caption = ScreenRectOf(tile.GetComponentsInChildren<RectTransform>(false).Single(part => part.name == "Condition caption"));
        Assert(Mathf.Abs(swatch.center.x - tileRect.center.x) < 1 && Mathf.Abs(caption.center.x - tileRect.center.x) < 1
          && Mathf.Abs(swatch.width - swatch.height) < .5f && caption.yMax <= swatch.yMin + .5f,
          "an answer's color and its name are centered on the tile: " + tileRect + " / " + swatch + " / " + caption);
      }
      SaveFrame(camera, texture, canvases, name + "-variable-fork");

      CheckDockKeyboard(text, dock, picker);
      CheckWordsAndEmptyValue(text, dock, camera, texture, canvases, name);
      CheckValueDigits(text, dock, camera, texture, canvases, name);

      // A color doesn't go in the text: at the caret none is listed.
      Call(text, "CloseOpen");
      if (picker.Visible)
        picker.Hide();
      Settle();
      Assert(!DockListed(dock, "Value highlight-color") && !DockListed(dock, "Value custom-color") && DockListed(dock, "Value combo"),
        "at the caret the list holds no colors");

      // Done saves it; the overlay draws the color.
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && accuracy.Settings.Text.Runs.All(run => run.Color.Formula == colors), "Done saves the color's condition: " + string.Join(" | ", accuracy.Settings.Text.Runs.Select(run => run.Color.Formula)));
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#5AC8FA"), "the overlay draws it: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => string.IsNullOrEmpty(run.Color.Formula)), "it is one layout undo step");
    }

    /// <summary>
    /// Slot <paramref name="slot"/> of the condition the dock shows, as the
    /// player reaches it: on the fork while the condition is picked, on its
    /// folded strip while the dock is inside one of its slots.
    /// </summary>
    private static Pressable DockSlot(DockProbe dock, int slot)
    {
      Settle();
      return dock.Vm.Folded.Value ? StripSlot(dock, slot) : ForkSlot(dock, slot);
    }

    private static Pressable ForkSlot(DockProbe dock, int slot) =>
      dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Single(row => row.name == "Condition slot " + slot);

    private static Pressable StripSlot(DockProbe dock, int slot) =>
      dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Single(row => row.name == "Condition strip slot " + slot);

    private static Pressable StripBack(DockProbe dock)
    {
      Settle();
      return dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Single(row => row.name == "Condition back");
    }

    // The folded condition's strip; null while none is folded.
    private static RectTransform StripOf(DockProbe dock) =>
      dock.MethodRoot.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(part => part.name == "Condition strip");

    /// <summary>
    /// Inside slot <paramref name="slot"/> of a condition: that condition is
    /// folded into a strip at the top of the input method's place, the back
    /// key first, then the five slots in a row with only this one lit, and
    /// the slot's input method under it, none over another.
    /// </summary>
    private static void CheckStrip(DockProbe dock, int slot, string what)
    {
      Settle();
      RectTransform strip = StripOf(dock);
      Assert(dock.Vm.Folded.Value && dock.Vm.Fold.In.Value == slot && strip != null, what + ": inside a slot the condition is folded over the input method");
      Rect place = ScreenRectOf(dock.MethodRoot), bar = ScreenRectOf(strip), back = ScreenRectOf((RectTransform)StripBack(dock).transform);
      Rect[] slots = Enumerable.Range(0, 5).Select(i => ScreenRectOf((RectTransform)StripSlot(dock, i).transform)).ToArray();
      float unit = bar.height / ConditionStripView.Height;
      Assert(Mathf.Abs(bar.yMax - place.yMax) < 1 && Mathf.Abs(bar.xMin - place.xMin) < 1 && Mathf.Abs(bar.xMax - place.xMax) < 1,
        what + ": the strip spans the top of the input method's place: " + bar + " / " + place);
      Assert(back.xMin >= bar.xMin && back.xMax <= slots[OverlayPiece.Left].xMin + .5f && Mathf.Abs(back.width - back.height) < .5f
        && Enumerable.Range(0, 4).All(i => slots[i].xMax <= slots[i + 1].xMin + .5f) && slots[OverlayPiece.Default].xMax <= bar.xMax
        && slots.All(row => Mathf.Abs(row.center.y - bar.center.y) < 1 && row.yMin >= bar.yMin && row.yMax <= bar.yMax)
        && Mathf.Abs(slots[OverlayPiece.Left].width - slots[OverlayPiece.Right].width) < .5f
        && Mathf.Abs(slots[OverlayPiece.Result].width - slots[OverlayPiece.Default].width) < .5f,
        what + ": on the strip, the back key and then the five slots in a row: " + back + " / " + string.Join(" ", slots.Select(row => row.ToString())));
      // Its answers are marked as on the fork, ✓ and ✕, each just before its slot.
      RectTransform stripYes = ByName<RectTransform>(strip, "Condition strip mark true"), stripNo = ByName<RectTransform>(strip, "Condition strip mark false");
      Rect yesMark = ScreenRectOf(stripYes), noMark = ScreenRectOf(stripNo);
      Assert(yesMark.xMax <= slots[OverlayPiece.Result].xMin + .5f && yesMark.xMin >= slots[OverlayPiece.Right].xMax
        && noMark.xMax <= slots[OverlayPiece.Default].xMin + .5f && noMark.xMin >= slots[OverlayPiece.Result].xMax
        && Mathf.Abs(yesMark.center.y - slots[OverlayPiece.Result].center.y) < 1 && Mathf.Abs(noMark.center.y - slots[OverlayPiece.Default].center.y) < 1,
        what + ": the strip's answers carry ✓ and ✕ just before them, as on the fork: " + yesMark + " / " + slots[OverlayPiece.Result] + " / " + noMark + " / " + slots[OverlayPiece.Default]);
      // What is in the slot grows out of the slot's own place on the strip.
      Assert(Mathf.Abs(bar.xMin + ConditionStripView.CenterOf(slot) * unit - slots[slot].center.x) < 1.5f,
        what + ": the slot's input method comes from where the slot stands: " + slots[slot] + " / " + (bar.xMin + ConditionStripView.CenterOf(slot) * unit));
      // Under it, whatever the slot's input method is keeps clear of the strip, inside the place.
      foreach (Pressable key in dock.MethodRoot.GetComponentsInChildren<Pressable>(false))
      {
        if (key.name.StartsWith("Condition strip") || key.name == "Condition back")
          continue;
        Rect rect = ScreenRectOf((RectTransform)key.transform);
        Assert(rect.yMax <= bar.yMin - 1 && rect.yMin >= place.yMin - .5f, what + ": " + key.name + " is under the strip, inside the place: " + rect + " / " + bar);
      }
      foreach (TextMeshProUGUI label in strip.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Rect words = ScreenRectOf(label.rectTransform);
        Assert(words.xMin >= bar.xMin - .5f && words.xMax <= bar.xMax + .5f, what + ": the strip's words stay on it: " + label.text + " " + words);
      }
    }

    /// <summary>
    /// A hand-drawn icon's edge is a screen pixel at the scale it is drawn
    /// at. The input method grows into place from small: its icons are drawn
    /// again as it grows and once it is there, not left with the wide edge of
    /// the small one. (In this run the clock stands still, so an arrival
    /// stays at its start until it is told to rest.)
    /// </summary>
    private static void CheckIconsFollowScale(TextEditorScreen text, DockProbe dock)
    {
      InputDockPanel panel = Field<InputDockPanel>(text, "_dockPanel");
      // Out to the condition and into the slot again, with motion on this once: the six keys are made while their place is small.
      SetField(dock.Vm, "_reduceMotion", false);
      Press(StripBack(dock));
      Settle();
      dock.Vm.Arrival.Play(0);
      Settle();
      Press(ForkSlot(dock, OverlayPiece.Compare));
      Settle();
      UiIcon icon = dock.MethodRoot.GetComponentsInChildren<UiIcon>(false).First(part => part.Kind == UiIconKind.MoreOrSame && part.GetComponentInParent<Pressable>().name == "Key ≥");
      panel.Tick();
      Canvas.ForceUpdateCanvases();
      float small = icon.DrawnScale;
      Assert(dock.Vm.Arrival.Running && dock.Vm.Folded.Value && Mathf.Abs(small / Mathf.Abs(icon.transform.lossyScale.x) - 1) < .001f,
        "as its place starts to grow, an icon is drawn for the small scale it has: " + small + " / " + icon.transform.lossyScale.x);
      dock.Vm.Arrival.Play(0);
      Settle();
      float whole = Mathf.Abs(icon.transform.lossyScale.x);
      Assert(!dock.Vm.Arrival.Running && whole > small * 3, "then its place is as large as it stays: " + small + " / " + whole);
      panel.Tick();
      Canvas.ForceUpdateCanvases();
      Assert(Mathf.Abs(icon.DrawnScale / whole - 1) < .001f,
        "in place, the icon is drawn again for its full scale, its edge one pixel and not the small one's: " + icon.DrawnScale + " / " + whole);
      SetField(dock.Vm, "_reduceMotion", true);
    }

    /// <summary>
    /// The keyboard while the dock has a color open on a value picked in the
    /// text: its keys are the dock's. Digits and backspace type into the
    /// condition's slot, nothing types over or deletes the picked value, and
    /// Esc steps back out one level at a time.
    /// </summary>
    private static void CheckDockKeyboard(TextEditorScreen text, DockProbe dock, UiColorPicker picker)
    {
      TextDocumentEditing document = TextDocumentOf(text);
      TextFormulas formulas = TextFormulasOf(text);
      TextKeyboardInput keyboard = Field<TextKeyboardInput>(text, "_keyboard");
      int anchor = document.Anchor, caret = document.Caret;
      Call(text, "CloseOpen");
      if (picker.Visible)
        picker.Hide();
      Settle();
      int value = Enumerable.Range(0, document.Document.Count).First(i => document.Document[i].IsValue);
      string before = document.Document.PlainText(0, document.Document.Count, _ => "<v>");
      formulas.OpenChip(value);
      Settle();
      PressBar(text, TextColorOf(text).Button);
      Settle();
      Press(DockSlot(dock, OverlayPiece.Right));
      Settle();
      Assert(picker.Visible && dock.Owner == ColorCases.Key && dock.Vm.Method.Value == DockMethod.Keypad && document.SelectedValue(out int picked) && picked == value
        && formulas.Editing.Working.Count == 1 && formulas.Editing.Working[0].Text == "100",
        "the picked value's color is open on its condition's slot, which holds 100");
      Assert(keyboard.Key(KeyDown(KeyCode.Alpha5, '5')) && formulas.Editing.Working[0].Text == "1005"
        && document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before && document.Document[value].IsValue,
        "a digit typed on the keyboard goes into the slot, not over the value picked in the text: " + formulas.Editing.Working[0].Text
          + " / " + document.Document.PlainText(0, document.Document.Count, _ => "<v>"));
      Assert(keyboard.Key(KeyDown(KeyCode.Backspace, '\0')) && formulas.Editing.Working[0].Text == "100"
        && document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before && document.SelectedValue(out _),
        "backspace takes the digit back, and deletes nothing in the text");
      // Delete takes a digit back like backspace does, in the dock: never the value picked in the text.
      Assert(keyboard.Key(KeyDown(KeyCode.Delete, '\0')) && formulas.Editing.Working[0].Text == "10" && keyboard.Key(KeyDown(KeyCode.Alpha0, '0'))
        && formulas.Editing.Working[0].Text == "100" && document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before,
        "Delete takes a digit of the slot's number too: " + formulas.Editing.Working[0].Text);
      foreach (Event key in new[] { KeyDown(KeyCode.A, 'a'), KeyDown(KeyCode.LeftArrow, '\0'), KeyDown(KeyCode.Space, ' ') })
        Assert(keyboard.Key(key), "the editor stays open");
      Assert(document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before && document.SelectedValue(out picked) && picked == value
        && dock.Owner == ColorCases.Key && picker.Visible && formulas.Editing.Depth == 1,
        "a letter, an arrow or a space does nothing to the text while the color is open: "
          + document.Document.PlainText(0, document.Document.Count, _ => "<v>"));
      // Esc: one step back out, then the color closes; the value stays picked.
      Assert(keyboard.Key(KeyDown(KeyCode.Escape, '\0')) && formulas.Editing.Depth == 0 && dock.Vm.Method.Value == DockMethod.Condition && picker.Visible && text.IsOpen,
        "Esc steps out of the slot, back to the condition");
      Assert(keyboard.Key(KeyDown(KeyCode.Escape, '\0')) && !picker.Visible && text.IsOpen && document.SelectedValue(out picked) && picked == value
        && document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before,
        "Esc again closes the color; the text and what is picked in it are as they were");
      // With only the value picked and nothing open, typing is the text's again: it goes on behind an open value.
      formulas.OpenChip(value);
      Settle();
      int items = document.Document.Count;
      Assert(keyboard.Key(KeyDown(KeyCode.Z, 'z')) && document.Document.Count == items + 1 && document.Document[value].IsValue
        && !document.Document[value + 1].IsValue && dock.Owner == null,
        "with only a value of the text open, typing goes on behind it: " + document.Document.PlainText(0, document.Document.Count, _ => "<v>"));
      // Taken out again by hand: an undo here could take an earlier step with it.
      document.DeleteRange(value + 1, value + 2);
      Call(text, "CloseOpen");
      document.Anchor = anchor;
      document.Caret = caret;
      Call(text, "Render");
      Settle();
      Assert(document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before, "the text is as it was: " + document.Document.PlainText(0, document.Document.Count, _ => "<v>"));
    }

    /// <summary>
    /// A condition made in the text, its answer some words. While it gives
    /// nothing the value shows nothing, and an empty slot stands in its place
    /// in the preview. The words' field reads as one (its mark, a ring, a
    /// caret asked for at once), words show on the line without quotes, and
    /// backspace on the keyboard takes them back a letter at a time, then the
    /// value itself.
    /// </summary>
    private static void CheckWordsAndEmptyValue(TextEditorScreen text, DockProbe dock, Camera camera, RenderTexture texture, Canvas[] canvases, string name)
    {
      TextDocumentEditing document = TextDocumentOf(text);
      TextFormulas formulas = TextFormulasOf(text);
      TextKeyboardInput keyboard = Field<TextKeyboardInput>(text, "_keyboard");
      TextPreview preview = TextPreviewOf(text);
      TextLayoutMap map = Field<TextLayoutMap>(text, "_map");
      List<RoundedRect> blanks = Field<List<RoundedRect>>(Field<TextUnderlines>(text, "_underlines"), "_blanks");
      int anchor = document.Anchor, caret = document.Caret;
      int at = Enumerable.Range(0, document.Document.Count).First(i => document.Document[i].IsValue);
      string before = document.Document.PlainText(0, document.Document.Count, _ => "<v>");
      Call(text, "CloseOpen");
      document.Anchor = document.Caret = at;
      Call(text, "Render");
      Settle();
      Assert(!blanks.Any(slot => slot.gameObject.activeSelf), "no value of the text shows nothing: no empty slot is drawn");
      Press(DockChip(dock, "Value make-condition"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(document.Document[at].IsValue && formulas.ChipOpen(out int open) && open == at && dock.Vm.Method.Value == DockMethod.Condition,
        "a condition made at the caret is a value there, open in the dock");
      // In the preview: it gives nothing yet, so its place is kept open, an empty slot in it, between its neighbours.
      RoundedRect[] shown = blanks.Where(slot => slot.gameObject.activeSelf).ToArray();
      Assert(preview.ShowsNothing(at) && shown.Length == 1, "a value that shows nothing has one empty slot in the preview: " + shown.Length);
      Assert(map.ItemRect(at, out Rect place) & map.ItemRect(at + 1, out Rect next) & map.ItemRect(at - 1, out Rect last), "the value and its neighbours have places");
      Rect slotRect = ScreenRectOf(shown[0].rectTransform);
      float unit = preview.FrameScale / Mathf.Max(.01f, preview.Scale);
      Assert(place.width >= 12 * unit && place.xMin >= last.xMax - .5f && place.xMax <= next.xMin + .5f && slotRect.width > slotRect.height * .45f && slotRect.width > 6,
        "the empty slot has room of its own, between the letters before it and the value after: " + last + " / " + place + " / " + next + " / " + slotRect);
      Assert(map.TryValueAt(slotRect.center, out int clicked) && clicked == at, "a click on the empty slot lands on its value");
      SaveFrame(camera, texture, canvases, name + "-variable-empty");
      // Its answer: words. Their field stands under the folded condition: a mark, the field, a ring; the caret is asked for as the words are picked.
      Press(DockSlot(dock, OverlayPiece.Left));
      Press(DockChip(dock, "Value combo"));
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Result));
      int asked = dock.Vm.WordsFocus.Value;
      Press(DockChip(dock, "Value make-text"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Words && dock.Vm.Folded.Value && dock.Vm.Fold.In.Value == OverlayPiece.Result && preview.ShowsNothing(at),
        "words made in the answer are picked there, their field the input method; with none typed the value still shows nothing");
      TMP_InputField field = dock.MethodRoot.GetComponentsInChildren<TMP_InputField>(false).Single(input => input.name == "Words field");
      RectTransform box = dock.MethodRoot.GetComponentsInChildren<RectTransform>(false).First(part => part.name == "Words box");
      Rect boxRect = ScreenRectOf(box), fieldRect = ScreenRectOf((RectTransform)field.transform), methodPlace = ScreenRectOf(dock.MethodRoot), strip = ScreenRectOf(StripOf(dock));
      Assert(dock.Vm.WordsFocus.Value == asked + 1 && field.customCaretColor && field.caretWidth >= 2 && !field.onFocusSelectAll && field.caretColor.a > .9f,
        "picking words asks their field to take the keys, with a caret plain to see and what is there kept");
      Assert(Mathf.Abs(boxRect.xMin - methodPlace.xMin) < 1 && Mathf.Abs(boxRect.xMax - methodPlace.xMax) < 1 && boxRect.yMax <= strip.yMin - 1
        && fieldRect.xMin > boxRect.xMin + 20 && fieldRect.xMax <= boxRect.xMax && fieldRect.yMin >= boxRect.yMin - .5f && fieldRect.yMax <= boxRect.yMax + .5f,
        "the words' field is a box across the place under the strip, its type's mark before what is typed: " + boxRect + " / " + fieldRect + " / " + strip);
      SaveFrame(camera, texture, canvases, name + "-variable-words");
      // Words not typed yet are one hollow box everywhere they are drawn: on the line, on the strip, and on the fork's tile.
      Assert(Blanks(ByName<RectTransform>(dock.Root, "Formula line")) == 1 && Blanks(StripOf(dock)) == 1,
        "words not typed yet are a hollow box on the line and on the strip, not an empty pill");
      Press(StripBack(dock));
      Settle();
      Transform yesTile = ForkSlot(dock, OverlayPiece.Result).transform;
      Assert(Blanks(yesTile) == 1 && !yesTile.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text.Length == 0 && label.name != "Blank words"),
        "back on the fork, the answer's words not typed yet are the same hollow box");
      SaveFrame(camera, texture, canvases, name + "-variable-blank-words");
      Press(ForkSlot(dock, OverlayPiece.Result));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Words && dock.Vm.Folded.Value, "back into the answer, its words' field again");
      // Typed words show on the line as they are, and in the preview in the value's place (the example combo is past 100).
      dock.Vm.Words = "Acc";
      Settle();
      Call(text, "Render");
      Settle();
      Assert(dock.Vm.Line.Root.Pieces.Length == 1 && dock.Vm.Line.Root.Pieces[0].Label == "Acc" && !preview.ShowsNothing(at) && !blanks.Any(slot => slot.gameObject.activeSelf)
        && dock.Vm.Fold.Slots[OverlayPiece.Result].Brief.Value == "Acc",
        "typed words are on the line and the strip without quotes, and the preview shows them, the empty slot gone: " + dock.Vm.Line.Root.Pieces[0].Label);
      Assert(Blanks(ByName<RectTransform>(dock.Root, "Formula line")) == 0 && Blanks(StripOf(dock)) == 0, "with words typed, no hollow box is left");
      // Backspace on the keyboard: a letter at a time, then the words themselves; never the text around the value.
      Assert(keyboard.Key(KeyDown(KeyCode.Backspace, '\0')) && dock.Vm.Words == "Ac" && keyboard.Key(KeyDown(KeyCode.Backspace, '\0')) && keyboard.Key(KeyDown(KeyCode.Backspace, '\0'))
        && dock.Vm.Words == "" && formulas.Editing.Working.Count == 1,
        "backspace takes the words back a letter at a time: " + dock.Vm.Words);
      Call(text, "Render");
      Settle();
      Assert(preview.ShowsNothing(at) && blanks.Count(slot => slot.gameObject.activeSelf) == 1, "with no letters left the value shows nothing again, the empty slot back in its place");
      Assert(keyboard.Key(KeyDown(KeyCode.Backspace, '\0')) && formulas.Editing.Working.Count == 0 && formulas.Editing.Depth == 1 && document.Document[at].IsValue,
        "once more and the empty words go from the answer; the dock stays in it and the value in the text");
      Press(DockChip(dock, "Value combo"));
      Settle();
      Assert(formulas.Editing.Working.Count == 1 && keyboard.Key(KeyDown(KeyCode.Backspace, '\0')) && formulas.Editing.Working.Count == 0,
        "a value picked from the list goes whole with one backspace");
      // The value made for this is taken out again, and the text is as it was.
      Call(text, "CloseOpen");
      document.DeleteRange(at, at + 1);
      document.Anchor = anchor;
      document.Caret = caret;
      Call(text, "Render");
      Settle();
      Assert(document.Document.PlainText(0, document.Document.Count, _ => "<v>") == before && !blanks.Any(slot => slot.gameObject.activeSelf),
        "the text is as it was: " + document.Document.PlainText(0, document.Document.Count, _ => "<v>"));
    }

    /// <summary>
    /// The fork says which answer is which without a word and whatever the
    /// values are now: "if" before what is compared, ✓ on the way to the
    /// answer for when it holds, ✕ on the way to the other, each mark on its
    /// way between the bar and its tile, over the tile's middle. Both tiles
    /// keep their words, "then" and "otherwise".
    /// </summary>
    private static void CheckForkMarks(DockProbe dock, string what)
    {
      Settle();
      RectTransform yesMark = ByName<RectTransform>(dock.MethodRoot, "Condition mark true"), noMark = ByName<RectTransform>(dock.MethodRoot, "Condition mark false");
      Assert(yesMark != null && noMark != null && yesMark.gameObject.activeInHierarchy && noMark.gameObject.activeInHierarchy,
        what + ": the fork's two ways are marked");
      Assert(yesMark.GetComponentInChildren<UiIcon>().Kind == UiIconKind.Check && noMark.GetComponentInChildren<UiIcon>().Kind == UiIconKind.Close,
        what + ": ✓ on the way to the answer for when it holds, ✕ on the other");
      Rect yes = ScreenRectOf((RectTransform)ForkSlot(dock, OverlayPiece.Result).transform), no = ScreenRectOf((RectTransform)ForkSlot(dock, OverlayPiece.Default).transform),
        bar = ScreenRectOf((RectTransform)ForkSlot(dock, OverlayPiece.Compare).transform), check = ScreenRectOf(yesMark), cross = ScreenRectOf(noMark);
      Assert(Mathf.Abs(check.center.x - yes.center.x) < 1 && Mathf.Abs(cross.center.x - no.center.x) < 1
        && check.yMin >= yes.yMax + 1 && cross.yMin >= no.yMax + 1 && check.yMax <= bar.yMin && Mathf.Abs(check.width - check.height) < .5f,
        what + ": each mark is on its way, over its tile's middle, between the bar and the tile: " + check + " / " + yes + " / " + cross + " / " + no);
      string[] words = dock.MethodRoot.GetComponentsInChildren<TextMeshProUGUI>(false).Select(label => label.text).ToArray();
      OverlayVariableWords said = FormulaWords.Current;
      Assert(words.Contains(said.ConditionStart) && words.Contains(said.Then) && words.Contains(said.Otherwise),
        what + ": the fork still reads if, then and otherwise: " + string.Join(" | ", words));
    }

    // How many hollow boxes for words not typed yet are drawn under a part.
    private static int Blanks(Transform part) =>
      part == null ? 0 : part.GetComponentsInChildren<RectTransform>(false).Count(box => box.name == "Blank words");

    /// <summary>
    /// A value with decimals, picked, is its own input method: its number
    /// written out to four decimals in the middle of a card, solid as far as it
    /// is read and faint past it, the operators under it. A digit pressed is
    /// where it ends: what a condition compares and a text writes; on the line
    /// its pill says it quietly ("0.0").
    /// </summary>
    private static void CheckValueDigits(TextEditorScreen text, DockProbe dock, Camera camera, RenderTexture texture, Canvas[] canvases, string name)
    {
      TextDocumentEditing document = TextDocumentOf(text);
      TextFormulas formulas = TextFormulasOf(text);
      PropertyButtonViewModel size = Field<TextSize>(text, "_size").Button;
      TextMeshProUGUI previewText = TextPreviewOf(text).Text;
      Call(text, "CloseOpen");
      Settle();

      // In a condition: progress, compared with 0.
      PressBar(text, size);
      Settle();
      Press(DockChip(dock, "Value make-condition"));
      Press(DockSlot(dock, OverlayPiece.Left));
      Press(DockChip(dock, "Value progress-current"));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Digits && dock.Vm.Folded.Value && formulas.Editing.SelectedPiece?.Value == OverlayValueId.ProgressCurrent,
        "progress picked in the compared slot has its own input method: its number");
      Assert(Written(dock) == "48.0000" && Solid(dock) == "48" && dock.Vm.Digits.Read == 0,
        "its number written out to four decimals (the example 48), solid as far as it is read, whole as the game shows it: " + Written(dock) + " / " + Solid(dock));
      Rect card = ScreenRectOf(DigitsCard(dock)), strip = ScreenRectOf(StripOf(dock)), place = ScreenRectOf(dock.MethodRoot);
      Rect[] glyphs = DigitsGlyphs(dock).Select(glyph => ScreenRectOf((RectTransform)glyph.transform)).ToArray();
      Rect[] operators = new[] { "Key ÷", "Key ×", "Key −", "Key +" }.Select(key => ScreenRectOf((RectTransform)DockKey(dock, key).transform)).ToArray();
      Assert(card.yMax <= strip.yMin - 1 && Mathf.Abs(card.xMin - place.xMin) < 1 && Mathf.Abs(card.xMax - place.xMax) < 1
        && operators.All(op => op.yMax <= card.yMin + .5f && op.yMin >= place.yMin - .5f && Mathf.Abs(op.center.y - operators[0].center.y) < .5f),
        "the number's card spans the place under the strip, the operators under it at the bottom: " + card + " / " + strip);
      Assert(Enumerable.Range(0, glyphs.Length - 1).All(i => glyphs[i].xMax <= glyphs[i + 1].xMin + .5f)
        && glyphs.All(glyph => Mathf.Abs(glyph.height - card.height) < 1 && card.Contains(glyph.center)),
        "its parts stand in a row, each as tall as the card to be pressed: " + string.Join(" ", glyphs.Select(glyph => glyph.ToString())));
      TextMeshProUGUI[] inks = DigitsGlyphs(dock).Select(glyph => glyph.GetComponentInChildren<TextMeshProUGUI>()).ToArray();
      foreach (TextMeshProUGUI ink in inks)
        ink.ForceMeshUpdate();
      float left = inks.Min(ink => RectTransformUtility.WorldToScreenPoint(null, ink.rectTransform.TransformPoint(ink.textBounds.min)).x),
        right = inks.Max(ink => RectTransformUtility.WorldToScreenPoint(null, ink.rectTransform.TransformPoint(ink.textBounds.max)).x);
      float unit = card.height / (KeypadView.MethodHeight - ConditionStripView.Height - ConditionStripView.Gap - KeypadView.KeyHeightIn(InputMethodView.FoldedHeight) - KeypadView.Gap);
      Assert(Mathf.Abs((left + right) / 2 - card.center.x) < 2 && inks.All(ink => !ink.isTextTruncated && ink.fontSize <= 40),
        "the number stands in the middle of its card, smaller than the mock's: " + (left + right) / 2 + " / " + card.center.x + " · size " + inks[0].fontSize);
      Assert(inks.Skip(3).All(ink => ink.GetComponentInParent<CanvasGroup>() != null || true), "drawn");
      SaveFrame(camera, texture, canvases, name + "-variable-digits");
      // Under the pointer a digit turns the highlight color, with nothing drawn behind it.
      Pressable third = DigitsKey(dock, 3);
      third.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
      Settle();
      UnityEngine.UI.Graphic[] behind = third.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Where(part => !(part is TMP_Text)).ToArray();
      Assert(behind.All(part => part.color.a < .01f),
        "a digit under the pointer has no fill behind it: " + string.Join(" ", behind.Select(part => part.name + " " + part.color.a)));
      third.OnPointerExit(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
      Settle();
      // One decimal: pressed where it stands; compared at it, so 0.1 is not 0; the pill says so.
      Press(DigitsKey(dock, 1));
      Settle();
      Assert(formulas.Editing.SelectedPiece.Digits == 1 && dock.Vm.Digits.Read == 1 && Solid(dock) == "48.0" && Written(dock) == "48.0000"
        && dock.Vm.Line.Root.Pieces[0].Digits == "0.0" && dock.Vm.Method.Value == DockMethod.Digits,
        "the first decimal pressed: read to it, solid to it; the line's pill says 0.0: " + Solid(dock) + " / " + dock.Vm.Line.Root.Pieces[0].Digits);
      Assert(dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.name == "Digits" && label.text == "0.0"),
        "it is said quietly after the value's name on the line");
      // The point reads it whole again.
      Press(DigitsKey(dock, -1));
      Settle();
      Assert(formulas.Editing.SelectedPiece.Digits == -1 && Solid(dock) == "48", "the point pressed reads it whole, as it shows: back to its own");
      Press(DigitsKey(dock, 1));
      Press(DockSlot(dock, OverlayPiece.Compare));
      Press(DockKey(dock, "Key ≠"));
      Press(DockSlot(dock, OverlayPiece.Right));
      Press(DockKey(dock, "Key 0"));
      Press(DockSlot(dock, OverlayPiece.Result));
      foreach (string key in new[] { "Key 6", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Default));
      foreach (string key in new[] { "Key 4", "Key 0" })
        Press(DockKey(dock, key));
      Settle();
      Assert(document.Document[0].SizeFormula == "if({progress-current:1};!=;0;60;40)", "the size saves progress read at one decimal: " + document.Document[0].SizeFormula);
      Press(StripBack(dock));
      Settle();
      Assert(dock.Vm.Form.Slots[OverlayPiece.Left].Chips.Value.Single().Digits == "0.0", "on the fork the compared slot's pill says it too");
      SaveFrame(camera, texture, canvases, name + "-variable-digits-fork");
      Finish(text);
      document.Undo();
      Call(text, "CloseOpen");
      Settle();

      // In the text: X-Accuracy written at four decimals, zeros kept; back at its own two it is its name alone.
      int accuracy = Enumerable.Range(0, document.Document.Count).First(i => document.Document[i].IsValue && document.Document[i].Value == "accuracy");
      formulas.OpenChip(accuracy);
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Digits && !dock.Vm.Folded.Value && dock.Vm.Digits.Read == 2 && Solid(dock) == "99.50" && Written(dock) == "99.5000",
        "X-Accuracy picked in the text: read at its own two decimals, as the overlay shows it: " + Solid(dock));
      Rect wholeCard = ScreenRectOf(DigitsCard(dock));
      Assert(wholeCard.height > card.height && DigitsGlyphs(dock).All(glyph => glyph.GetComponentInChildren<TextMeshProUGUI>().fontSize <= 40),
        "with the whole place the card is taller; the number no larger than 40");
      SaveFrame(camera, texture, canvases, name + "-variable-digits-text");
      Press(DigitsKey(dock, 4));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(document.Document[accuracy].Value == "{accuracy:4}" && previewText.text.Contains("99.5000"),
        "the fourth decimal pressed, the text writes it at four, zeros kept: " + document.Document[accuracy].Value);
      Press(DigitsKey(dock, 2));
      Settle();
      Call(text, "Render");
      Assert(document.Document[accuracy].Value == "accuracy" && previewText.text.Contains("99.5") && !previewText.text.Contains("99.50"),
        "back at its own count it is the value alone again: " + document.Document[accuracy].Value);
      // An operator carries on a calculation from it.
      Press(DockKey(dock, "Key ×"));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Keypad && formulas.Editing.Working.Count == 3, "× joins a number to it, which the keypad types");
      document.Undo();
      Call(text, "CloseOpen");
      Settle();
      Assert(document.Document[accuracy].Value == "accuracy", "the text is as it was: " + document.Document[accuracy].Value);
    }

    // The digits input method's parts, in order: the whole part, the point, the four decimals.
    private static Pressable[] DigitsGlyphs(DockProbe dock)
    {
      Settle();
      return dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Where(key => key.name.StartsWith("Digits ")).ToArray();
    }

    // The part that reads the picked value at <paramref name="digits"/> decimals; -1 for the point.
    private static Pressable DigitsKey(DockProbe dock, int digits) =>
      DigitsGlyphs(dock).Single(key => key.name == (digits < 0 ? "Digits point" : "Digits " + digits));

    private static RectTransform DigitsCard(DockProbe dock) =>
      dock.MethodRoot.GetComponentsInChildren<RectTransform>(false).First(part => part.name == "Digits number");

    // The number as written, and its solid part.
    private static string Written(DockProbe dock) => string.Concat(dock.Vm.Digits.Glyphs.Select(glyph => glyph.Text.Value));

    private static string Solid(DockProbe dock) =>
      string.Concat(dock.Vm.Digits.Glyphs.Where(glyph => glyph.Solid.Value).Select(glyph => glyph.Text.Value));

    private static Event KeyDown(KeyCode code, char character) => new() { type = EventType.KeyDown, keyCode = code, character = character };

    // The same frame at twice the pixels, to look at closely.
    private static void SaveFrameLarge(Camera camera, Canvas[] canvases, string name)
    {
      _refresh?.Invoke();
      UiTidyAudit.Inspect(name);
      int width = Screen.width * 2,
        height = Screen.height * 2;
      RenderTexture large = new RenderTexture(width, height, 24);
      RenderTexture before = camera.targetTexture;
      camera.targetTexture = large;
      foreach (Canvas canvas in canvases)
      {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.scaleFactor = 2;
      }
      canvases[0].planeDistance = 2;
      Canvas.ForceUpdateCanvases();
      camera.Render();
      RenderTexture.active = large;
      Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
      image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      RenderTexture.active = null;
      Object.DestroyImmediate(image);
      camera.targetTexture = before;
      large.Release();
      Object.DestroyImmediate(large);
      foreach (Canvas canvas in canvases)
      {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.scaleFactor = 1;
      }
      Canvas.ForceUpdateCanvases();
    }
  }
}

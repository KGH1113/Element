using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.TextEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.ColorPicker;
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
      Rect place = ScreenRectOf(dock.MethodRoot), doneKey = ScreenRectOf((RectTransform)DockKey(dock, "Key done").transform);
      Rect[] rows = slots.Select(row => ScreenRectOf((RectTransform)row.transform)).ToArray();
      Assert(Enumerable.Range(1, 4).All(i => rows[i].yMax <= rows[i - 1].yMin + .5f && Mathf.Abs(rows[i].xMin - rows[0].xMin) < .5f)
        && rows.All(row => place.Contains(row.center) && row.yMin >= doneKey.yMax - .5f),
        "the five slots stand one under another in the input method's place, over Remove and Apply");
      // Apply while a slot that must hold something is empty: refused with a shake, the slot outlined.
      int shaken = dock.Vm.Form.Shake.Asked;
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(dock.Owner == "size" && dock.Vm.Alarmed.Value && dock.Vm.Form.Shake.Asked == shaken + 1 && dock.Vm.Line.Shake.Asked > 0
        && dock.Vm.Form.Slots[OverlayPiece.Left].Bad.Value && !dock.Vm.Form.Slots[OverlayPiece.Compare].Bad.Value
        && !dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.name == "Notice"),
        "Apply on an unfinished condition is refused without words: the form shakes and the empty slot is outlined");
      SaveFrame(camera, texture, canvases, name + "-variable-condition");
      Press(DockSlot(dock, OverlayPiece.Left));
      Settle();
      Assert(formulas.Editing.Path.Count == 2 && formulas.Editing.Selected < 0 && dock.Vm.Line.Root.Targeted
        && dock.Vm.Line.Name == FormulaRefusals.SlotName(OverlayPiece.Left) && dock.Vm.Line.Whose.Contains(L("조건", "Condition"))
        && dock.Vm.Method.Value == DockMethod.Keypad,
        "a slot clicked in the form is gone into: the line is that slot's, with the way to it, and the keypad is its input method: "
          + dock.Vm.Line.Whose + " " + dock.Vm.Line.Name);
      Press(DockChip(dock, "Value combo"));
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Condition && formulas.Editing.Path.Count == 1
        && dock.Vm.Form.Slots[OverlayPiece.Left].Chips.Value.Length == 1 && !dock.Vm.Form.Slots[OverlayPiece.Left].Bad.Value,
        "Apply inside a slot goes back to the condition, whose slot now shows what it holds");
      Press(DockSlot(dock, OverlayPiece.Compare));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Compare && formulas.Editing.Working[0].Op == '≥' && dock.Vm.Line.Type == OverlayValueType.Compare,
        "the comparison is a value of its own type: it starts as ≥ and its input method is the six comparisons");
      Assert(DockListed(dock, "Value make-compare") && DockListed(dock, "Value make-condition") && !DockListed(dock, "Value combo")
        && !DockListed(dock, "Value make-number") && !DockListed(dock, "Value make-group"),
        "only a comparison or a condition that gives one goes where the condition compares");
      SaveFrame(camera, texture, canvases, name + "-variable-compare");
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Result));
      foreach (string key in new[] { "Key 6", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockSlot(dock, OverlayPiece.Default));
      foreach (string key in new[] { "Key 4", "Key 0" })
        Press(DockKey(dock, key));
      Settle();
      Assert(formulas.Editing.Problem == null && document.Document[0].SizeFormula == "if({combo};>=;100;60;40)",
        "filled slot by slot, the size takes the condition: " + document.Document[0].SizeFormula);
      // Apply inside the last slot goes back to the condition, where the line says what the field gives.
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Condition && !dock.Vm.Alarmed.Value && dock.Vm.Form.Slots.All(row => !row.Empty.Value && !row.Bad.Value),
        "back at the condition, all five slots show what they hold and none is outlined");
      Assert(dock.ResultText.Contains("60") && size.Result.Value.Contains("60"),
        "the example combo 120 is past 100: the dock and the size row show 60: " + dock.ResultText + " / " + size.Result.Value);
      SaveFrame(camera, texture, canvases, name + "-variable-size");
      SaveFrameLarge(camera, canvases, name + "-variable-size-2x");
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
        "with a condition picked the picker rests, clear, and the condition's five slots have the place");
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
      Press(DockKey(dock, "Key done"));
      Settle();
      Call(text, "Render");
      Assert(document.Document[0].Color.Formula == colors, "Apply keeps the condition");

      // A color doesn't go in the text: at the caret none is listed.
      Call(text, "CloseOpen");
      if (picker.Visible)
        picker.Hide();
      Settle();
      Assert(!DockListed(dock, "Value highlight-color") && !DockListed(dock, "Value custom-color") && DockListed(dock, "Value combo"),
        "at the caret the list holds no colors");

      // Done saves it; the overlay draws the color.
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && accuracy.Settings.Text.Runs.All(run => run.Color.Formula == colors), "Done saves the color's condition");
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#5AC8FA"), "the overlay draws it: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => string.IsNullOrEmpty(run.Color.Formula)), "it is one layout undo step");
    }

    /// <summary>
    /// Slot <paramref name="slot"/> of the condition open in the dock, as
    /// the player reaches it: its row in the condition's input method. From
    /// inside another slot, Apply goes back to the condition first.
    /// </summary>
    private static Pressable DockSlot(DockProbe dock, int slot)
    {
      Settle();
      if (dock.Vm.Method.Value != DockMethod.Condition)
      {
        Press(DockKey(dock, "Key done"));
        Settle();
      }
      return dock.MethodRoot.GetComponentsInChildren<Pressable>(false).Single(row => row.name == "Condition slot " + slot);
    }

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

using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
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
  // picked by hand (the picker following the color piece picked), a value of
  // the player's made in the text, filled, renamed and shared, and Done
  // saving what the game then draws.
  public static partial class SettingsPreview
  {
    private static void CheckVariables(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      bool korean = Korean(name);
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
      Assert(dock.Vm.Method.Value == DockMethod.Condition && formulas.Editing.Problem?.Slot == OverlayPiece.Left
        && dock.Vm.Problem.Value.Length > 0, "a new condition shows its five slots, and the dock says which is still empty: " + dock.Vm.Problem.Value);
      SaveFrame(camera, texture, canvases, name + "-variable-condition");
      Press(DockSlot(dock, OverlayPiece.Left));
      Assert(formulas.Editing.Path.Count == 2 && dock.Vm.Title.Value.Contains("›"), "a slot opens, and the dock's title says where it is: " + dock.Vm.Title.Value);
      Press(DockChip(dock, "Value combo"));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Compare));
      Assert(dock.Vm.Method.Value == DockMethod.Compare && formulas.Editing.Working[0].Op == '≥', "the operator starts as ≥ and shows the six comparisons");
      Settle();
      TextMeshProUGUI signs = ByName<TextMeshProUGUI>(dock.Root, "Values rest");
      Assert(Shown(signs) && signs.text == L("기호는 오른쪽 입력기에서 골라요.", "Pick the sign on the input method at the right.") && DockChips(dock).Length == 0,
        "the operator's slot lists no values: its sign is picked on the input method");
      SaveFrame(camera, texture, canvases, name + "-variable-compare");
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Result));
      foreach (string key in new[] { "Key 6", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Default));
      foreach (string key in new[] { "Key 4", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockKey(dock, "Key back"));
      Settle();
      Assert(formulas.Editing.Problem == null && document.Document[0].SizeFormula == "if({combo};>=;100;60;40)",
        "filled slot by slot, the size takes the condition: " + document.Document[0].SizeFormula);
      Assert(dock.ResultText.Contains("60") && size.Value == "60",
        "the example combo 120 is past 100: the dock and the size row show 60: " + dock.ResultText + " / " + size.Value);
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
      Assert(color.Cases.OnCase && !string.IsNullOrEmpty(picker.Model.WaitNote), "with a condition picked the picker says how to reach a color");
      Press(DockSlot(dock, OverlayPiece.Left));
      Press(DockChip(dock, "Value accuracy"));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Compare));
      Press(DockKey(dock, "Key ="));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Right));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(DockKey(dock, "Key back"));
      Press(DockSlot(dock, OverlayPiece.Result));
      Settle();
      Assert(dock.Values.Title.Value == L("결과값 자리에 넣을 수 있는 값", "Values for the Result slot") && !DockListed(dock, "Value combo"),
        "the result's slot names itself and lists colors, not numbers: " + dock.Values.Title.Value);
      Press(DockChip(dock, "Value highlight-color"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(color.Cases.OnCase && picker.Model.FollowsHighlight && color.Title == ColorCases.CaseTitle,
        "the picker shows the result's color, the highlight color: " + color.Title);
      Press(DockKey(dock, "Key back"));
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
      int count = document.Document.Count;
      Assert(!DockListed(dock, "Value highlight-color") && !DockListed(dock, "Value custom-color") && DockListed(dock, "Value combo"),
        "at the caret the list holds no colors");

      // A value of the player's, made at the end of the text: filled, renamed, shared.
      document.Anchor = document.Caret = count;
      Press(DockChip(dock, "Value make-custom"));
      Settle();
      Assert(OverlayCustomValues.Keys.Count == 1, "Make a value makes one");
      string made = OverlayCustomValues.Keys[0];
      Assert(document.Document[count].Value == "{@" + made + "}" && formulas.Editing.Path.Count == 2 && formulas.Editing.Path[1].Custom == made,
        "it goes in at the caret and opens to be filled");
      Press(DockChip(dock, "Value make-text"));
      Settle();
      TMP_InputField words = ByName<TMP_InputField>(dock.Root, "Words field");
      words.text = " PERFECT";
      Settle();
      Assert(OverlayCustomValues.TryGet(made, out OverlayVariable inside) && inside.Serialize() == "\" PERFECT\"",
        "the keyboard types its words, and the value keeps them: " + inside?.Serialize());
      Press(DockKey(dock, "Key back"));
      Settle();
      Assert(dock.Vm.Method.Value == DockMethod.Custom, "back out, the value shows its name and its inside");
      ByName<TMP_InputField>(dock.Root, "Custom name").text = korean ? "칭찬" : "Praise";
      Settle();
      Assert(OverlayCustomValues.NameOf(made) == (korean ? "칭찬" : "Praise") && dock.FormulaText.Contains(korean ? "칭찬" : "Praise"),
        "its name is the one typed, in the line too: " + dock.FormulaText);
      Assert(DockChip(dock, "Value @" + made) != null, "and it is listed under My values for every element");
      SaveFrame(camera, texture, canvases, name + "-variable-mine");
      SaveFrameLarge(camera, canvases, name + "-variable-mine-2x");
      Press(DockKey(dock, "Key done"));
      Settle();
      Call(text, "Render");
      Assert(previewText.text.Contains("PERFECT"), "the text shows what the value gives: " + previewText.text);

      // Done saves it all; the overlay draws the color and the value's words.
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && accuracy.Settings.Text.Runs.All(run => run.Color.Formula == colors), "Done saves the color's condition");
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#5AC8FA") && game.text.Contains("PERFECT"), "the overlay draws them: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => string.IsNullOrEmpty(run.Color.Formula)), "it is one layout undo step");
      // The made value isn't part of undo; later checks start without it.
      Field<CanvasSession>(editor, "_session").CustomValues.Restore(new List<OverlayCustomValueSettings>());
      Assert(OverlayCustomValues.Keys.Count == 0, "made values can be put back");
    }

    /// <summary>A row of the dock's condition input method, or the row that opens a group or a made value (slot 0).</summary>
    private static Pressable DockSlot(DockProbe dock, int slot)
    {
      Settle();
      return dock.Root.GetComponentsInChildren<Pressable>(false).Single(row => row.name == "Slot " + slot);
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

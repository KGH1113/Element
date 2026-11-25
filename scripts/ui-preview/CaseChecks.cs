using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.TextEditor;
using System.IO;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;

namespace Element.Editor
{
  // Cases typed on the keypad: a size that changes by value, and a text
  // color whose cases give the highlight color and a custom one, the picker
  // following the color a case just got, the dock reading it all, and Done
  // saving what the game then draws.
  public static partial class SettingsPreview
  {
    private static void CheckCases(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
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
      Pressable then = DockKey(dock, "Key then");

      // A size with a case: combo 100 or more makes it 60.
      PropertyButtonViewModel size = Field<TextSize>(text, "_size").Button;
      PressBar(text, size);
      Settle();
      Assert(dock.Owner == "size" && !then.IsInteractable(), "the size opens in the dock; 'then' waits for a comparison");
      Press(DockChip(dock, "Value combo"));
      Press(DockKey(dock, "Key ≥"));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Settle();
      Assert(then.IsInteractable() && then.GetComponentInChildren<TextMeshProUGUI>().text == (korean ? "이면" : "then"),
        "after a comparison the key says 'then'");
      Press(then);
      Press(DockKey(dock, "Key 6"));
      Press(DockKey(dock, "Key 0"));
      Settle();
      Assert(formulas.Editing.Working.Serialize() == "{combo}>=100?60" && then.GetComponentInChildren<TextMeshProUGUI>().text == (korean ? "아니면" : "otherwise"),
        "a case is typed left to right, and the key now says 'otherwise': " + formulas.Editing.Working.Serialize());
      Assert(dock.ResultText.Contains("60") && dock.FormulaText.Contains(korean ? "아니면" : "otherwise"),
        "the dock shows the result for the example combo 120, and what shows otherwise: " + dock.FormulaText + " " + dock.ResultText);
      Assert(document.Document[0].SizeFormula == "{combo}>=100?60" && size.Value == "60", "the size row shows the result: " + size.Value);
      SaveFrame(camera, texture, canvases, name + "-cases-size");
      SaveFrameLarge(camera, canvases, name + "-cases-size-2x");
      document.Undo();
      Call(text, "CloseOpen");
      Settle();

      // The text color: the dock points at it while the picker is open.
      PropertyButtonViewModel colorButton = color.Button;
      PressBar(text, colorButton);
      Settle();
      UiColorPicker picker = color.Picker;
      Assert(picker.Visible && dock.Owner == ColorCases.Key && formulas.Editing.Kind == OverlayFormulaKind.Color,
        "opening the color points the dock at it");
      Assert(!ColorPickerProbe.Shown(ColorPickerProbe.Segment(picker.Root, 0)), "Highlight/Custom is no longer in the picker");
      Assert(dock.FormulaText.Contains("●"), "with no case the line shows the color itself: " + dock.FormulaText);

      // X-Accuracy = 100 then the highlight color.
      Press(DockChip(dock, "Value accuracy"));
      Press(DockKey(dock, "Key ="));
      foreach (string key in new[] { "Key 1", "Key 0", "Key 0" })
        Press(DockKey(dock, key));
      Press(then);
      Settle();
      Assert(!DockKey(dock, "Key 5").IsInteractable(), "a color's case takes no number as its result");
      Press(DockChip(dock, "Value highlight-color"));
      Settle();
      Call(text, "Render");
      Settle();
      Assert(document.Document[0].Color.Formula == "{accuracy}=100?{highlight-color}"
        && document.Document.Count > 1 && document.Document[document.Document.Count - 1].Color.Formula == "{accuracy}=100?{highlight-color}",
        "the whole text's color has the case: " + document.Document[0].Color.Formula);
      Assert(color.Cases.OnCase && color.Title == ColorCases.CaseTitle && picker.Model.FollowsHighlight,
        "the picker shows the color the case just got: " + color.Title);

      // Otherwise, X-Accuracy 98 or more: a custom color, picked in the picker.
      Press(then);
      Settle();
      Call(text, "Render");
      Assert(!color.Cases.OnCase && color.Title != ColorCases.CaseTitle, "'otherwise' gives the picker back to the color itself");
      Press(DockChip(dock, "Value accuracy"));
      Press(DockKey(dock, "Key ≥"));
      foreach (string key in new[] { "Key 9", "Key 8" })
        Press(DockKey(dock, key));
      Press(then);
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
      Assert(document.Document[0].Color.Formula == "{accuracy}=100?{highlight-color};{accuracy}>=98?#5AC8FA",
        "the picker colors that case: " + document.Document[0].Color.Formula);
      Assert(previewText.text.Contains("<color=#5AC8FA"), "the example 99.5 is past 98: the preview turns sky blue");
      Assert(dock.FormulaText.Contains("#5AC8FA") && dock.FormulaText.Contains(korean ? "아니면" : "otherwise"),
        "the line reads the cases and the color otherwise: " + dock.FormulaText);
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-cases-color");
      SaveFrameLarge(camera, canvases, name + "-cases-color-2x");

      // Apply lets the picker go back to the text's own color; the cases stay.
      Press(DockKey(dock, "Key done"));
      Settle();
      Call(text, "Render");
      Assert(picker.Visible && !color.Cases.OnCase && dock.Owner == ColorCases.Key
        && document.Document[0].Color.Formula.EndsWith("#5AC8FA"), "Apply keeps the cases and the picker on the color itself");

      // A color can't go in a number, nor in the text.
      Call(text, "CloseOpen");
      picker.Hide();
      Settle();
      int count = document.Document.Count;
      Press(DockChip(dock, "Value highlight-color"));
      Assert(document.Document.Count == count && EditorScreen(text).ViewModel.NoticeText.Length > 0, "a color isn't added to the text, and says why");

      // Done saves the cases; the overlay draws the color for the example.
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && accuracy.Settings.Text.Runs.All(run => run.Color.Formula.Contains("#5AC8FA")), "Done saves the color's cases");
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#5AC8FA"), "the overlay draws the case's color: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => string.IsNullOrEmpty(run.Color.Formula)), "it is one layout undo step");
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

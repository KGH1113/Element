using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.IfTool;
using Element.Presentation.OverlayEditor.Parts.ValueSource;
using Element.Presentation.OverlayEditor.TextEditor;
using System.IO;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Conditions;
using Element.Domain.Overlay.Text;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  // An If in a text: the column's If row and its tool, making an If, a case's
  // value, number and the three results that turn it on, looks shown and
  // edited one at a time, trying a value, undo, and Done saving an If the
  // game then draws by the value.
  public static partial class SettingsPreview
  {
    private static void CheckIf(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      bool korean = Korean(name);
      TextOverlayElement accuracy = host.LayoutTargets.OfType<TextOverlayElement>().Single(target => target.Id == "accuracy");
      Select(editor, IndexOf(editor, accuracy));
      PressEdit(editor);
      TextEditorScreen text = (TextEditorScreen)Kind<TextKind>(editor).Editor;
      Assert(text.IsOpen, "the X-Accuracy text opens in the editor");
      TextIf tool = Field<TextIf>(text, "_if");
      TextDocumentEditing document = TextDocumentOf(text);
      DockProbe dock = DockOf(text);
      EditorFramePanel screen = EditorScreen(text);
      TextMeshProUGUI previewText = TextPreviewOf(text).Text;
      TextMeshProUGUI caption = ByName<TextMeshProUGUI>(screen.Frame, "If look");
      int length = document.Document.Count;

      Assert(tool.Button.Value == (korean ? "없음" : "None"), "the If row says the text has none: " + tool.Button.Value);
      PressBar(text, tool.Button);
      Settle();
      IfToolViewModel model = tool.ViewModel;

      // On a selection, only that part of the text becomes an If, and with
      // the caret in it the column's tools act on its look alone.
      int value = Enumerable.Range(0, length).First(i => document.Document[i].IsValue);
      SetField(document, "_anchor", value); SetField(document, "_caretIndex", value + 1);
      Call(text, "Render");
      Press(ByName<Pressable>(tool.Root, "Add case"));
      Settle();
      Assert(document.Document.BlockCount == 1 && document.Document.Block(0).Start == value && document.Document.Block(0).End == value + 1,
        "a selection becomes an If over just those characters");
      document.Range(out int lookStart, out int lookEnd);
      Assert(!document.HasSelection && document.InLook && lookStart == value && lookEnd == value + 1,
        "with the caret in the If, tools act on its look: " + lookStart + "–" + lookEnd);
      SetField(document, "_anchor", 0); SetField(document, "_caretIndex", 0);
      Call(text, "Render");
      Settle();
      document.Range(out lookStart, out lookEnd);
      Assert(!document.InLook && lookStart == 0 && lookEnd == length && model.HasIf.Value,
        "with the caret outside, they act on the whole text, and the tool still shows the text's If");
      document.Undo();
      SetField(document, "_anchor", length); SetField(document, "_caretIndex", length);
      Call(text, "Render");
      Settle();
      Assert(document.Document.BlockCount == 0 && !model.HasIf.Value, "undo takes the If away again");
      Assert(tool.Visible && !model.HasIf.Value && model.Cases.Value.Count == 0, "the If row opens its tool, empty at first");
      Assert(dock.HintText.Contains(korean ? "만약 만들기" : "Make an If"), "the dock says what an If does: " + dock.HintText);
      Assert(!Shown(caption), "a text without an If has no look named over it");

      // Making one: the whole text is an If whose first case compares the text's own number.
      Press(ByName<Pressable>(tool.Root, "Add case"));
      Settle();
      OverlayTextDocument edited = document.Document;
      Assert(edited.BlockCount == 1 && edited.Block(0).Start == 0 && edited.Block(0).End == length && edited.Block(0).Cases == 1,
        "with nothing selected the whole text becomes an If with one case");
      OverlayCondition first = edited.Block(0).When(0);
      Assert(first.Value == "accuracy" && first.Than == "100" && first.Same && first.Bigger && !first.Smaller,
        "the case compares X-Accuracy with the round number above it, on from there up: " + first.Value + " " + first.Than);
      Assert(edited.Block(0).Shown == 0 && !document.HasSelection && document.Caret == length && document.InLook,
        "the new case's look is in the preview with the caret in it, nothing selected");
      IfCaseViewModel card = model.Card(0);
      Assert(model.HasIf.Value && model.Cases.Value.Count == 1 && card.Open.Value && !card.Now.Value && model.Otherwise.Now.Value,
        "its card is open; the example 99.5 is in no case, so otherwise is on now");
      Assert(card.ValueName.Value == (korean ? "절대 정확도" : "X-Accuracy") && card.ValueNow.Value == "99.5" && card.Number.Value == "100"
        && card.Result.Value == OverlayComparison.Smaller,
        "the card names the value, its number now and how it compares: " + card.ValueName.Value + " " + card.ValueNow.Value);
      Assert(tool.Button.Value == (korean ? "경우 1개" : "1 case"), "the row counts the cases: " + tool.Button.Value);
      Assert(Shown(caption) && caption.text.Contains(korean ? "100이거나 더 크면" : "100 or bigger"), "a line over the preview names the look: " + caption.text);
      Assert(screen.ViewModel.NoticeText.Length > 0, "and a notice says the look can be changed now");

      // The look is changed with the text's own tools; only it changes.
      document.Remember(false);
      edited.SetColor(0, length, OverlayColorSettings.Custom("#FFD84D"));
      Call(text, "Render");
      Settle();
      Assert(previewText.text.Contains("<color=#FFD84D"), "a color picked now colors this look");
      CheckIfToolFits(text, tool);
      SaveFrame(camera, texture, canvases, name + "-if");
      SaveFrameLarge(camera, canvases, name + "-if-2x");

      // The three results are switches: smaller and bigger without the same is "anything but".
      Press(ByName<Pressable>(tool.Root, "If smaller 0"));
      Press(ByName<Pressable>(tool.Root, "If same 0"));
      Settle();
      first = document.Document.Block(0).When(0);
      Assert(first.Smaller && !first.Same && first.Bigger && card.Now.Value, "the results switch one by one; 99.5 is not 100, so the case is on now");
      document.Undo();
      document.Undo();
      Settle();
      first = document.Document.Block(0).When(0);
      Assert(!first.Smaller && first.Same && first.Bigger, "each switch is its own undo step");

      // Its number is typed on the keypad.
      Press(ByName<Pressable>(tool.Root, "If number 0"));
      Settle();
      Assert(dock.Owner == TextIf.KeyPrefix + "0", "the case's number types in the dock: " + dock.Owner);
      Press(DockKey(dock, "Key clear"));
      foreach (string key in new[] { "Key 9", "Key 9" })
        Press(DockKey(dock, key));
      Settle();
      Assert(document.Document.Block(0).When(0).Than == "99" && card.Number.Value == "99" && card.Now.Value,
        "the case takes the typed number, and 99.5 is past it: " + card.Number.Value);
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(tool.Visible && dock.Owner == null, "the keypad lets go; the tool stays open");

      // A second case copies the first, to be changed; the open card is the look in the preview.
      Press(ByName<Pressable>(tool.Root, "Add case"));
      Settle();
      edited = document.Document;
      Assert(edited.Block(0).Cases == 2 && edited.Block(0).Shown == 1 && model.Card(1).Open.Value && !card.Open.Value,
        "a new case goes after the others and shows its look");
      Assert(edited.Block(0).When(1).Than == "99" && previewText.text.Contains("<color=#FFD84D"), "it starts as the case before it, look and all");
      Assert(card.Now.Value && !model.Card(1).Now.Value && !model.Otherwise.Now.Value, "the first case that is on is the one on now, read from the top");
      Assert(card.Sentence.Value.Contains(korean ? "99이거나 더 크면" : "99 or bigger"), "a closed card reads as a sentence: " + card.Sentence.Value);
      foreach (TextMeshProUGUI label in tool.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated || label.name == "Look", "If tool label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-if-cases");
      SaveFrameLarge(camera, canvases, name + "-if-cases-2x");

      // More cases than the column has room for scroll inside it, never over the keypad.
      for (int i = 0; i < 4; i++)
        Press(ByName<Pressable>(tool.Root, "Add case"));
      Settle();
      Assert(document.Document.Block(0).Cases == 6, "cases keep adding: " + document.Document.Block(0).Cases);
      CheckIfToolFits(text, tool);
      SaveFrame(camera, texture, canvases, name + "-if-many");
      for (int i = 0; i < 4; i++)
        document.Undo();
      Call(text, "Render");
      Settle();
      Assert(document.Document.Block(0).Cases == 2, "and undo takes them back one by one");

      // A closed card shows its look; so does otherwise.
      Press(ByName<Pressable>(tool.Root, "Show otherwise"));
      Settle();
      Assert(document.Document.Block(0).Shown == 2 && model.Otherwise.Open.Value && !previewText.text.Contains("<color=#FFD84D"),
        "otherwise shows the text as it was");
      Assert(caption.text.Contains(korean ? "아니면" : "otherwise"), "and the line over it says so: " + caption.text);
      Press(ByName<Pressable>(tool.Root, "Show case 0"));
      Settle();
      Assert(document.Document.Block(0).Shown == 0 && card.Open.Value, "a closed card opens with a press");

      // Trying a value moves it by hand: the look for that number shows.
      Assert(model.CanTry.Value && model.TryName.Value == card.ValueName.Value, "the slider tries the compared value: " + model.TryName.Value);
      model.Tried = 0;
      Settle();
      Assert(document.Document.Block(0).Shown == 2 && model.Otherwise.Now.Value && !card.Now.Value,
        "at the low end no case is on, and otherwise shows: " + model.TryNumber.Value);
      model.Tried = 1;
      Settle();
      Assert(document.Document.Block(0).Shown == 0 && card.Now.Value && card.Result.Value == OverlayComparison.Bigger,
        "at the high end the first case is on and shows: " + model.TryNumber.Value);
      Assert(previewText.text.Contains(model.TryNumber.Value), "the text shows the tried number: " + previewText.text);

      // Another value to compare: from the text, or any value from the dock.
      Press(ByName<Pressable>(tool.Root, "If value 0"));
      Settle();
      ValueSourceMenuPanel menu = (ValueSourceMenuPanel)tool.Menu;
      Assert(menu.Visible && ByName<Pressable>(menu.Root, "Follow accuracy") != null && InsideScreen(ScreenRectOf(menu.Root)),
        "the value offers the text's values in a menu on screen");
      Press(ByName<Pressable>(menu.Root, "Follow another value"));
      Settle();
      Assert(tool.Armed && !menu.Visible && dock.HintText.Contains(korean ? "비교할 값" : "value to compare"),
        "the dock waits for a value to compare, and says so: " + dock.HintText);
      int items = document.Document.Count;
      Press(DockChip(dock, "Value song-title"));
      Settle();
      Assert(tool.Armed && document.Document.Count == items, "words can't be compared, and nothing is added to the text");
      Press(DockChip(dock, "Value combo"));
      Settle();
      Assert(!tool.Armed && document.Document.Count == items && document.Document.Block(0).When(0).Value == "combo",
        "a value clicked in the dock is compared, not added to the text: " + document.Document.Block(0).When(0).Value);
      document.Undo();
      Settle();
      Assert(document.Document.Block(0).When(0).Value == "accuracy", "undo compares X-Accuracy again");

      // Cases go one by one; without the last, the If is gone and the text is plain again.
      Press(ByName<Pressable>(tool.Root, "Remove case 0"));
      Settle();
      Assert(document.Document.Block(0).Cases == 1 && document.Document.Block(0).Shown == 1, "removing the shown case shows otherwise");
      Press(ByName<Pressable>(tool.Root, "Show case 0"));
      Settle();
      Press(ByName<Pressable>(tool.Root, "Remove case 0"));
      Settle();
      Assert(document.Document.BlockCount == 0 && !model.HasIf.Value && document.Document.Count == length,
        "without its last case the If is gone, the text as otherwise showed it");
      document.Undo();
      document.Undo();
      Settle();
      Assert(document.Document.BlockCount == 1 && document.Document.Block(0).Cases == 2, "undo brings the cases back");

      // Back closes the tool and lets the tried value go.
      PressBack(text);
      Settle();
      Assert(!tool.Visible && !tool.Armed, "back closes the If tool");
      Assert(Shown(caption), "the look in the preview stays named while the tool is closed");

      // Done saves it; the game draws the look for the value from then on.
      PressEditorButton(text, "Text editor done");
      OverlayTextRun saved = accuracy.Settings.Text.Runs.Single();
      Assert(!text.IsOpen && saved.If != null && saved.If.Cases.Count == 2 && saved.If.Cases[0].Runs.All(run => run.Color.Hex == "#FFD84D"),
        "Done saves the If with its cases and their looks");
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#FFD84D"), "the overlay draws the look for the example, past 99: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => run.If == null), "it is one layout undo step");
    }

    // The tool stands in the column above the keypad, and never reaches into it.
    private static void CheckIfToolFits(TextEditorScreen text, TextIf tool)
    {
      Settle();
      Rect rect = ScreenRectOf(tool.Root), keypad = KeypadRect(DockOf(text));
      Assert(Mathf.Abs(rect.xMin - keypad.xMin) < 1 && Mathf.Abs(rect.xMax - keypad.xMax) < 1 && rect.yMin > keypad.yMax,
        "the If tool stands in the column, above the keypad: " + rect + " / " + keypad);
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

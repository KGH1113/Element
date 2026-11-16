using System.IO;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.ValueColors;
using Element.Domain.Overlay.Values;
using Element.Presentation.Layout;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Layout.ContentEditors.Text;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  // A text's color following a value: the Animate tool's color switch, the
  // value it follows (from the text or the dock), the band's stops (add,
  // pick, drag, type, recolor, remove), smooth or steps, undo, and Done
  // saving a color the game then draws from the value.
  public static partial class SettingsPreview
  {
    private static void CheckValueColor(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      bool korean = Korean(name);
      TextOverlayElement accuracy = host.LayoutTargets.OfType<TextOverlayElement>().Single(target => target.Id == "accuracy");
      Select(editor, IndexOf(editor, accuracy));
      PressEdit(editor);
      OverlayTextEditor text = (OverlayTextEditor)Kind<TextKind>(editor).Editor;
      Assert(text.IsOpen, "the X-Accuracy text opens in the editor");
      TextAnimate animate = Field<TextAnimate>(text, "_animate");
      TextValueColor valueColor = Field<TextValueColor>(text, "_valueColor");
      OverlayValueColorModel model = Field<OverlayValueColorModel>(valueColor, "_model");
      TextDocumentEditing document = TextDocumentOf(text);
      OverlayInputDock dock = Field<OverlayInputDock>(text, "_dock");
      OverlayEditorFrame screen = EditorScreen(text);

      PressBar(text, animate.Button);
      Assert(animate.Visible, "Animate opens its tool");
      Pressable colorSwitch = ByName<Pressable>(animate.Root, "Color by value");

      // Letters alone have no value to follow: the values to pick from open at once.
      SetField(document, "_caretIndex", 3); SetField(document, "_anchor", 0);
      Call(text, "Render");
      Press(colorSwitch);
      Settle();
      OverlayValueSourceMenu sources = (OverlayValueSourceMenu)valueColor.Menu;
      Assert(valueColor.Current() != null && valueColor.Current().Value == "" && sources.Visible,
        "a selection without a value turns on following nothing yet, and offers the values");
      Assert(model.SourceName.Value == (korean ? "고르기" : "Pick") && document.Document[5].Color.ValueColor == null,
        "the value row asks for one; the rest of the text is left alone");
      Press(ByName<Pressable>(sources.Root, "Follow accuracy"));
      Settle();
      Assert(valueColor.Current().Value == "accuracy" && valueColor.Current().Stops.Count == 4 && !sources.Visible,
        "a value from the menu is followed, with its own first look");
      document.Undo();
      document.Undo();
      SetField(document, "_caretIndex", document.Document.Count); SetField(document, "_anchor", document.Document.Count);
      Call(text, "Render");
      Settle();
      Assert(Enumerable.Range(0, document.Document.Count).All(i => !document.Document[i].Color.FollowsValue), "undo leaves the text following nothing");

      Press(colorSwitch);
      Settle();
      OverlayValueColor color = valueColor.Current();
      Assert(color != null && color.Value == "accuracy" && color.Mode == OverlayValueColorMode.Step && color.Stops.Count == 4,
        "turning it on follows the text's X-Accuracy, stepping through four stops");
      Assert(Enumerable.Range(0, document.Document.Count).All(i => document.Document[i].Color.FollowsValue),
        "with nothing selected, the whole text follows it");
      Assert(animate.Button.Value == (korean ? "색" : "Color"), "the Animate row says the color is on: " + animate.Button.Value);
      Assert(model.Current.Stops[model.Selected].At == 99.5f, "the stop the value has reached now is picked first");
      Assert(model.SourceName.Value == (korean ? "절대 정확도" : "X-Accuracy"), "the value row names X-Accuracy: " + model.SourceName.Value);
      Assert(!double.IsNaN(model.Live) && model.LiveIsSample, "the band marks the editor's example while no level plays");
      RectTransform band = ByName<RectTransform>(animate.Root, "Value color band");
      Assert(band != null && Shown(band), "the band shows under the value row");
      CheckToolFits(text, animate);
      TextMeshProUGUI previewText = TextPreviewOf(text).Text;
      Assert(previewText.text.Contains("<color=#5AC8FA"), "the preview wears the color of 99.5: sky blue");
      Assert(TextColorOf(text).Button.Value == (korean ? "값에 따라" : "By value"), "Text color says the color follows a value");
      SaveFrame(camera, texture, canvases, name + "-value-color");
      SaveFrameLarge(camera, canvases, name + "-value-color-2x");

      // Smooth blends the stops.
      model.Mode = 0;
      Settle();
      Assert(valueColor.Current().Mode == OverlayValueColorMode.Smooth, "the segmented control switches to smooth");
      SaveFrameLarge(camera, canvases, name + "-value-color-smooth-2x");
      document.Undo();
      Settle();
      Assert(valueColor.Current().Mode == OverlayValueColorMode.Step, "undo brings steps back");

      // A press on the band between stops adds one there, in the band's color, and drags it.
      UiPointerArea input = ByName<UiPointerArea>(animate.Root, "Band input");
      input.Down(Pointer(BandPoint(band, model, 97)));
      Settle();
      color = valueColor.Current();
      Assert(color.Stops.Count == 5 && Mathf.Abs(color.Stops[4].At - 97) < .06f && model.Selected == 4,
        "a press on the band adds a stop where it was pressed and picks it: " + color.Stops.Last().At);
      Assert(color.Stops[4].Hex == "#FFFFFF", "in steps it takes the color the band had there");
      input.Dragged(Pointer(BandPoint(band, model, 96.42f)));
      input.Up(Pointer(BandPoint(band, model, 96.42f)));
      Settle();
      color = valueColor.Current();
      Assert(Mathf.Abs(color.Stops[4].At - 96.4f) < .001f, "dragging snaps the stop to a tidy number: " + color.Stops[4].At);
      document.Undo();
      Settle();
      Assert(valueColor.Current().Stops.Count == 4, "adding and dragging a stop is one undo step");
      document.Redo();
      Settle();

      // A press on a stop picks it; its value is typed on the keypad.
      input.Down(Pointer(BandPoint(band, model, 98)));
      input.Up(Pointer(BandPoint(band, model, 98)));
      Settle();
      Assert(model.Current.Stops[model.Selected].At == 98, "a press on a stop picks it");
      Press(ByName<Pressable>(animate.Root, "Stop value"));
      Settle();
      Assert(dock.Owner == TextValueColor.StopKey, "the picked stop's value types in the dock");
      Press(DockKey(dock, "Key clear"));
      foreach (string key in new[] { "Key 9", "Key 7", "Key ." , "Key 5" })
        Press(DockKey(dock, key));
      Settle();
      Assert(model.Current.Stops[model.Selected].At == 97.5f && model.StopValue.Value == "97.5", "the stop takes the typed value: " + model.StopValue.Value);
      Press(DockKey(dock, "Key done"));
      Settle();
      Assert(animate.Visible && dock.Owner == null, "the keypad lets go; the tool stays open");

      // Its color opens the picker beside the column, and the text wears it meanwhile.
      Press(ByName<Pressable>(animate.Root, "Stop color"));
      Settle();
      UiColorPicker picker = TextColorOf(text).Picker;
      Assert(picker.Visible && TextColorOf(text).PicksOther && !picker.GradientAvailable, "the stop's color opens the picker, solid only");
      Rect pickerRect = ScreenRectOf(picker.Root), toolRect = ScreenRectOf(animate.Root);
      Assert(pickerRect.xMax <= toolRect.xMin + .5f && InsideScreen(pickerRect), "the picker opens beside the tool, the band staying in view: " + pickerRect + " / " + toolRect);
      Assert(!pickerRect.Overlaps(ScreenRectOf(previewText.rectTransform)), "the text moves clear of the picker");
      picker.Model.SetSource(false);
      picker.Model.PickSaturationValue(.7f, .95f);
      picker.Model.PickHue(.8f);
      Settle();
      string recolored = model.Current.Stops[model.Selected].Hex;
      Assert(recolored != "#FFFFFF" && !model.Current.Stops[model.Selected].Highlight,
        "the picker recolors the stop: " + recolored + " (stop " + model.Selected + " of " + string.Join(", ", model.Current.Stops.Select(stop => stop.At + " " + stop.Hex)) + ")");
      Assert(previewText.text.Contains("<color=" + recolored), "the text shows the stop's color while it is picked");
      foreach (TextMeshProUGUI label in animate.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "Animate tool label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-value-color-picker");
      SaveFrameLarge(camera, canvases, name + "-value-color-picker-2x");
      text.Escape();
      Settle();
      Assert(!picker.Visible && animate.Visible, "Esc closes the picker first, leaving the tool");
      Assert(previewText.text.Contains("<color=#5AC8FA"), "and the text follows the value again");

      // Remove the picked stop; the last two stay.
      int stopsBefore = valueColor.Current().Stops.Count;
      Pressable remove = ByName<Pressable>(animate.Root, "Remove stop");
      Assert(remove != null && remove.IsInteractable() && model.CanRemove.Value, "with five stops the picked one can go: " + model.CanRemove.Value);
      Press(remove);
      Settle();
      Assert(valueColor.Current().Stops.Count == stopsBefore - 1, "the picked stop goes: " + valueColor.Current().Stops.Count);

      // Another value: from the text first, or any value from the dock.
      Press(ByName<Pressable>(animate.Root, "Value color source"));
      Settle();
      OverlayValueSourceMenu menu = (OverlayValueSourceMenu)valueColor.Menu;
      Assert(menu.Visible && ByName<Pressable>(menu.Root, "Follow accuracy") != null && ByName<Pressable>(menu.Root, "Follow another value") != null,
        "the value row offers the text's values, then another from the dock");
      foreach (TextMeshProUGUI label in menu.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "value menu label fits: " + label.text);
      }
      Assert(InsideScreen(ScreenRectOf(menu.Root)), "the menu stays on screen");
      SaveFrameLarge(camera, canvases, name + "-value-color-menu-2x");
      Press(ByName<Pressable>(menu.Root, "Follow another value"));
      Settle();
      Assert(valueColor.Armed && !menu.Visible && dock.HintText.Contains(korean ? "따라갈 값" : "value to follow"),
        "the dock waits for a value to follow, and says so: " + dock.HintText);
      int items = document.Document.Count;
      Press(DockChip(dock, "Value song-title"));
      Settle();
      Assert(valueColor.Armed && document.Document.Count == items && screen.NoticeText.Length > 0,
        "words can't pick a color, and say why: " + screen.NoticeText);
      text.Escape();
      Assert(!valueColor.Armed && animate.Visible && text.IsOpen, "Esc stops picking from the dock, leaving the tool");
      Press(ByName<Pressable>(animate.Root, "Value color source"));
      Settle();
      Press(ByName<Pressable>(menu.Root, "Follow another value"));
      Settle();
      Press(DockChip(dock, "Value combo"));
      Settle();
      color = valueColor.Current();
      Assert(!valueColor.Armed && document.Document.Count == items && color.Value == "combo" && color.Stops.Count == stopsBefore - 1,
        "a value clicked in the dock is followed, not added to the text: " + color.Value + ", " + color.Stops.Count + " stops");
      OverlayValueColorEdits.Range(color, out float low, out float high);
      Assert(low == 0 && high == 200, "the stops spread over combo's range, keeping their colors: " + low + "–" + high);
      SaveFrameLarge(camera, canvases, name + "-value-color-combo-2x");
      document.Undo();
      Settle();
      Assert(valueColor.Current().Value == "accuracy", "undo follows X-Accuracy again");

      // Another mod's value works the same.
      Press(ByName<Pressable>(animate.Root, "Value color source"));
      Settle();
      Press(ByName<Pressable>(menu.Root, "Follow another value"));
      Settle();
      Press(DockChip(dock, "Value Sample/left-keys"));
      Settle();
      Assert(valueColor.Current().Value == "Sample/left-keys" && model.Live == 5 && model.SourceName.Value == (korean ? "왼손 키" : "Left keys"),
        "a mod's value can be followed, its number marked on the band: " + model.Live);
      Assert(previewText.text.Contains("<color="), "the text takes the mod value's color");
      document.Undo();
      Settle();

      // One way to color a text at a time: a color picked in Text color stops following the value.
      PressBack(text);
      Assert(!animate.Visible && !valueColor.Armed, "back closes the Animate tool");
      PressBar(text, TextColorOf(text).Button);
      UiColorPicker textPicker = TextColorOf(text).Picker;
      Assert(textPicker.Visible && !TextColorOf(text).PicksOther && textPicker.GradientAvailable,
        "Text color opens its own picker, with the color bar");
      textPicker.Model.SetSource(false);
      textPicker.Model.PickSaturationValue(.5f, .9f);
      Settle();
      Assert(!document.Document[0].Color.FollowsValue && screen.NoticeText.Contains(korean ? "값에 따라 바뀌던 색은 껐어요" : "no longer follows a value"),
        "a color picked there stops following the value, and says so: " + screen.NoticeText);
      text.Escape();
      document.Undo();
      Settle();
      Assert(document.Document[0].Color.FollowsValue, "undo follows the value again");

      // Done saves it; the game draws the value's color from then on.
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && accuracy.Settings.Text.Runs.All(run => run.Color.FollowsValue && run.Color.ValueColor.Value == "accuracy"),
        "Done saves the color that follows X-Accuracy");
      TextMeshProUGUI game = Field<TextMeshProUGUI>(accuracy, "_text");
      Assert(game.text.Contains("<color=#5AC8FA"), "the overlay draws the example's color: " + game.text);
      Call(editor, "Undo");
      Assert(accuracy.Settings.Text.Runs.All(run => !run.Color.FollowsValue), "it is one layout undo step");
    }

    // The tool fits above the keypad on 16:9 and 16:10 screens: 312 frame units under its back row.
    private static void CheckToolFits(OverlayTextEditor text, TextAnimate animate)
    {
      Settle();
      Assert(animate.Height <= 312, "the Animate tool with its band fits above the keypad on wide screens: " + animate.Height);
      Rect tool = ScreenRectOf(animate.Root), keypad = KeypadRect(Field<OverlayInputDock>(text, "_dock"));
      Assert(Mathf.Abs(tool.xMin - keypad.xMin) < 1 && Mathf.Abs(tool.xMax - keypad.xMax) < 1 && tool.yMin > keypad.yMax,
        "the tool stands in the column, above the keypad: " + tool + " / " + keypad);
    }

    // A point on the band at value, on the scale it shows now.
    private static Vector2 BandPoint(RectTransform band, OverlayValueColorModel model, float value)
    {
      OverlayValueColorEdits.Range(model.Current, out float low, out float high);
      Rect local = band.rect;
      float inset = OverlayValueColorBandView.Inset;
      float x = local.xMin + inset + (value - low) / (high - low) * (local.width - inset * 2);
      float y = local.yMax - OverlayValueColorBandView.LabelRow - OverlayValueColorBandView.Track * .5f;
      return RectTransformUtility.WorldToScreenPoint(null, band.TransformPoint(new Vector3(x, y, 0)));
    }

    // The same frame at twice the pixels, to look at closely.
    private static void SaveFrameLarge(Camera camera, Canvas[] canvases, string name)
    {
      _refresh?.Invoke();
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

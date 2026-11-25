using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Gradient;
using Element.Presentation.OverlayEditor.ShapeEditor;
using Element.Presentation.OverlayEditor.TextEditor;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Domain.Overlay.Variables;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Ui.Popovers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.ColorPicker;
using ElementUI.Ugui;
using Element.Presentation.Overlay.Shape;
using Element.Presentation.Overlay.Text;

namespace Element.Editor
{
  // Gradients in the overlay editors: the color bar, kinds, handles with
  // snapping, the highlight's opacity-only stops, and how shapes and texts
  // draw them afterwards.
  public static partial class SettingsPreview
  {
    private static void CheckGradients(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      Kind<ShapeKind>(editor).Add(OverlayShapeKind.Square);
      ShapeOverlayElement shape = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      ShapeEditorScreen shapes = (ShapeEditorScreen)Kind<ShapeKind>(editor).Editor;
      Assert(shapes.IsOpen, "a new square opens its editor");
      Call(ShapeNumbersOf(shapes), "Write", "width", OverlayVariable.FromNumber(420));
      Call(ShapeNumbersOf(shapes), "Write", "height", OverlayVariable.FromNumber(240));
      Call(shapes, "Render");
      // With motion, opening the picker glides the shape aside from where it was.
      ElementSettings preferences = Session(editor).Preferences;
      bool reduceMotion = preferences.ReduceUiMotion;
      preferences.ReduceUiMotion = false;
      Rect resting = ScreenRectOf(ShapeRect(shapes));
      Call(ShapeColorsOf(shapes), "Open", false);
      EditorGlide glide = Field<EditorGlide>(Field<EditorPreviewSpace>(shapes, "_space"), "_glide");
      Assert(glide.Moving && ScreenRectOf(ShapeRect(shapes)) == resting,
        "opening the picker starts the shape gliding from where it was, without a jump");
      Call(ShapeColorsOf(shapes), "Open", false);
      // The checks below measure where things land, so they settle at once.
      preferences.ReduceUiMotion = true;
      Call(ShapeColorsOf(shapes), "Open", false);
      UiColorPicker picker = ShapeColorsOf(shapes).Picker;
      GradientHandles handles = ShapeGradientOf(shapes).Handles;
      Assert(picker.Visible && picker.GradientAvailable && !picker.Gradient.IsGradient && !handles.Visible,
        "the fill picker has a color bar and starts solid, with no handles");
      RectTransform bar = Field<RectTransform>(picker.Gradient, "_bar");
      Settle();
      // The one shown: each of the picker's two layouts keeps its own.
      Pressable makeGradient() => picker.Root.GetComponentsInChildren<Pressable>(false).FirstOrDefault(button => button.name == "Make gradient");
      Assert(!bar.gameObject.activeInHierarchy, "a solid color has no color bar: " + PathOf(bar));
      Assert(makeGradient() != null, "only the button that makes it a gradient");

      // A new shape follows the highlight; a custom color shows the whole picker.
      picker.Model.SetSource(false);
      Settle();
      Rect solidPicker = ScreenRectOf(picker.Root);
      SaveFrame(camera, texture, canvases, name + "-gradient-hover");

      // The button makes a gradient whose new color shows at once.
      Press(makeGradient());
      Settle();
      OverlayColorSettings fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(fill.IsGradient && fill.Gradient.Stops.Count == 2 && picker.Gradient.IsGradient && picker.Gradient.SelectedStop == 1
        && bar.gameObject.activeInHierarchy && makeGradient() == null,
        "the button makes a gradient, picks its new color and gives way to the bar");
      Assert(ScreenRectOf(picker.Root) == solidPicker, "turning into a gradient keeps the picker in place");
      Invoke(picker.Gradient, "BarDown", Pointer(BarPoint(bar, 1)));
      Invoke(picker.Gradient, "BarDrag", Pointer(BarPoint(bar, .7f)));
      Settle();
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(fill.Gradient.Stops[1].Alpha > .99f && Mathf.Abs(fill.Gradient.Stops[1].Position - .7f) < .03f,
        "the new color is opaque and moves along the bar: " + fill.Gradient.Stops[1].Alpha + " at " + fill.Gradient.Stops[1].Position);
      Assert(fill.Gradient.Stops[1].Hex != fill.Gradient.Stops[0].Hex, "the new color is a neighbor, not the same color");
      Assert(Field<RectTransform>(picker.Gradient, "_kindRow").gameObject.activeSelf && handles.Visible,
        "the kinds and the handles show with two colors");
      DockProbe dock0 = DockOf(shapes);
      FormulaFieldView position = ShapeGradientOf(shapes).Position;
      Assert(position.Text == "70", "the picked color's position number says where it is: " + position.Text);
      Rect numberRect = ScreenRectOf(position.Root), barRect = ScreenRectOf(bar);
      Rect opacityRect = ScreenRectOf(ShapeColorsOf(shapes).Opacity.Field.Root);
      Rect delete = ScreenRectOf((RectTransform)Field<Button>(picker.Gradient, "_delete").transform);
      Assert(numberRect.yMax <= barRect.yMin && Mathf.Abs(numberRect.center.y - opacityRect.center.y) < 1
        && Mathf.Abs(numberRect.width - opacityRect.width) < 1 && numberRect.xMin >= opacityRect.xMax,
        "the position and the opacity share a row under the bar, each half of it: " + numberRect + " / " + opacityRect);
      Assert(delete.yMin >= barRect.yMax - .5f && Mathf.Abs(delete.center.x - BarPoint(bar, .7f).x) < delete.width * .75f,
        "the delete button stands over the picked color");
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "picker label fits: " + label.text);
      }
      Assert(ScreenRectOf(picker.Root).yMax <= Screen.height && ScreenRectOf(picker.Root).yMin >= 0, "the picker stays on screen");
      // 16:9 and 16:10 screens leave 310 frame units above the keypad.
      Assert(picker.Height <= 310 && picker.CurrentWidth > 500, "the two-column picker fits above the keypad on wide screens: " + picker.Height);
      Rect pickerRect = ScreenRectOf(picker.Root);
      Assert(!pickerRect.Overlaps(ScreenRectOf(ShapeRect(shapes)))
        && !pickerRect.Overlaps(ScreenRectOf(EditorBarRoot(shapes)))
        && !pickerRect.Overlaps(KeypadRect(dock0)),
        "the shape and its bar move clear of the picker, which stays off the keypad");
      SaveFrame(camera, texture, canvases, name + "-gradient-linear");

      // The ends of the line are handles only: nothing covers them.
      Rect shapeRect = ScreenRectOf(ShapeRect(shapes));
      List<Button> arcs = Field<List<Button>>(Measures(shapes), "_arcs");
      Assert(arcs.All(arc => !arc.gameObject.activeSelf), "corner arcs step aside while the handles show");
      Vector2 start = new Vector2(shapeRect.xMin, shapeRect.center.y);
      Assert(handles.Contains(start) && handles.SquareAt(start) < 0, "the start handle is a handle, not a color");
      int square = Enumerable.Range(0, 40).Select(i => handles.SquareAt(start + new Vector2(0, -i))).FirstOrDefault(i => i >= 0);
      Assert(handles.SquareAt(start + new Vector2(0, -24)) == 0 || square == 0, "the first color hangs just below its end of the line");

      // Each kind is a tile drawn in the colors; radial adds a width handle.
      Field<Image[]>(picker.Gradient, "_kindFrames")[1].GetComponent<Button>().onClick.Invoke();
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(fill.Gradient.Kind == OverlayGradientKind.Radial && Mathf.Abs(fill.Gradient.StartX - .5f) < .01f, "a kind tile switches the kind");

      // Dragging the end handle to the top right corner sticks to it.
      object grips = Field<object>(handles, "_grips");
      Vector2 end = new Vector2(shapeRect.xMax, shapeRect.center.y);
      Invoke(handles, "GripDown", 1, Pointer(end));
      // A few frame units short of the corner, whatever the screen's scale.
      float near = 3 * Field<EditorFramePanel>(shapes, "_frame").Scale;
      Invoke(handles, "GripDrag", 1, Pointer(new Vector2(shapeRect.xMax - near, shapeRect.yMax - near * 1.3f)));
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(Mathf.Abs(fill.Gradient.EndX - 1) < .001f && Mathf.Abs(fill.Gradient.EndY) < .001f,
        "the end handle sticks to the box's corner: " + fill.Gradient.EndX + ", " + fill.Gradient.EndY);
      SaveFrame(camera, texture, canvases, name + "-gradient-snap");
      Invoke(handles, "EndDrag");
      Assert(grips != null, "handles exist");

      // The angle pill types in the dock; 0° points the gradient up.
      Call(shapes, "Activate", "fill-angle");
      DockProbe dock = DockOf(shapes);
      Assert(dock.Owner == "fill-angle", "the angle opens in the dock");
      Call(ShapeNumbersOf(shapes), "Write", "fill-angle", OverlayVariable.FromNumber(0));
      ShapeNumbersOf(shapes).FinishEditing();
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(Mathf.Abs(OverlayGradientMath.Angle(fill.Gradient, 420, 240)) < .5f, "a typed angle turns the gradient");

      // Following the highlight leaves only opacities; nothing on the left moves.
      Rect barBefore = ScreenRectOf(bar);
      picker.Model.SetSource(true);
      Settle();
      Assert(Mathf.Abs(ScreenRectOf(bar).xMax - barBefore.xMax) < 1 && Mathf.Abs(ScreenRectOf(bar).yMin - barBefore.yMin) < 1
        && Mathf.Abs(ScreenRectOf(picker.Root).xMin - KeypadRect(DockOf(shapes)).xMin) < 1,
        "following the highlight leaves one column over the keypad, the bar at the same height");
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      List<int> order = OverlayGradientEdits.Order(fill.Gradient);
      Assert(fill.Highlight && fill.IsGradient && fill.Gradient.Stops[order[order.Count - 1]].Alpha < fill.Gradient.Stops[order[0]].Alpha,
        "the highlight gradient fades instead of vanishing");
      Assert(!ColorPickerProbe.Shown(ColorPickerProbe.Plane(picker.Root)) && ColorPickerProbe.Shown(ColorPickerProbe.Alpha(picker.Root)),
        "only the opacity shows while following the highlight");
      SaveFrame(camera, texture, canvases, name + "-gradient-highlight");
      picker.Model.SetSource(false);
      Settle();

      // A small shape shows larger while its color is edited, and says so.
      ShapePreview shapePreview = Field<ShapePreview>(shapes, "_preview");
      Assert(shapePreview.Shown <= 1.001f, "a large shape isn't made larger: " + shapePreview.Shown);
      Call(ShapeNumbersOf(shapes), "Write", "width", OverlayVariable.FromNumber(30));
      Call(ShapeNumbersOf(shapes), "Write", "height", OverlayVariable.FromNumber(24));
      Call(shapes, "Render");
      Settle();
      Assert(bar.gameObject.activeInHierarchy && position.Root.gameObject.activeInHierarchy && ShapeColorsOf(shapes).Opacity.Field.Root.gameObject.activeInHierarchy,
        "back from the highlight, the bar and both numbers are there again");
      TextMeshProUGUI zoomLine = ByName<TextMeshProUGUI>(Field<EditorFramePanel>(shapes, "_frame").Frame, "Preview zoom");
      Assert(shapePreview.Shown >= 2 && Shown(zoomLine) && zoomLine.text.Length > 0,
        "a small shape shows larger while its color is edited: " + shapePreview.Shown);
      SaveFrame(camera, texture, canvases, name + "-gradient-small");
      Call(ShapeNumbersOf(shapes), "Write", "width", OverlayVariable.FromNumber(420));
      Call(ShapeNumbersOf(shapes), "Write", "height", OverlayVariable.FromNumber(240));
      Call(shapes, "Render");
      Settle();

      // Done keeps the gradient; the shape draws it with its texture.
      shapes.Close(true, true);
      Assert(!shapes.IsOpen && shape.Shape.Fill.IsGradient, "Done keeps the gradient");
      RectTransform shapeTransform = (RectTransform)typeof(OverlayElementTarget).GetProperty("Rect", Private).GetValue(shape);
      ShapeGraphic graphic = shapeTransform.GetComponent<ShapeGraphic>();
      Assert(graphic != null && graphic.mainTexture != null && graphic.mainTexture.name == "Element gradient", "the shape draws from its gradient texture");

      // A text run with a gradient colors its glyphs across the text's box.
      TextOverlayElement fps = host.LayoutTargets.OfType<TextOverlayElement>().Single(target => target.Id == "fps");
      OverlayColorSettings textColor = OverlayColorSettings.Custom("#FFFFFF");
      OverlayGradientEdits.AddStop(textColor, 1, "#BA712B");
      textColor.Gradient.Stops[1].Hex = "#4F8DF7";
      List<OverlayColorSettings> original = fps.Settings.Text.Runs.Select(run => run.Color.Clone()).ToList();
      foreach (OverlayTextRun run in fps.Settings.Text.Runs)
        run.Color = textColor.Clone();
      fps.Apply();
      TextMeshProUGUI fpsText = Field<TextMeshProUGUI>(fps, "_text");
      fpsText.ForceMeshUpdate();
      TMP_TextInfo info = fpsText.textInfo;
      Color32 first = default, last = default;
      for (int i = 0, seen = 0; i < info.characterCount; i++)
      {
        TMP_CharacterInfo character = info.characterInfo[i];
        if (!character.isVisible)
          continue;
        Color32 color = info.meshInfo[character.materialReferenceIndex].colors32[character.vertexIndex];
        if (seen++ == 0)
          first = color;
        last = info.meshInfo[character.materialReferenceIndex].colors32[character.vertexIndex + 2];
      }
      Assert(first.r > 200 && first.b > 200 && last.b > last.r, "the text runs from white into blue: " + first + " → " + last);
      SaveFrame(camera, texture, canvases, name + "-gradient-text");
      // A growing value keeps the colors it has at rest.
      OverlayTextRun fpsRun = fps.Settings.Text.Runs.Last(run => run.IsValue);
      fpsRun.Animate = true;
      fps.Apply();
      host.Values.Set(OverlayValueId.Fps, new System.Text.StringBuilder("87654"), 87654);
      host.Tick();
      fpsText.ForceMeshUpdate();
      OverlayTextPulse fpsPulse = Field<OverlayTextPulse>(fps, "_pulse");
      Assert(fpsPulse.Playing, "an animated FPS grows on a new value");
      Color32[] growingColors = GlyphColors(fpsText.textInfo);
      fpsPulse.Stop();
      fpsText.ForceMeshUpdate();
      Color32[] restingColors = GlyphColors(fpsText.textInfo);
      Assert(growingColors.Length > 0 && growingColors.SequenceEqual(restingColors), "a growing value keeps the gradient it has at rest");
      fpsRun.Animate = false;
      // The checks after these expect the text as the layout's history knows it.
      for (int i = 0; i < original.Count; i++)
        fps.Settings.Text.Runs[i].Color = original[i];
      fps.Apply();

      // The text editor: the same bar makes the whole text a gradient, with
      // handles over the text's box; Cancel leaves the text as it was.
      Select(editor, IndexOf(editor, fps));
      PressEdit(editor);
      TextEditorScreen text = (TextEditorScreen)Kind<TextKind>(editor).Editor;
      Assert(text.IsOpen, "Edit text opens the text editor");
      Call(TextColorOf(text), "OpenPicker");
      UiColorPicker textPicker = TextColorOf(text).Picker;
      textPicker.Model.SetSource(false);
      Settle();
      RectTransform textBar = Field<RectTransform>(textPicker.Gradient, "_bar");
      Invoke(textPicker.Gradient, "BarDown", Pointer(BarPoint(textBar, 1)));
      Invoke(textPicker.Gradient, "BarDrag", Pointer(BarPoint(textBar, 1)));
      Settle();
      OverlayColorSettings range = TextDocumentOf(text).RangeColor();
      GradientHandles textHandles = Field<TextGradient>(text, "_gradient").Handles;
      Assert(range.IsGradient && textPicker.Gradient.IsGradient && textHandles.Visible, "the text's color bar makes a gradient with handles over the text");
      TextMeshProUGUI preview = TextPreviewOf(text).Text;
      Assert(OverlayTextGradients.Box(preview.textInfo, out Rect box) && box.width > 0, "the handles measure the text's box");
      Rect textRect = ScreenRectOf(preview.rectTransform);
      Vector2 origin = Field<Vector2>(textHandles, "_origin");
      float unitScale = Field<float>(textHandles, "_scale");
      Vector2 originScreen = new Vector2(origin.x, Screen.height / Field<EditorFramePanel>(text, "_frame").Scale - origin.y) * Field<EditorFramePanel>(text, "_frame").Scale;
      RectTransform handleRoot = Field<RectTransform>(textHandles, "_root");
      Rect angleRect = ScreenRectOf(textHandles.Angle.Root);
      Assert(Mathf.Abs(originScreen.x - textRect.xMin) < 3 && originScreen.y >= textRect.yMax - 3 && originScreen.y <= textRect.yMax + 6
        && angleRect.Overlaps(new Rect(textRect.x - 80, textRect.y - 80, textRect.width + 160, textRect.height + 160)),
        "the handles and the angle sit over the text: origin " + origin + " (" + originScreen + ") scale " + unitScale + " text " + textRect
        + " box " + box + " root " + handleRoot.rect + " " + ScreenRectOf(handleRoot) + " angle " + angleRect + " " + textHandles.Angle.Root.anchoredPosition);
      SaveFrame(camera, texture, canvases, name + "-gradient-text-editor");
      Assert(ScreenRectOf(textHandles.Angle.Root) == angleRect, "the handles stay put whatever camera draws the editor");
      text.Close(false, true);
      Assert(!text.IsOpen && !fps.Settings.Text.Runs.Any(run => run.Color.IsGradient), "Cancel leaves the text's colors as they were");
      preferences.ReduceUiMotion = reduceMotion;
    }

    private static Color32[] GlyphColors(TMP_TextInfo info)
    {
      List<Color32> colors = new List<Color32>();
      for (int i = 0; i < info.characterCount; i++)
      {
        TMP_CharacterInfo character = info.characterInfo[i];
        if (!character.isVisible)
          continue;
        for (int corner = 0; corner < 4; corner++)
          colors.Add(info.meshInfo[character.materialReferenceIndex].colors32[character.vertexIndex + corner]);
      }
      return colors.ToArray();
    }

    private static Vector2 BarPoint(RectTransform bar, float position)
    {
      const float inset = 11;
      Rect local = bar.rect;
      Vector3 world = bar.TransformPoint(new Vector3(local.xMin + inset + position * (local.width - inset * 2), local.center.y, 0));
      return RectTransformUtility.WorldToScreenPoint(null, world);
    }

    private static PointerEventData Pointer(Vector2 screen) =>
      new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };

    private static void Invoke(object target, string method, params object[] args) =>
      target.GetType().GetMethod(method, Private).Invoke(target, args);
  }
}

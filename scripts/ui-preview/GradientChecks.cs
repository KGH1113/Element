using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Editor
{
  // Gradients in the overlay editors: the color bar, kinds, handles with
  // snapping, the highlight's opacity-only stops, and how shapes and texts
  // draw them afterwards.
  public static partial class SettingsPreview
  {
    private static void CheckGradients(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      Call(editor, "AddShape", OverlayShapeKind.Square);
      ShapeOverlayElement shape = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      OverlayShapeEditor shapes = Field<OverlayShapeEditor>(editor, "_shapeEditor");
      Assert(shapes.IsOpen, "a new square opens its editor");
      Call(shapes, "Write", "width", OverlayFormula.FromNumber(420));
      Call(shapes, "Write", "height", OverlayFormula.FromNumber(240));
      Call(shapes, "Render");
      // With motion, opening the picker glides the shape aside from where it was.
      ElementSettings preferences = Field<ElementSettings>(editor, "_preferences");
      bool reduceMotion = preferences.ReduceUiMotion;
      preferences.ReduceUiMotion = false;
      Rect resting = ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"));
      Call(shapes, "OpenColor", false);
      OverlayGlide glide = Field<OverlayGlide>(shapes, "_space");
      Assert(glide.Moving && ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect")) == resting,
        "opening the picker starts the shape gliding from where it was, without a jump");
      Call(shapes, "OpenColor", false);
      // The checks below measure where things land, so they settle at once.
      preferences.ReduceUiMotion = true;
      Call(shapes, "OpenColor", false);
      SettingsColorPicker picker = Field<SettingsColorPicker>(shapes, "_picker");
      OverlayGradientHandles handles = Field<OverlayGradientHandles>(shapes, "_handles");
      Assert(picker.Visible && picker.GradientAvailable && !picker.IsGradient && !handles.Visible,
        "the fill picker has a color bar and starts solid, with no handles");
      RectTransform bar = Field<RectTransform>(picker, "_bar");
      Assert(bar.gameObject.activeInHierarchy && Field<RectTransform>(picker, "_kindRow").gameObject.activeSelf == false,
        "a solid color shows only the bar; the kinds wait for a second color");

      // A new shape follows the highlight; a custom color shows the whole picker.
      Call(picker, "SetSource", false);

      Rect solidBar = ScreenRectOf(bar), solidPicker = ScreenRectOf(picker.Root);

      // Hovering the solid bar says what a click does, right under it.
      Invoke(picker, "BarHover", Pointer(BarPoint(bar, .7f)));
      RectTransform tip = Field<RectTransform>(picker, "_tip");
      Assert(tip.gameObject.activeSelf && ScreenRectOf(tip).yMax <= ScreenRectOf(bar).yMin + .5f
        && Mathf.Abs(ScreenRectOf(tip).center.x - BarPoint(bar, .7f).x) < ScreenRectOf(tip).width,
        "hovering the bar shows the add tip under the pointer");
      SaveFrame(camera, texture, canvases, name + "-gradient-hover");

      // A click makes a gradient whose new color shows at once.
      Invoke(picker, "BarDown", Pointer(BarPoint(bar, .7f)));
      Invoke(picker, "BarDrag", Pointer(BarPoint(bar, .7f)));
      OverlayColorSettings fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(fill.IsGradient && fill.Gradient.Stops.Count == 2 && picker.IsGradient && picker.SelectedStop == 1,
        "a click on the bar adds a color, picks it and makes a gradient");
      Assert(ScreenRectOf(bar) == solidBar && ScreenRectOf(picker.Root) == solidPicker,
        "turning into a gradient keeps the picker and its bar in place");
      Assert(fill.Gradient.Stops[1].Alpha > .99f && Mathf.Abs(fill.Gradient.Stops[1].Position - .7f) < .03f,
        "the new color is opaque and lands where it was clicked: " + fill.Gradient.Stops[1].Alpha + " at " + fill.Gradient.Stops[1].Position);
      Assert(fill.Gradient.Stops[1].Hex != fill.Gradient.Stops[0].Hex, "the new color is a neighbor, not the same color");
      Assert(!tip.gameObject.activeSelf && Field<RectTransform>(picker, "_kindRow").gameObject.activeSelf && handles.Visible,
        "the kinds and the handles show with two colors");
      OverlayInputDock dock0 = Field<OverlayInputDock>(shapes, "_dock");
      OverlayFormulaField position = Field<OverlayFormulaField>(shapes, "_position");
      Assert(position.Text == "70", "the picked color's position pill says where it is: " + position.Text);
      Rect pill = ScreenRectOf(position.Root), barRect = ScreenRectOf(bar);
      Rect delete = ScreenRectOf((RectTransform)Field<Button>(picker, "_delete").transform);
      Assert(pill.yMin >= barRect.yMax - .5f && delete.xMin >= pill.xMax && Mathf.Abs(delete.center.y - pill.center.y) < 2,
        "the pill sits over the bar with the delete button beside it");
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "picker label fits: " + label.text);
      }
      Assert(ScreenRectOf(picker.Root).yMax <= Screen.height && ScreenRectOf(picker.Root).yMin >= 0, "the picker stays on screen");
      // 16:9 and 16:10 screens leave 310 frame units above the keypad.
      Assert(picker.Height <= 310 && picker.CurrentWidth > 500, "the two-column picker fits above the keypad on wide screens: " + picker.Height);
      Rect pickerRect = ScreenRectOf(picker.Root);
      Assert(!pickerRect.Overlaps(ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect")))
        && !pickerRect.Overlaps(ScreenRectOf(Field<RectTransform>(shapes, "_bar")))
        && !pickerRect.Overlaps(ScreenRectOf(dock0.Keypad.Root)),
        "the shape and its bar move clear of the picker, which stays off the keypad");
      SaveFrame(camera, texture, canvases, name + "-gradient-linear");

      // The ends of the line are handles only: nothing covers them.
      Rect shapeRect = ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"));
      List<Button> arcs = Field<List<Button>>(shapes, "_arcs");
      Assert(arcs.All(arc => !arc.gameObject.activeSelf), "corner arcs step aside while the handles show");
      Vector2 start = new Vector2(shapeRect.xMin, shapeRect.center.y);
      Assert(handles.Contains(start) && handles.SquareAt(start) < 0, "the start handle is a handle, not a color");
      int square = Enumerable.Range(0, 40).Select(i => handles.SquareAt(start + new Vector2(0, -i))).FirstOrDefault(i => i >= 0);
      Assert(handles.SquareAt(start + new Vector2(0, -24)) == 0 || square == 0, "the first color hangs just below its end of the line");

      // Each kind is a tile drawn in the colors; radial adds a width handle.
      Field<Image[]>(picker, "_kindFrames")[1].GetComponent<Button>().onClick.Invoke();
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(fill.Gradient.Kind == OverlayGradientKind.Radial && Mathf.Abs(fill.Gradient.StartX - .5f) < .01f, "a kind tile switches the kind");

      // Dragging the end handle to the top right corner sticks to it.
      object grips = Field<object>(handles, "_grips");
      Vector2 end = new Vector2(shapeRect.xMax, shapeRect.center.y);
      Invoke(handles, "GripDown", 1, Pointer(end));
      // A few frame units short of the corner, whatever the screen's scale.
      float near = 3 * Field<OverlayEditorFrame>(shapes, "_screen").Scale;
      Invoke(handles, "GripDrag", 1, Pointer(new Vector2(shapeRect.xMax - near, shapeRect.yMax - near * 1.3f)));
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(Mathf.Abs(fill.Gradient.EndX - 1) < .001f && Mathf.Abs(fill.Gradient.EndY) < .001f,
        "the end handle sticks to the box's corner: " + fill.Gradient.EndX + ", " + fill.Gradient.EndY);
      SaveFrame(camera, texture, canvases, name + "-gradient-snap");
      Invoke(handles, "EndDrag");
      Assert(grips != null, "handles exist");

      // The angle pill types in the dock; 0° points the gradient up.
      Call(shapes, "Activate", "fill-angle");
      OverlayInputDock dock = Field<OverlayInputDock>(shapes, "_dock");
      Assert(dock.Owner == "fill-angle", "the angle opens in the dock");
      Call(shapes, "Write", "fill-angle", OverlayFormula.FromNumber(0));
      Call(shapes, "FinishEditing");
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      Assert(Mathf.Abs(OverlayGradientMath.Angle(fill.Gradient, 420, 240)) < .5f, "a typed angle turns the gradient");

      // Following the highlight leaves only opacities; nothing on the left moves.
      Rect barBefore = ScreenRectOf(bar), pickerBefore = ScreenRectOf(picker.Root);
      Call(picker, "SetSource", true);
      Assert(ScreenRectOf(bar) == barBefore && ScreenRectOf(picker.Root) == pickerBefore,
        "switching to the highlight keeps the picker and its bar in place");
      fill = Field<OverlayShapeSettings>(shapes, "_shape").Fill;
      List<int> order = OverlayGradientEdits.Order(fill.Gradient);
      Assert(fill.Highlight && fill.IsGradient && fill.Gradient.Stops[order[order.Count - 1]].Alpha < fill.Gradient.Stops[order[0]].Alpha,
        "the highlight gradient fades instead of vanishing");
      Assert(!Field<RectTransform>(picker, "_planeRect").gameObject.activeSelf && Field<TextMeshProUGUI>(picker, "_highlightNote").gameObject.activeSelf,
        "only the opacity and a note show while following the highlight");
      SaveFrame(camera, texture, canvases, name + "-gradient-highlight");
      Call(picker, "SetSource", false);

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
      // The checks after these expect the text as the layout's history knows it.
      for (int i = 0; i < original.Count; i++)
        fps.Settings.Text.Runs[i].Color = original[i];
      fps.Apply();

      // The text editor: the same bar makes the whole text a gradient, with
      // handles over the text's box; Cancel leaves the text as it was.
      Select(editor, IndexOf(editor, fps));
      Call(editor, "UpdateToolbar");
      Field<Button>(editor, "_editContent").onClick.Invoke();
      OverlayTextEditor text = Field<OverlayTextEditor>(editor, "_textEditor");
      Assert(text.IsOpen, "Edit text opens the text editor");
      Call(text, "OpenPicker");
      SettingsColorPicker textPicker = Field<SettingsColorPicker>(text, "_picker");
      Call(textPicker, "SetSource", false);
      RectTransform textBar = Field<RectTransform>(textPicker, "_bar");
      Invoke(textPicker, "BarDown", Pointer(BarPoint(textBar, 1)));
      Invoke(textPicker, "BarDrag", Pointer(BarPoint(textBar, 1)));
      OverlayColorSettings range = (OverlayColorSettings)text.GetType().GetMethod("RangeColor", Private).Invoke(text, null);
      OverlayGradientHandles textHandles = Field<OverlayGradientHandles>(text, "_handles");
      Assert(range.IsGradient && textPicker.IsGradient && textHandles.Visible, "the text's color bar makes a gradient with handles over the text");
      TextMeshProUGUI preview = Field<TextMeshProUGUI>(text, "_preview");
      Assert(OverlayTextGradients.Box(preview.textInfo, out Rect box) && box.width > 0, "the handles measure the text's box");
      Rect textRect = ScreenRectOf(preview.rectTransform);
      Vector2 origin = Field<Vector2>(textHandles, "_origin");
      float unitScale = Field<float>(textHandles, "_scale");
      Vector2 originScreen = new Vector2(origin.x, Screen.height / Field<OverlayEditorFrame>(text, "_screen").Scale - origin.y) * Field<OverlayEditorFrame>(text, "_screen").Scale;
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

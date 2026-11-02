using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  // Production text and shape elements on a real overlay canvas: placement,
  // value redraws, converted built-ins and the combo fit.
  internal static class OverlayTargetChecks
  {
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static int Run(TMP_FontAsset font, Material material)
    {
      int checks = 0;
      Action<bool, string> check = (condition, message) =>
      {
        if (!condition)
          throw new InvalidOperationException("Overlay element: " + message);
        checks++;
      };
      OverlayCanvasHost host = new OverlayCanvasHost();
      try
      {
        host.SetFont(font, material);
        List<OverlayLayoutSettings> layouts = new List<OverlayLayoutSettings>();
        OverlayElementMigration.Migrate(layouts);
        layouts.Add(new OverlayLayoutSettings
        {
          Id = "combo:moved",
          TemplateId = "combo",
          Customized = true,
          Scale = 1.5f,
          LayoutVersion = 1,
          Anchor = Anchor9.Top,
          OffsetY = 300,
        });
        OverlayElementMigration.Migrate(layouts);
        host.ConfigureLayouts(layouts);
        host.SetLayoutPreview(true);
        Canvas.ForceUpdateCanvases();
        host.ApplyLayouts();
        Canvas.ForceUpdateCanvases();
        float unit = OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
        TextOverlayElement fps = Text(host, "fps");
        TextMeshProUGUI fpsText = Field<TextMeshProUGUI>(fps, "_text");
        check(fpsText.text.Contains("FPS | ") && fpsText.text.Contains("120"), "preview shows the FPS sample: " + fpsText.text);
        Rect fpsBounds = fps.Bounds;
        check(Mathf.Abs(fpsBounds.x - 16 * unit) < 6 * unit, "converted FPS starts where the built-in did: " + fpsBounds.x);
        check(fpsBounds.y > 121 * unit && fpsBounds.yMax < 151 * unit + 4, "converted FPS keeps its built-in row: " + fpsBounds);
        Rect tile = Text(host, "tile-bpm").Bounds;
        check(Mathf.Abs(tile.xMax - (Screen.width - 16 * unit)) < 6 * unit, "BPM rows stay right aligned: " + tile);

        host.Values.Set(OverlayValueId.Fps, new System.Text.StringBuilder("1234"), 1234);
        host.Tick();
        check(fpsText.text.Contains("1234"), "a value change redraws only texts that use it");
        check(!Text(host, "progress").Settings.Text.Runs.Any(run => run.Value == "fps"), "progress does not use FPS");

        fps.SetBounds(new Rect(fpsBounds.x + 100, fpsBounds.y + 50, fpsBounds.width * 2, fpsBounds.height * 2));
        fps.CommitLayout(true);
        Canvas.ForceUpdateCanvases();
        Rect moved = fps.Bounds;
        check(fps.Settings.Customized && fps.Settings.LayoutVersion == 1 && Mathf.Abs(fps.Settings.Scale - 2) < .01f,
          "moving a text saves an anchored placement and its scale");
        fps.Apply();
        Canvas.ForceUpdateCanvases();
        check(Same(moved, fps.Bounds, 1), "an applied anchored text stays put");
        host.Values.Set(OverlayValueId.Fps, new System.Text.StringBuilder("1234567"), 1234567);
        host.Tick();
        Canvas.ForceUpdateCanvases();
        check(Mathf.Abs(fps.Bounds.x - moved.x) < 1 && fps.Bounds.width > moved.width,
          "a longer value grows away from the left anchor");

        OverlayLayoutSettings literal = new OverlayLayoutSettings
        {
          Id = "text:literal",
          TemplateId = OverlayElementKinds.Text,
          Text = new OverlayTextSettings { Runs = { OverlayTextRun.Literal("<b>x", OverlayColorSettings.Custom("#FFFFFF")) } },
        };
        layouts.Add(literal);
        host.ConfigureLayouts(layouts);
        TextMeshProUGUI literalText = Field<TextMeshProUGUI>(Text(host, "text:literal"), "_text");
        literalText.ForceMeshUpdate();
        check(literalText.textInfo.characterCount == 4, "typed angle brackets stay literal text");

        // Calculated values: × before +, times stay times, and nothing shows without data.
        OverlayLayoutSettings math = new OverlayLayoutSettings
        {
          Id = "text:math",
          TemplateId = OverlayElementKinds.Text,
          Text = new OverlayTextSettings
          {
            Runs =
            {
              OverlayTextRun.ValueRun("{combo}*2+1", OverlayColorSettings.Custom("#FFFFFF")),
              OverlayTextRun.Literal(" ", OverlayColorSettings.Custom("#FFFFFF")),
              OverlayTextRun.ValueRun("{time-total}-{time-current}", OverlayColorSettings.Custom("#FFFFFF")),
            },
          },
        };
        layouts.Add(math);
        host.ConfigureLayouts(layouts);
        TextOverlayElement mathElement = Text(host, "text:math");
        TextMeshProUGUI mathText = Field<TextMeshProUGUI>(mathElement, "_text");
        check(mathText.text.Contains("241") && mathText.text.Contains("2:21"), "formulas show their results with samples: " + mathText.text);
        host.Values.Set(OverlayValueId.Combo, new System.Text.StringBuilder("10"), 10);
        host.Tick();
        check(mathText.text.Contains(">21<") || mathText.text.Contains("21</"), "a live value recalculates: " + mathText.text);
        check(mathElement.UsesCombo, "a calculated combo still pulses");
        mathElement.SetPreview(false);
        host.Values.Clear(OverlayValueId.TotalTime);
        host.Tick();
        check(!mathText.text.Contains(":"), "a formula without data shows nothing in the game: " + mathText.text);
        mathElement.SetPreview(true);

        TextOverlayElement combo = Text(host, "combo:moved");
        TextOverlayElement title = Text(host, "combo:moved/title");
        for (int i = 0; i < 3 && (combo.PendingFit || title.PendingFit); i++)
        {
          Canvas.ForceUpdateCanvases();
          host.Tick();
        }
        check(!combo.PendingFit && !title.PendingFit && combo.Settings.Customized && title.Settings.Customized,
          "a moved combo's title and number are placed once and saved");
        check(title.Bounds.yMax <= combo.Bounds.center.y && Mathf.Abs(title.Bounds.center.x - combo.Bounds.center.x) < 4 * unit,
          "the combo title stays above its number");
        check(Mathf.Abs(combo.Settings.Scale - 1.5f * 78f / 25f) < .05f, "the fitted number keeps the old scale and font size");
        OverlayTextPulse pulse = Field<OverlayTextPulse>(combo, "_pulse");
        combo.Pulse();
        check(pulse.enabled, "a combo bump pulses the number");

        ShapeOverlayElement shape = host.AddShape();
        Canvas.ForceUpdateCanvases();
        Rect box = shape.Bounds;
        check(Mathf.Abs(box.width - OverlayShapeGeometry.DefaultSize * unit) < 1, "a new shape is 160 reference units wide");
        check(shape.Bounds.center.x > Screen.width * .45f && shape.Bounds.center.x < Screen.width * .55f, "a new shape starts centered");
        ShapeGraphic graphic = Field<ShapeGraphic>(shape, "_graphic");
        check(Vertices(graphic) == 8, "a square draws its fill and a one-pixel soft edge");
        // Numbers tied to values follow them: a progress bar.
        shape.Shape.WidthFormula = "{progress-current}*3";
        shape.Apply();
        Canvas.ForceUpdateCanvases();
        check(Mathf.Abs(shape.Bounds.width - 144 * unit) < 1, "a width formula uses the sample while editing: " + shape.Bounds.width / unit);
        check(shape.ValueMask == OverlayValueCatalog.Bit(OverlayValueId.ProgressCurrent), "a shape knows the values it uses");
        host.Values.Set(OverlayValueId.ProgressCurrent, new System.Text.StringBuilder("60"), 60);
        host.Tick();
        Canvas.ForceUpdateCanvases();
        check(Mathf.Abs(shape.Bounds.width - 180 * unit) < 1, "a live value resizes the shape: " + shape.Bounds.width / unit);
        check(Mathf.Abs(shape.Shape.Width - OverlayShapeGeometry.DefaultSize) < .01f, "the plain width stays underneath the formula");
        shape.Shape.WidthFormula = "";
        shape.Apply();
        shape.Shape.Points[0].Radius = 40;
        shape.Shape.StrokeWidth = 6;
        shape.Redraw();
        check(Vertices(graphic) > 40, "rounded corners and a border add geometry");
        OverlayShapeGeometry.SetKind(shape.Shape, OverlayShapeKind.Circle);
        shape.Apply();
        check(Vertices(graphic) > 40, "circles draw as ellipses in their box");
        Transform shapes = host.Root.transform.Find("Shapes"), texts = host.Root.transform.Find("Texts");
        check(shapes.GetSiblingIndex() < texts.GetSiblingIndex(), "shapes draw behind every text");
      }
      finally
      {
        if (host.Root != null)
          Object.DestroyImmediate(host.Root);
      }
      return checks;
    }

    private static TextOverlayElement Text(OverlayCanvasHost host, string id) =>
      host.LayoutTargets.OfType<TextOverlayElement>().Single(element => element.Id == id);

    private static int Vertices(Graphic graphic)
    {
      using (VertexHelper mesh = new VertexHelper())
      {
        typeof(ShapeGraphic).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
        return mesh.currentVertCount;
      }
    }

    private static bool Same(Rect a, Rect b, float tolerance) =>
      Mathf.Abs(a.x - b.x) < tolerance && Mathf.Abs(a.y - b.y) < tolerance
      && Mathf.Abs(a.width - b.width) < tolerance && Mathf.Abs(a.height - b.height) < tolerance;

    private static T Field<T>(object target, string name)
    {
      for (Type type = target.GetType(); type != null; type = type.BaseType)
      {
        FieldInfo field = type.GetField(name, Private);
        if (field != null)
          return (T)field.GetValue(target);
      }
      throw new MissingFieldException(target.GetType().Name, name);
    }
  }
}

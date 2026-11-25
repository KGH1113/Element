using System.Linq;
using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Ui.Theme;
using Element.Presentation.OverlayEditor.KeyViewers;
using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.FloatView;
using UnityEngine;

namespace Element.Editor
{
  public static partial class SettingsPreview
  {
    private static void CheckFloatView(OverlayEditorScreen editor, Camera camera, RenderTexture texture, Canvas[] canvases, string name)
    {
      var session = Session(editor);
      Select(editor, -1);
      var original = new LayoutSnapshot(session.Host.LayoutSettings);
      var visible = new[] { "progress-bar", "progress-bar-track", "progress-bar-fill", "fps", "combo", "tile-bpm" };
      foreach (var target in session.Host.LayoutTargets)
        target.SetEnabled(visible.Contains(target.Id));
      session.History.Reset(session.Host.LayoutSettings);
      var viewer = session.KeyViewers.Targets.Last(item => item.Enabled);
      Rect home = viewer.Bounds;
      viewer.SetBounds(new Rect(45, session.ViewHeight * .49f, 148, 96));
      Rect keys = viewer.SelectionBounds;
      var sample = NativeViewerMock.Create("foot", session.Root.transform, session.Font, false);
      sample.anchoredPosition = new Vector2(keys.x, -keys.y);
      sample.localScale = new Vector3(keys.width / sample.rect.width, keys.height / sample.rect.height, 1);
      sample.SetAsFirstSibling();
      // A small game-camera fixture distinguishes the tilted world from the editor's unmoved chrome.
      var course = new GameObject("Preview course");
      var tile = new Material(Shader.Find("Unlit/Color")) { color = new Color(.25f, .30f, .39f) };
      var fire = new Material(tile) { color = new Color(.99f, .36f, .23f) };
      var ice = new Material(tile) { color = new Color(.23f, .65f, 1) };
      for (int i = 0; i < 16; i++)
      {
        var part = GameObject.CreatePrimitive(i < 14 ? PrimitiveType.Cube : PrimitiveType.Sphere);
        part.transform.SetParent(course.transform);
        int slot = i < 14 ? i : i - 10;
        part.transform.position = new Vector3((slot-6)*.82f, Mathf.Min(2, Mathf.Max(0, slot-6))*.82f, i<14 ? 6 : 5);
        part.transform.localScale = i<14 ? new Vector3(.78f,.78f,.1f) : Vector3.one * .66f;
        part.GetComponent<Renderer>().sharedMaterial = i<14 ? tile : i==14 ? fire : ice;
      }
      var button = ToolbarButton(editor, "Float view");
      SaveFrame(camera, texture, canvases, name + "-float-off");
      sample.gameObject.SetActive(false);
      var gameCamera = new GameObject("Fixture game camera").AddComponent<Camera>();
      gameCamera.CopyFrom(camera);
      gameCamera.targetTexture = null;
      Press(button);
      var scene = Field<FloatScene>(editor, "_float");
      Assert(scene != null && editor.NativeExclusionBounds.width == session.ViewWidth, "float view owns the canvas and covers native windows");
      gameCamera.targetTexture = texture;
      gameCamera.Render();
      for (int i=0; i<40; i++) scene.Advance(.016f);
      Settle();
      SaveFrame(camera, texture, canvases, name + "-float-on");
      CheckFloatHover(editor, scene, camera, texture, canvases, name);
      bool reduced = session.Preferences.ReduceUiMotion;
      session.Preferences.ReduceUiMotion = false;
      var orbit = Field<FloatOrbit>(scene, "_orbit");
      float homeYaw = orbit.Yaw, homePitch = orbit.Pitch;
      var space = new Vector2(8, 10);
      scene.Begin(space);
      scene.Move(space + new Vector2(66, 24));
      scene.End(space + new Vector2(66, 24));
      float released = orbit.Yaw;
      scene.Advance(.016f);
      Assert(released > homeYaw && orbit.Yaw > released && orbit.Pitch < homePitch,
        "dragging empty space orbits in two axes and coasts on release");
      var chrome = editor.ToolbarBounds.center;
      Assert(!scene.Accepts(new Vector2(chrome.x/session.ViewWidth, 1-chrome.y/session.ViewHeight))
        && scene.Accepts(new Vector2(.5f,.6f)), "native gestures start on the canvas and leave toolbar controls alone");
      scene.Orbit(new Vector2(0, 4));
      for (int i = 0; i < 20; i++) scene.Magnify(.2f);
      Assert(orbit.Pitch > 75 && orbit.Pitch < 84 && orbit.Zoom > 1.9f && orbit.Zoom < 2.14f,
        "pitch and pinch resist movement beyond their normal range");
      float beforeTwist = orbit.Yaw;
      scene.Rotate(18);
      Assert(Mathf.Abs(orbit.Yaw-beforeTwist-18)<.01f, "a native rotation turns the view around its vertical axis");
      scene.Begin(space); scene.End(space);
      scene.Begin(space); scene.End(space);
      for (int i = 0; i < 100; i++) scene.Advance(.016f);
      Assert(Mathf.Abs(orbit.Yaw-homeYaw)<.01f && Mathf.Abs(orbit.Pitch-homePitch)<.01f && Mathf.Abs(orbit.Zoom-1)<.01f,
        "a double-click on empty space restores the original viewpoint");
      session.Preferences.ReduceUiMotion = reduced;
      var order = new FloatOrder(session);
      var before = order.Read();
      var shape = before.First(target => target.TemplateId == "shape");
      int index = before.IndexOf(shape);
      var layers = Field<List<FloatLayer>>(scene, "_layers");
      Vector2 start = PointerOn(layers.Single(layer => layer.Target == shape).Face);
      scene.Begin(start);
      scene.Move(start - Vector2.up * 24);
      scene.End(start - Vector2.up * 24);
      Assert(order.Read()[index+1] == shape, "lifting moves one visible shape toward the front");
      Call(editor, "Undo");
      Assert(order.Read().SequenceEqual(before), "one undo restores the depth order");
      Call(editor, "Redo");
      Assert(order.Read()[index+1] == shape, "redo restores the lifted depth");
      Call(editor, "Undo");
      Press(ToolbarButton(editor, "Float view"));
      for (int i=0; i<60; i++) scene.Advance(.016f);
      Call(editor, "AdvanceFloatView", .016f);
      Assert(Field<FloatScene>(editor, "_float") == null, "closing releases the float scene");
      Object.DestroyImmediate(sample.gameObject);
      viewer.SetBounds(home);
      original.Restore(session.Host.LayoutSettings);
      Call(editor, "RefreshLayouts");
      session.History.Reset(session.Host.LayoutSettings);
      Object.DestroyImmediate(course);
      Object.DestroyImmediate(gameCamera.gameObject);
      Object.DestroyImmediate(tile);
      Object.DestroyImmediate(fire);
      Object.DestroyImmediate(ice);
    }

    private static void CheckFloatHover(OverlayEditorScreen editor, FloatScene scene, Camera camera,
      RenderTexture texture, Canvas[] canvases, string name)
    {
      var session = Session(editor);
      var layers = Field<List<FloatLayer>>(scene, "_layers");
      var fps = layers.Single(layer => layer.Target.Id == "fps");
      var hover = Field<FloatHover>(scene, "_hover");
      var tag = Field<RectTransform>(hover, "_tag");
      var edges = Field<RectTransform[]>(hover, "_edges");
      var caption = tag.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
      bool reduced = session.Preferences.ReduceUiMotion;
      session.Preferences.ReduceUiMotion = true;
      // Measure outward from the rendered edge, perpendicular to the tilted baseline.
      Vector3[] corners = new Vector3[4];
      fps.Face.GetWorldCorners(corners);
      Vector2 edge = (corners[0] + corners[3]) * .5f;
      Vector2 along = (corners[3] - corners[0]).normalized;
      Vector2 Outside(float pixels)
      {
        Vector2 p = edge + new Vector2(along.y, -along.x) * (pixels * session.UiScale);
        return new Vector2(p.x, session.ViewHeight - p.y);
      }
      Select(editor, IndexOf(editor, fps.Target));
      string expected = ToolbarModel(editor).Selection.Value;
      Select(editor, -1);
      foreach (int distance in new[] { 12, 24 })
      {
        Vector2 point = Outside(distance);
        scene.Hover(point);
        Assert(hover.Target == fps && tag.gameObject.activeInHierarchy && caption.text == expected &&
          edges.All(rect => rect.gameObject.activeInHierarchy && rect.GetComponent<UnityEngine.UI.Image>().color == UiPalette.Accent),
          "the off-edge pointer previews the toolbar name and accent outline before pressing");
        Assert(fps.Visual.anchoredPosition == Vector2.zero, "reduced motion leaves the highlighted text in place");
        scene.Begin(point);
        Assert(Field<FloatLayer>(scene, "_drag") == fps && !Field<bool>(scene, "_orbiting"),
          "pressing " + distance + " pixels outside thin text grabs the highlighted element");
        scene.End(point);
      }
      Vector2 empty = Outside(30);
      scene.Hover(empty);
      Assert(hover.Target == null && !tag.gameObject.activeInHierarchy && edges.All(rect => !rect.gameObject.activeInHierarchy),
        "an empty patch of the game has neither name tag nor outline");
      scene.Begin(empty);
      Assert(Field<bool>(scene, "_orbiting") && Field<FloatLayer>(scene, "_drag") == null,
        "pressing beyond the proximity limit starts an orbit");
      scene.CancelGesture();
      // Compare two thin projected rectangles on the same patch of canvas.
      var bpm = layers.Single(layer => layer.Target.Id == "tile-bpm");
      var planeToScreen = Field<RectTransform>(scene, "_root").worldToLocalMatrix;
      Vector2 between = Outside(9);
      bpm.Footprint.Project(fps.Face, planeToScreen);
      scene.Hover(between);
      var front = layers.IndexOf(bpm) > layers.IndexOf(fps) ? bpm : fps;
      Assert(hover.Target == front, "equal edge distances pick the face in front");
      scene.ClearHover();
      Vector2 outward = Outside(1) - Outside(0);
      var separation = Matrix4x4.Translate(new Vector3(outward.x, -outward.y, 0) * 13);
      bpm.Footprint.Project(fps.Face, separation * planeToScreen);
      // Beyond the right end of the two projected bottom edges, the closest
      // edge can be on the back face even inside both padded hit regions.
      Vector2 tip = new Vector2(corners[3].x, session.ViewHeight - corners[3].y);
      Vector2 tangent = new Vector2(along.x, -along.y);
      Vector2 nearFps = tip + tangent * (6 * session.UiScale) + outward * 2;
      scene.Hover(nearFps);
      Assert(hover.Target == fps, "the nearest edge wins between distinct faces");
      scene.ClearHover();
      bpm.Footprint.Project(bpm.Face, planeToScreen);
      var keys = layers.Single(layer => layer.Target == session.KeyViewers.Targets.Last(target => target.Enabled));
      session.Preferences.ReduceUiMotion = false;
      Vector2 center = PointerOn(keys.Face);
      scene.Hover(center);
      Assert(hover.Target == keys && Vector2.Distance(PointerOn(keys.Visual), center + Vector2.down * 3 * session.UiScale) < .1f,
        "the hovered artwork lifts three pixels while its hit rectangle stays still");
      SaveFrame(camera, texture, canvases, name + "-float-hover");
      scene.Hover(editor.ToolbarBounds.center);
      Assert(hover.Target == null && keys.Visual.anchoredPosition == Vector2.zero, "the toolbar clears the highlight and lift");
      session.Preferences.ReduceUiMotion = reduced;
    }
  }
}

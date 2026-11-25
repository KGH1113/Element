using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.KeyViewers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>A visual snapshot shares the rendered meshes without cloning mod scripts or changing live layout.</summary>
internal sealed class FloatLayer
{
  public IOverlayLayoutTarget Target { get; }
  public RectTransform Face { get; }
  public Vector2 Home { get; }
  public float Rank,
    Velocity;

  public FloatLayer(
    CanvasSession session,
    IOverlayLayoutTarget target,
    RectTransform plane,
    RectTransform shadows,
    int rank
  )
  {
    Target = target;
    Rank = rank;
    Rect bounds = target.SelectionBounds;
    if (bounds.width <= 0 || bounds.height <= 0)
      bounds = target.Bounds;
    Face = session.Ui.Rect(target.Id, plane, bounds.x, bounds.y, bounds.width, bounds.height);
    Home = Face.anchoredPosition;
    if (target is OverlayElementTarget element)
    {
      RectTransform shadow = session.Ui.Rect(target.Id, shadows, bounds.x, bounds.y, bounds.width, bounds.height);
      foreach (Graphic graphic in element.Rect.GetComponentsInChildren<Graphic>())
      {
        CanvasRenderer source = graphic.canvasRenderer;
        if (!graphic.isActiveAndEnabled || source.cull || source.materialCount == 0)
          continue;
        Mesh mesh = source.GetMesh();
        if (mesh == null || mesh.vertexCount == 0)
          continue;
        Vector3 point = graphic.rectTransform.position;
        Vector2 offset = new(point.x - bounds.x, Screen.height - point.y - bounds.y);
        Copy(Face, offset, graphic, mesh, source.GetColor());
        Copy(shadow, offset, graphic, mesh, new Color(0, 0, 0, .65f));
      }
    }
    else
    {
      session.Ui.Panel(
        "Key viewer shadow",
        shadows,
        bounds.x,
        bounds.y,
        bounds.width,
        bounds.height,
        new Color(0, 0, 0, .65f)
      );
      var sample = NativeViewerMock.Create(
        target.TemplateId == OverlayElementKinds.FootKeyViewer ? "foot" : "hand",
        Face,
        session.Font,
        false
      );
      sample.localScale = new Vector3(bounds.width / sample.rect.width, bounds.height / sample.rect.height, 1);
    }
  }

  private static void Copy(RectTransform parent, Vector2 offset, Graphic graphic, Mesh mesh, Color tint)
  {
    GameObject obj = new(graphic.name, typeof(RectTransform), typeof(CanvasRenderer));
    var rect = (RectTransform)obj.transform;
    rect.SetParent(parent, false);
    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
    rect.anchoredPosition = new Vector2(offset.x, -offset.y);
    rect.localScale = graphic.rectTransform.lossyScale;
    rect.localRotation = graphic.rectTransform.rotation;
    CanvasRenderer renderer = obj.GetComponent<CanvasRenderer>();
    renderer.SetMesh(mesh);
    Material material = graphic.canvasRenderer.GetMaterial();
    // TMP binds its atlas through the material; Graphic.mainTexture is white.
    renderer.SetMaterial(
      material,
      graphic is TMP_Text || graphic is TMP_SubMeshUI ? material.mainTexture : graphic.mainTexture
    );
    renderer.SetColor(tint);
  }
}

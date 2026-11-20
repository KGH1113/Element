using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Widgets;

/// <summary>
/// One edge of a scroll view fading into the color under it: solid at the
/// edge, clear toward the content, eased so it shows no band. Its corners at
/// the edge round like the base under it, so it never shows past it. Its
/// strength scales the whole fade; at 0 it draws nothing. See
/// <see cref="UiScrollCues"/>.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class UiEdgeFadeGraphic : MaskableGraphic
{
  // How far from the edge each row of the mesh lies, as a share of the
  // fade's height; close together near the edge, where the corners round.
  private static readonly float[] Rows = { 0, .02f, .05f, .1f, .16f, .24f, .34f, .5f, .7f, 1 };

  private bool _top;
  private float _radius;

  /// <summary>How strong it shows, 0 to 1.</summary>
  public float Strength { get; private set; } = -1;

  /// <summary>
  /// A fade <paramref name="height"/> tall along the top or bottom edge of
  /// <paramref name="parent"/>, over what is in it, its corners rounded by <paramref name="radius"/>.
  /// </summary>
  public static UiEdgeFadeGraphic Make(
    RectTransform parent,
    string name,
    Color under,
    bool top,
    float height,
    float radius
  )
  {
    GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
    go.layer = parent.gameObject.layer;
    RectTransform rect = (RectTransform)go.transform;
    rect.SetParent(parent, false);
    rect.anchorMin = new Vector2(0, top ? 1 : 0);
    rect.anchorMax = new Vector2(1, top ? 1 : 0);
    rect.pivot = new Vector2(.5f, top ? 1 : 0);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(0, height);
    rect.SetAsLastSibling();
    UiEdgeFadeGraphic fade = go.AddComponent<UiEdgeFadeGraphic>();
    fade._top = top;
    fade._radius = Mathf.Max(0, radius);
    fade.color = under;
    fade.raycastTarget = false;
    fade.Show(0);
    return fade;
  }

  /// <summary>Shows it at <paramref name="strength"/>, in steps of a hundredth so a fade coming in redraws a few times, not every frame.</summary>
  public void Show(float strength)
  {
    strength = Mathf.Round(Mathf.Clamp01(strength) * 100) / 100;
    if (strength == Strength)
      return;
    Strength = strength;
    bool on = strength > 0;
    if (enabled != on)
      enabled = on;
    if (on)
      SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect r = rectTransform.rect;
    float radius = Mathf.Min(_radius, r.width / 2, r.height);
    for (int i = 0; i < Rows.Length; i++)
    {
      // t runs from the edge (0) to the content (1); the ink thins out smoothly.
      float t = Rows[i];
      float distance = r.height * t;
      float alpha = 1 - t * t * (3 - 2 * t);
      float y = _top ? r.yMax - distance : r.yMin + distance;
      // Within the radius of the edge, the row starts where the base's rounded corner does.
      float inset =
        distance < radius ? radius - Mathf.Sqrt(radius * radius - (radius - distance) * (radius - distance)) : 0;
      Color32 ink = color;
      ink.a = (byte)Mathf.RoundToInt(ink.a * alpha * Mathf.Max(0, Strength));
      mesh.AddVert(new Vector3(r.xMin + inset, y), ink, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMax - inset, y), ink, Vector2.zero);
      if (i == 0)
        continue;
      int at = i * 2;
      mesh.AddTriangle(at - 2, at - 1, at + 1);
      mesh.AddTriangle(at - 2, at + 1, at);
    }
  }
}

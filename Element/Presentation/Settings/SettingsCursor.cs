using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal enum SettingsCursorKind
{
  Pointer,
  Text,
  Horizontal,
  Crosshair,
}

// Cursor ownership follows pointer events, including drag capture. Hardware
// textures are shared and generated once; ordinary frames do not raycast or
// allocate. The same cursor setter is used by layout editing to avoid stale
// caches when crossing from an overlay handle into the settings controls.
internal sealed class SettingsCursor
  : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
  private static readonly Texture2D[] Textures = new Texture2D[4];
  private static SettingsCursor _owner;
  private static Texture2D _applied;
  private static Vector2 _hotspot;
  private static bool _appliedOnce;
  private static int _users;
  private Selectable _selectable;
  private bool _inside,
    _pressed;
  public SettingsCursorKind Kind;

  public static SettingsCursor Attach(GameObject target, SettingsCursorKind kind)
  {
    SettingsCursor cursor = target.GetComponent<SettingsCursor>() ?? target.AddComponent<SettingsCursor>();
    cursor.Kind = kind;
    return cursor;
  }

  internal static void Retain() => _users++;

  internal static void Release()
  {
    if (--_users > 0)
      return;
    _users = 0;
    ResetCursor();
    for (int i = 0; i < Textures.Length; i++)
    {
      SettingsUiFactory.Destroy(Textures[i]);
      Textures[i] = null;
    }
  }

  private void Awake() => _selectable = GetComponent<Selectable>();

  public void OnPointerEnter(PointerEventData data)
  {
    _inside = true;
    if (_selectable == null || _selectable.IsInteractable())
    {
      _owner = this;
      Restore();
    }
  }

  public void OnPointerExit(PointerEventData data)
  {
    _inside = false;
    if (!_pressed)
      ClearOwner();
  }

  public void OnPointerDown(PointerEventData data)
  {
    if (data.button != PointerEventData.InputButton.Left || (_selectable != null && !_selectable.IsInteractable()))
      return;
    _pressed = true;
    _owner = this;
    Restore();
  }

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button != PointerEventData.InputButton.Left)
      return;
    _pressed = false;
    if (!_inside)
      ClearOwner();
  }

  private void OnDisable()
  {
    _inside = _pressed = false;
    ClearOwner();
  }

  private void OnApplicationFocus(bool focused)
  {
    if (!focused)
    {
      _inside = _pressed = false;
      ClearOwner();
    }
  }

  private void ClearOwner()
  {
    if (_owner != this)
      return;
    _owner = null;
    // A nested edit button can leave the pointer inside its parent preset card.
    // Unity does not re-enter that parent, so restore its retained hover owner.
    for (Transform parent = transform.parent; parent != null; parent = parent.parent)
    {
      SettingsCursor cursor = parent.GetComponent<SettingsCursor>();
      if (
        cursor == null
        || !cursor._inside
        || !cursor.isActiveAndEnabled
        || (cursor._selectable != null && !cursor._selectable.IsInteractable())
      )
        continue;
      _owner = cursor;
      Restore();
      return;
    }
    Apply(null, Vector2.zero);
  }

  public static bool Restore()
  {
    if (_owner == null)
      return false;
    if (!_owner.isActiveAndEnabled || (_owner._selectable != null && !_owner._selectable.IsInteractable()))
    {
      _owner = null;
      Apply(null, Vector2.zero);
      return false;
    }
    int index = (int)_owner.Kind;
    if (Textures[index] == null)
      Textures[index] = Create(_owner.Kind);
    Apply(Textures[index], _owner.Kind == SettingsCursorKind.Pointer ? new Vector2(11, 4) : new Vector2(16, 16));
    return true;
  }

  public static void ResetCursor()
  {
    _owner = null;
    Apply(null, Vector2.zero);
  }

  public static void Apply(Texture2D texture, Vector2 hotspot)
  {
    if (_appliedOnce && _applied == texture && _hotspot == hotspot)
      return;
    _appliedOnce = true;
    _applied = texture;
    _hotspot = hotspot;
    Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
  }

  private static Texture2D Create(SettingsCursorKind kind)
  {
    Color32[] pixels = new Color32[32 * 32];
    for (int y = 0; y < 32; y++)
    for (int x = 0; x < 32; x++)
    {
      float fill = 0,
        border = 0;
      for (int sy = 0; sy < 4; sy++)
      for (int sx = 0; sx < 4; sx++)
      {
        Vector2 p = new(x + (sx + .5f) / 4, 31 - y + (sy + .5f) / 4);
        float distance = Distance(kind, p);
        if (distance <= 0)
          fill += 1f / 16;
        if (distance <= 1.25f)
          border += 1f / 16;
      }
      float shade = border > 0 ? Mathf.Lerp(.06f, .98f, fill / border) : 0;
      pixels[y * 32 + x] = new Color(shade, shade, shade, border);
    }
    Texture2D texture = new(32, 32, TextureFormat.RGBA32, false)
    {
      name = "Settings " + kind + " cursor",
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp,
    };
    texture.SetPixels32(pixels);
    texture.Apply(false, false);
    return texture;
  }

  private static float Distance(SettingsCursorKind kind, Vector2 p)
  {
    if (kind == SettingsCursorKind.Pointer)
    {
      // Rounded raised index finger and a compact palm, with readable fingers.
      float d = Segment(p, 11, 5, 11, 19) - 1.7f;
      d = Mathf.Min(d, RoundedBox(p, 15.5f, 21, 7.5f, 6.5f, 3));
      d = Mathf.Min(d, Segment(p, 15, 13, 15, 20) - 1.8f);
      d = Mathf.Min(d, Segment(p, 19, 14.5f, 19, 20) - 1.8f);
      d = Mathf.Min(d, Segment(p, 23, 16.5f, 23, 22) - 1.8f);
      return Mathf.Min(d, Segment(p, 5.5f, 17, 10, 24) - 1.8f);
    }
    if (kind == SettingsCursorKind.Text)
    {
      float d = Segment(p, 16, 7, 16, 25);
      d = Mathf.Min(d, Segment(p, 12, 7, 20, 7));
      return Mathf.Min(d, Segment(p, 12, 25, 20, 25)) - .7f;
    }
    if (kind == SettingsCursorKind.Horizontal)
    {
      float d = Segment(p, 6, 16, 26, 16);
      d = Mathf.Min(d, Segment(p, 6, 16, 11, 11));
      d = Mathf.Min(d, Segment(p, 6, 16, 11, 21));
      d = Mathf.Min(d, Segment(p, 26, 16, 21, 11));
      return Mathf.Min(d, Segment(p, 26, 16, 21, 21)) - .8f;
    }
    float cross = Mathf.Abs((p - new Vector2(16, 16)).magnitude - 4.5f) - .65f;
    cross = Mathf.Min(cross, Segment(p, 16, 5, 16, 9) - .65f);
    cross = Mathf.Min(cross, Segment(p, 16, 23, 16, 27) - .65f);
    cross = Mathf.Min(cross, Segment(p, 5, 16, 9, 16) - .65f);
    return Mathf.Min(cross, Segment(p, 23, 16, 27, 16) - .65f);
  }

  private static float Segment(Vector2 point, float x1, float y1, float x2, float y2)
  {
    Vector2 a = new(x1, y1),
      d = new(x2 - x1, y2 - y1);
    return (point - a - d * Mathf.Clamp01(Vector2.Dot(point - a, d) / d.sqrMagnitude)).magnitude;
  }

  private static float RoundedBox(Vector2 point, float x, float y, float halfWidth, float halfHeight, float radius)
  {
    Vector2 d = new(Mathf.Abs(point.x - x) - halfWidth + radius, Mathf.Abs(point.y - y) - halfHeight + radius);
    return new Vector2(Mathf.Max(0, d.x), Mathf.Max(0, d.y)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - radius;
  }
}

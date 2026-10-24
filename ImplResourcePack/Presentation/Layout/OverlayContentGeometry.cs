using System;
using TMPro;
using UnityEngine;

namespace ImplResourcePack.Presentation.Layout;

// Read geometry when TMP has already prepared it. Ordinary frames do not
// measure text or rebuild a Canvas; animation-only changes reuse resting bounds.
internal sealed class OverlayContentGeometry : IDisposable
{
  private readonly RectTransform _root;
  private readonly TextGeometry[] _texts;
  private readonly Action _changed;
  private readonly bool _restingPose;
  private readonly float[] _fontSizes;
  private readonly Vector2[] _positions;
  private readonly Vector3[] _scales;
  private Rect _bounds;
  public bool HasBounds { get; private set; }
  public Rect Bounds => _bounds;

  internal void Seed(Rect bounds)
  {
    if (HasBounds || bounds.width <= 0 || bounds.height <= 0)
      return;
    _bounds = bounds;
    HasBounds = true;
  }

  public OverlayContentGeometry(
    RectTransform root,
    TextMeshProUGUI[] texts,
    float[] fontSizes,
    Vector2[] positions,
    Vector3[] scales,
    bool restingPose,
    Action changed
  )
  {
    _root = root;
    _changed = changed;
    _restingPose = restingPose;
    _fontSizes = fontSizes;
    _positions = positions;
    _scales = scales;
    _texts = new TextGeometry[texts.Length];
    for (int i = 0; i < texts.Length; i++)
      _texts[i] = new TextGeometry(this, texts[i], i);
  }

  private void UpdateBounds()
  {
    bool found = false;
    Rect bounds = default;
    foreach (TextGeometry text in _texts)
    {
      if (!text.HasBounds)
        continue;
      bounds = found
        ? Rect.MinMaxRect(
          Mathf.Min(bounds.xMin, text.Bounds.xMin),
          Mathf.Min(bounds.yMin, text.Bounds.yMin),
          Mathf.Max(bounds.xMax, text.Bounds.xMax),
          Mathf.Max(bounds.yMax, text.Bounds.yMax)
        )
        : text.Bounds;
      found = true;
    }
    if (!found || (HasBounds && Close(_bounds, bounds)))
      return;
    _bounds = bounds;
    HasBounds = true;
    _changed();
  }

  private Rect Measure(TextMeshProUGUI text, int index)
  {
    Bounds mesh = text.textBounds;
    float ratio = _restingPose && text.fontSize > 0 ? _fontSizes[index] / text.fontSize : 1;
    Vector3 scale = Vector3.one;
    Vector3 translation = Vector3.zero;
    if (_restingPose && text.rectTransform != _root)
    {
      Vector3 current = text.transform.localScale;
      scale = new Vector3(
        current.x != 0 ? _scales[index].x / current.x : 1,
        current.y != 0 ? _scales[index].y / current.y : 1,
        1
      );
      translation = text.transform.parent.TransformVector(_positions[index] - text.rectTransform.anchoredPosition);
    }
    Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
    Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int i = 0; i < 4; i++)
    {
      Vector3 point = new((i & 1) == 0 ? mesh.min.x : mesh.max.x, i < 2 ? mesh.min.y : mesh.max.y, 0);
      Vector2 local = _root.InverseTransformPoint(
        text.transform.TransformPoint(Vector3.Scale(point * ratio, scale)) + translation
      );
      min = Vector2.Min(min, local);
      max = Vector2.Max(max, local);
    }
    return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
  }

  private static bool Close(Rect a, Rect b) =>
    Mathf.Abs(a.x - b.x) < .001f
    && Mathf.Abs(a.y - b.y) < .001f
    && Mathf.Abs(a.width - b.width) < .001f
    && Mathf.Abs(a.height - b.height) < .001f;

  public void Dispose()
  {
    foreach (TextGeometry text in _texts)
      text.Dispose();
  }

  private sealed class TextGeometry : IDisposable
  {
    private readonly OverlayContentGeometry _owner;
    private readonly TextMeshProUGUI _text;
    private readonly int _index;
    private string _value;
    private TMP_FontAsset _font;
    private float _size;
    private Vector2 _rectSize;
    private TextAlignmentOptions _alignment;
    public bool HasBounds { get; private set; }
    public Rect Bounds { get; private set; }

    public TextGeometry(OverlayContentGeometry owner, TextMeshProUGUI text, int index)
    {
      _owner = owner;
      _text = text;
      _index = index;
      _text.OnPreRenderText += OnPrepared;
    }

    private void OnPrepared(TMP_TextInfo info)
    {
      if (string.IsNullOrEmpty(_text.text))
        return; // A temporarily empty/hidden element retains its stable anchor.
      Vector2 rectSize = _text.rectTransform.rect.size;
      if (
        HasBounds
        && _value == _text.text
        && _font == _text.font
        && _alignment == _text.alignment
        && (_owner._restingPose || (_size == _text.fontSize && _rectSize == rectSize))
      )
        return;
      _value = _text.text;
      _font = _text.font;
      _alignment = _text.alignment;
      _size = _text.fontSize;
      _rectSize = rectSize;
      Rect bounds = _owner.Measure(_text, _index);
      if (bounds.width <= 0 || bounds.height <= 0)
        return;
      Bounds = bounds;
      HasBounds = true;
      _owner.UpdateBounds();
    }

    public void Dispose()
    {
      if (_text != null)
        _text.OnPreRenderText -= OnPrepared;
    }
  }
}

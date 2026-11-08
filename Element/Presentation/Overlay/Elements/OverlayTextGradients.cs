using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Gradient colors in a TMP text. The composer writes each gradient run in a
/// marker color; once TMP has laid the text out, every corner of those glyphs
/// takes the gradient's color at that point of the whole text's box, as Figma
/// does, so a gradient on part of a text carries on across it. Any font and
/// fallback works, as only vertex colors change. Between glyph corners colors
/// blend in a straight line, which is exact for two-color linear gradients.
/// </summary>
internal sealed class OverlayTextGradients : MonoBehaviour
{
  private const byte MarkerRed = 1,
    MarkerGreen = 254;

  private readonly List<OverlayColorSettings> _colors = new();
  private readonly List<OverlayGradientFormulas> _formulas = new();
  private readonly List<OverlayResolvedGradient> _resolved = new();
  private TMP_Text _text;
  private IOverlayNumberSource _source;
  private Color _highlight = Color.white;

  public int Count => _colors.Count;

  /// <summary>The values the gradients use, as catalog bits.</summary>
  public ulong Mask { get; private set; }

  public void Initialize(TMP_Text text)
  {
    _text = text;
    _text.OnPreRenderText += Paint;
  }

  public void Clear()
  {
    _colors.Clear();
    Mask = 0;
  }

  /// <summary>Adds a gradient color and returns its slot, which <see cref="Marker"/> turns into the run's color.</summary>
  public int Add(OverlayColorSettings color)
  {
    int slot = _colors.Count;
    while (_formulas.Count <= slot)
    {
      _formulas.Add(new OverlayGradientFormulas());
      _resolved.Add(new OverlayResolvedGradient());
    }
    _formulas[slot].Compile(color);
    Mask |= _formulas[slot].Mask;
    _colors.Add(color);
    return slot;
  }

  /// <summary>Where values and the highlight come from for the next paint.</summary>
  public void SetSource(IOverlayNumberSource source, Color highlight)
  {
    _source = source;
    _highlight = highlight;
  }

  /// <summary>The color a gradient run is written in.</summary>
  public static Color Marker(int slot) => new Color32(MarkerRed, MarkerGreen, (byte)Mathf.Clamp(slot, 0, 255), 255);

  /// <summary>A solid color one step off any marker, so it is never mistaken for one.</summary>
  public static Color AvoidMarker(Color color)
  {
    Color32 bytes = color;
    if (bytes.r != MarkerRed || bytes.g != MarkerGreen)
      return color;
    color.g = (MarkerGreen - 1) / 255f;
    return color;
  }

  private static bool IsMarker(Color32 color, out int slot)
  {
    slot = color.b;
    return color.r == MarkerRed && color.g == MarkerGreen;
  }

  /// <summary>Colors the text as laid out now again, for gradients that changed while the text didn't.</summary>
  public void Repaint()
  {
    if (_text == null || _colors.Count == 0 || _text.textInfo == null || _text.textInfo.characterCount == 0)
      return;
    Paint(_text.textInfo);
    _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
  }

  /// <summary>
  /// The text's box in its local space: from the first glyph's origin to the
  /// last advance, and from the lowest descender to the highest ascender, as
  /// TMP measures text bounds. False when nothing shows.
  /// </summary>
  public static bool Box(TMP_TextInfo info, out Rect box)
  {
    box = default;
    if (info == null)
      return false;
    float left = float.PositiveInfinity,
      bottom = float.PositiveInfinity,
      right = float.NegativeInfinity,
      top = float.NegativeInfinity;
    int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
    for (int i = 0; i < count; i++)
    {
      TMP_CharacterInfo character = info.characterInfo[i];
      if (!character.isVisible)
        continue;
      left = Mathf.Min(left, character.origin);
      right = Mathf.Max(right, character.xAdvance);
      bottom = Mathf.Min(bottom, character.descender);
      top = Mathf.Max(top, character.ascender);
    }
    if (left >= right || bottom >= top)
      return false;
    box = Rect.MinMaxRect(left, bottom, right, top);
    return true;
  }

  private void Paint(TMP_TextInfo info)
  {
    if (_colors.Count == 0 || !Box(info, out Rect box))
      return;
    for (int slot = 0; slot < _colors.Count; slot++)
      if (!_formulas[slot].Resolve(_colors[slot], _highlight, _source, box.width, box.height, _resolved[slot]))
        _resolved[slot].Stops.Clear();
    int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
    for (int i = 0; i < count; i++)
    {
      TMP_CharacterInfo character = info.characterInfo[i];
      if (!character.isVisible || !IsMarker(character.color, out int slot) || slot >= _colors.Count)
        continue;
      OverlayResolvedGradient gradient = _resolved[slot];
      if (gradient.Stops.Count == 0)
        continue;
      OverlayGradientFrame frame = gradient.Frame(box.width, box.height);
      TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
      if (mesh.vertices == null || mesh.colors32 == null)
        continue;
      for (int corner = 0; corner < 4; corner++)
      {
        int index = character.vertexIndex + corner;
        if (index >= mesh.vertices.Length || index >= mesh.colors32.Length)
          break;
        Vector3 position = mesh.vertices[index];
        mesh.colors32[index] = gradient.Sample32(frame.At(position.x - box.xMin, box.yMax - position.y));
      }
    }
  }

  private void OnDestroy()
  {
    if (_text != null)
      _text.OnPreRenderText -= Paint;
  }
}

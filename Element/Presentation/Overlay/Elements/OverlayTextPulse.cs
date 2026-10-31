using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

// The combo's bump: only the combo value's glyphs grow and settle back. It
// moves already generated vertices, so the text is never laid out again and
// its anchor bounds stay at rest.
internal sealed class OverlayTextPulse : MonoBehaviour
{
  private const double DurationMilliseconds = 500d;
  private const float Growth = 30f / 78f;

  private readonly Stopwatch _clock = new();
  private readonly List<Glyph> _glyphs = new();
  private readonly List<Vector3> _resting = new();
  private TextMeshProUGUI _text;
  private Vector3 _center;

  public void Initialize(TextMeshProUGUI text)
  {
    _text = text;
    _text.OnPreRenderText += Capture;
    enabled = false;
  }

  public void Play()
  {
    _clock.Restart();
    enabled = true;
    Apply(true);
  }

  private void LateUpdate()
  {
    if (_clock.Elapsed.TotalMilliseconds >= DurationMilliseconds)
    {
      _clock.Reset();
      enabled = false;
    }
    Apply(true);
  }

  private void OnDisable()
  {
    _clock.Reset();
    Apply(true);
  }

  private void OnDestroy()
  {
    if (_text != null)
      _text.OnPreRenderText -= Capture;
  }

  private float Scale
  {
    get
    {
      if (!_clock.IsRunning)
        return 1;
      double progress = Math.Min(1d, _clock.Elapsed.TotalMilliseconds / DurationMilliseconds);
      return 1 + Growth * (float)(progress >= 1d ? 0d : Math.Pow(2d, -10d * progress));
    }
  }

  // Runs after each layout, before TMP uploads the mesh.
  private void Capture(TMP_TextInfo info)
  {
    _glyphs.Clear();
    _resting.Clear();
    Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity),
      max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int link = 0; link < info.linkCount; link++)
    {
      TMP_LinkInfo linkInfo = info.linkInfo[link];
      if (linkInfo.GetLinkID() != OverlayTextComposer.ComboLink)
        continue;
      int end = Math.Min(info.characterCount, linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength);
      for (int c = linkInfo.linkTextfirstCharacterIndex; c < end; c++)
      {
        TMP_CharacterInfo character = info.characterInfo[c];
        if (!character.isVisible)
          continue;
        Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
        _glyphs.Add(new Glyph(character.materialReferenceIndex, character.vertexIndex));
        for (int v = 0; v < 4; v++)
        {
          Vector3 point = vertices[character.vertexIndex + v];
          _resting.Add(point);
          min = Vector3.Min(min, point);
          max = Vector3.Max(max, point);
        }
      }
    }
    _center = (min + max) * .5f;
    Apply(false);
  }

  private void Apply(bool upload)
  {
    if (_text == null || _glyphs.Count == 0)
      return;
    TMP_TextInfo info = _text.textInfo;
    float scale = Scale;
    for (int g = 0; g < _glyphs.Count; g++)
    {
      Glyph glyph = _glyphs[g];
      if (glyph.Material >= info.meshInfo.Length)
        return;
      Vector3[] vertices = info.meshInfo[glyph.Material].vertices;
      if (vertices == null || glyph.Vertex + 3 >= vertices.Length)
        return;
      for (int v = 0; v < 4; v++)
        vertices[glyph.Vertex + v] = _center + (_resting[g * 4 + v] - _center) * scale;
    }
    if (upload)
      _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
  }

  private readonly struct Glyph
  {
    public readonly int Material;
    public readonly int Vertex;

    public Glyph(int material, int vertex)
    {
      Material = material;
      Vertex = vertex;
    }
  }
}

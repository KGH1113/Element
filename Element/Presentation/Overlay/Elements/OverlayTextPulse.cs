using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

// Values that grow and settle back when what they show changes. Each value is
// a link in the text, named by its order there, and grows on its own clock
// around its own center. Only its glyphs move: their vertices are set from
// where TMP laid them out, so the text is never laid out again and its anchor
// bounds stay at rest.
internal sealed class OverlayTextPulse : MonoBehaviour
{
  private const double DurationMilliseconds = 500d;
  private const float Growth = 30f / 78f;

  private readonly Stopwatch _clock = new();

  // When each link started growing, in clock milliseconds; NaN at rest.
  private readonly List<double> _started = new();
  private TMP_Text _text;

  public void Initialize(TMP_Text text)
  {
    _text = text;
    _text.OnPreRenderText += Relaid;
    enabled = false;
  }

  /// <summary>Some value is still growing or settling back.</summary>
  public bool Playing => enabled;

  /// <summary>Grows the text's <paramref name="link"/>th link from the start.</summary>
  public void Play(int link)
  {
    if (link < 0 || _text == null)
      return;
    if (!_clock.IsRunning)
      _clock.Start();
    while (_started.Count <= link)
      _started.Add(double.NaN);
    _started[link] = _clock.Elapsed.TotalMilliseconds;
    enabled = true;
    // A text waiting to be laid out again is grown once it is (Relaid).
    if (!_text.havePropertiesChanged)
      Apply(true);
  }

  /// <summary>Puts every value back at rest now.</summary>
  public void Stop()
  {
    Rest();
    enabled = false;
  }

  private void LateUpdate()
  {
    if (!Apply(true))
      enabled = false;
  }

  private void OnDisable() => Rest();

  private void Rest()
  {
    // One last pass at full progress puts each value back where it was laid out.
    for (int i = 0; i < _started.Count; i++)
      if (!double.IsNaN(_started[i]))
        _started[i] = double.NegativeInfinity;
    Apply(true);
    _clock.Reset();
  }

  private void OnDestroy()
  {
    if (_text != null)
      _text.OnPreRenderText -= Relaid;
  }

  // Runs after each layout, before TMP uploads the mesh.
  private void Relaid(TMP_TextInfo info) => Apply(false);

  /// <summary>Sets each growing link's glyphs; true while one is still moving.</summary>
  private bool Apply(bool upload)
  {
    TMP_TextInfo info = _text != null ? _text.textInfo : null;
    if (info == null)
      return false;
    double now = _clock.IsRunning ? _clock.Elapsed.TotalMilliseconds : 0;
    bool moving = false,
      moved = false;
    for (int link = 0; link < _started.Count; link++)
    {
      double started = _started[link];
      if (double.IsNaN(started))
        continue;
      double progress = (now - started) / DurationMilliseconds;
      float scale = 1;
      if (progress < 1)
      {
        scale = 1 + Growth * (float)Math.Pow(2d, -10d * Math.Max(0, progress));
        moving = true;
      }
      else
        _started[link] = double.NaN;
      if (link < info.linkCount)
        moved |= Scale(info, info.linkInfo[link], scale);
    }
    if (moved && upload)
      _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    return moving;
  }

  // Scales one link's glyphs around the middle of where they were laid out.
  private static bool Scale(TMP_TextInfo info, TMP_LinkInfo link, float scale)
  {
    int first = Math.Max(0, link.linkTextfirstCharacterIndex),
      end = Math.Min(info.characterCount, link.linkTextfirstCharacterIndex + link.linkTextLength);
    Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity),
      max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int c = first; c < end; c++)
    {
      ref readonly TMP_CharacterInfo character = ref info.characterInfo[c];
      if (!character.isVisible)
        continue;
      min = Vector3.Min(min, Vector3.Min(character.vertex_BL.position, character.vertex_TR.position));
      max = Vector3.Max(max, Vector3.Max(character.vertex_BL.position, character.vertex_TR.position));
    }
    if (min.x > max.x)
      return false;
    Vector3 center = (min + max) * .5f;
    bool moved = false;
    for (int c = first; c < end; c++)
    {
      ref readonly TMP_CharacterInfo character = ref info.characterInfo[c];
      if (!character.isVisible || character.materialReferenceIndex >= info.meshInfo.Length)
        continue;
      Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
      int index = character.vertexIndex;
      if (vertices == null || index < 0 || index + 3 >= vertices.Length)
        continue;
      // TMP's corner order: bottom left, top left, top right, bottom right.
      vertices[index] = center + (character.vertex_BL.position - center) * scale;
      vertices[index + 1] = center + (character.vertex_TL.position - center) * scale;
      vertices[index + 2] = center + (character.vertex_TR.position - center) * scale;
      vertices[index + 3] = center + (character.vertex_BR.position - center) * scale;
      moved = true;
    }
    return moved;
  }
}

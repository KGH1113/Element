using System;
using System.Collections.Generic;
using Element.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Overlay.Combo;

internal sealed class SongTitleMirror : MonoBehaviour
{
  private Action<string> _target;
  private IRenderVideo _video;
  private Text _source;
  private string _lastText;
  private int _lastSourceFontSize;
  private bool _sourceWasEnabled;
  private string _playCaption;
  private float _playSpeed;
  private bool _playPractice;
  private string _playTitle;

  public void Initialize(Action<string> target, IRenderVideo video)
  {
    _target = target;
    _video = video;
  }

  public void Release()
  {
    RestoreSource();
  }

  private void OnEnable()
  {
    Sync();
  }

  private void LateUpdate()
  {
    Sync();
  }

  private void OnDisable()
  {
    RestoreSource();
  }

  private void OnDestroy()
  {
    RestoreSource();
  }

  private void Sync()
  {
    Text currentSource = scrUIController.instance?.txtLevelName;
    if (currentSource != _source)
    {
      RestoreSource();
      _source = currentSource;
      if (_source != null)
      {
        _sourceWasEnabled = _source.enabled;
        _source.enabled = false;
      }

      _lastText = null;
      _lastSourceFontSize = 0;
    }
    else if (_source != null && _source.enabled)
    {
      _source.enabled = false;
    }

    if (_target == null)
      return;

    string text =
      _video?.IsRendering == true ? PlayModeTitle()
      : _source != null ? _source.text
      : string.Empty;
    int sourceFontSize = _source != null ? _source.fontSize : 0;
    if (_lastText == text && _lastSourceFontSize == sourceFontSize)
      return;

    _target(SongTitleMarkupNormalizer.Normalize(text, sourceFontSize));
    _lastText = text;
    _lastSourceFontSize = sourceFontSize;
  }

  // OrbitRender plays the level in the editor, which never writes ADOFAI's
  // level name. This is the name play mode shows (scrController.WaitForStartCo).
  private string PlayModeTitle()
  {
    string caption = scrController.instance?.caption ?? string.Empty;
    float speed = GCS.speedTrialMode ? GCS.currentSpeedTrial : 0f;
    bool practice = !GCS.speedTrialMode && GCS.practiceMode;
    if (
      _playTitle != null
      && ReferenceEquals(caption, _playCaption)
      && speed == _playSpeed
      && practice == _playPractice
    )
      return _playTitle;

    _playCaption = caption;
    _playSpeed = speed;
    _playPractice = practice;
    _playTitle =
      speed > 0f
        ? caption
          + " ("
          + RDString.Get(
            "levelSelect.multiplier",
            new Dictionary<string, object> { { "multiplier", speed.ToString("0.0#") } }
          )
          + ")"
      : practice ? caption + "\n(" + RDString.Get("status.practiceMode") + ")"
      : caption;
    return _playTitle;
  }

  private void RestoreSource()
  {
    if (_source != null)
      _source.enabled = _sourceWasEnabled;

    _source = null;
    _lastText = null;
    _lastSourceFontSize = 0;
  }
}

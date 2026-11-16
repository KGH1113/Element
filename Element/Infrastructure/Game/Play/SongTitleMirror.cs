using System;
using System.Collections.Generic;
using Element.Application.Overlay;
using Element.Application.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Infrastructure.Game.Play;

/// <summary>
/// ADOFAI's level name, hidden while active and its text handed on as it changes.
/// During an offline render, the name play mode shows instead.
/// </summary>
internal sealed class SongTitleMirror : MonoBehaviour, ISongTitleMirror
{
  private Action<string, int> _target;
  private IRenderVideo _video;
  private Text _source;
  private string _lastText;
  private int _lastSourceFontSize;
  private bool _sourceWasEnabled;
  private string _playCaption;
  private float _playSpeed;
  private bool _playPractice;
  private string _playTitle;

  /// <summary>Starts inactive under <paramref name="parent"/>.</summary>
  public static SongTitleMirror Create(Transform parent, IRenderVideo video)
  {
    GameObject root = new("Song title source");
    root.SetActive(false);
    root.transform.SetParent(parent, false);
    SongTitleMirror mirror = root.AddComponent<SongTitleMirror>();
    mirror._video = video;
    return mirror;
  }

  public void Follow(Action<string, int> changed) => _target = changed;

  public void SetActive(bool active) => gameObject.SetActive(active);

  public void Release()
  {
    RestoreSource();
    if (this != null)
      Destroy(gameObject);
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

    _target(text, sourceFontSize);
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

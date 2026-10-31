using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Overlay.Combo;

internal sealed class SongTitleMirror : MonoBehaviour
{
  private TextMeshProUGUI _target;
  private Text _source;
  private string _lastText;
  private int _lastSourceFontSize;
  private bool _sourceWasEnabled;

  public void Initialize(TextMeshProUGUI target)
  {
    _target = target;
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

    string text = _source != null ? _source.text : string.Empty;
    int sourceFontSize = _source != null ? _source.fontSize : 0;
    if (_lastText == text && _lastSourceFontSize == sourceFontSize)
      return;

    _target.text = SongTitleMarkupNormalizer.Normalize(text, sourceFontSize);
    _lastText = text;
    _lastSourceFontSize = sourceFontSize;
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

using System;
using System.Globalization;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.TimingScale;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.TimingScale;

internal sealed class TimingScaleView : IOverlayView<TimingScaleSnapshot>
{
  private GameObject _root;
  private TextMeshProUGUI _text;
  private double _lastPercent = double.NaN;

  public TimingScaleView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    _root = OverlayTextFactory.CreateRect(
      "TimingScale",
      host.Root.transform,
      new Vector2(0.5f, 0f),
      new Vector2(0.5f, 0f),
      // JRP's normal overlay uses 90 + 40 * Size (default Size = 1).
      new Vector2(0f, 130f),
      new Vector2(300f, 30f)
    );
    _text = OverlayTextFactory.AddText(_root, font, material, 20f, TextAlignmentOptions.Bottom);
    _text.color = Color.white;
    _root.SetActive(false);
  }

  public void Show()
  {
    _root?.SetActive(true);
  }

  public void Hide()
  {
    _root?.SetActive(false);
  }

  public void Render(TimingScaleSnapshot snapshot)
  {
    if (_text == null)
      return;

    if (!snapshot.IsValid)
    {
      if (!double.IsNaN(_lastPercent))
        _text.text = string.Empty;
      _lastPercent = double.NaN;
      return;
    }

    double percent = Math.Round(snapshot.Scale * 100d, 2);
    if (_lastPercent == percent)
      return;

    _text.text = "Timing Scale - " + percent.ToString(CultureInfo.InvariantCulture) + "%";
    _lastPercent = percent;
  }

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);

    _root = null;
    _text = null;
  }
}

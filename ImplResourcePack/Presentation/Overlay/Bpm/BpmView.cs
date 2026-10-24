using System;
using System.Globalization;
using System.Text;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.Bpm;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.Bpm;

internal sealed class BpmView : IOverlayView<BpmSnapshot>
{
  private readonly StringBuilder _builder = new(192);
  private GameObject _root;
  private TextMeshProUGUI _text;
  private TextMeshProUGUI _currentText;
  private TextMeshProUGUI _keysText;
  private double _lastTileBpm = double.NaN;
  private double _lastCurrentBpm = double.NaN;
  private int _lastThemeVersion = -1;

  public BpmView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    _root = OverlayTextFactory.CreateRect(
      "BPM",
      host.Root.transform,
      new Vector2(1f, 1f),
      new Vector2(1f, 1f),
      new Vector2(-16f, -16f),
      new Vector2(456f, 90f)
    );
    _text = CreateRow("TileBPM", 0f, font, material);
    _currentText = CreateRow("CurrentBPM", -35f, font, material);
    _keysText = CreateRow("KeysPerSecond", -70f, font, material);
    host.RegisterLayout("tile-bpm", "타일 BPM", _text.gameObject, _text, "TBPM | 180");
    host.RegisterLayout("current-bpm", "현재 BPM", _currentText.gameObject, _currentText, "CBPM | 180");
    host.RegisterLayout("keys-per-second", "초당 입력 수", _keysText.gameObject, _keysText, "KPS | 6");
    _root.SetActive(false);
  }

  public void Show()
  {
    if (_root != null)
      _root.SetActive(true);
  }

  public void Hide()
  {
    if (_root != null)
      _root.SetActive(false);
  }

  public void Render(BpmSnapshot snapshot)
  {
    if (
      _text == null
      || !snapshot.IsValid
      || (
        _lastTileBpm == snapshot.TileBpm
        && _lastCurrentBpm == snapshot.CurrentBpm
        && _lastThemeVersion == OverlayTheme.Version
      )
    )
      return;

    string tileColor = OverlayTheme.ColorToHex(OverlayTheme.BpmColor(snapshot.TileBpm));
    string currentColor = OverlayTheme.ColorToHex(OverlayTheme.BpmColor(snapshot.CurrentBpm));

    _builder.Clear();
    AppendLine("TBPM", snapshot.TileBpm, tileColor);
    _text.text = _builder.ToString();
    _builder.Clear();
    AppendLine("CBPM", snapshot.CurrentBpm, currentColor);
    _currentText.text = _builder.ToString();
    _builder.Clear();
    AppendLine("KPS", snapshot.KeysPerSecond, currentColor);
    _keysText.text = _builder.ToString();

    _lastTileBpm = snapshot.TileBpm;
    _lastCurrentBpm = snapshot.CurrentBpm;
    _lastThemeVersion = OverlayTheme.Version;
  }

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);

    _root = null;
    _text = null;
    _currentText = null;
    _keysText = null;
  }

  private TextMeshProUGUI CreateRow(string name, float y, TMP_FontAsset font, Material material)
  {
    GameObject row = OverlayTextFactory.CreateRect(
      name,
      _root.transform,
      Vector2.one,
      Vector2.one,
      new Vector2(0f, y),
      new Vector2(456f, 30f)
    );
    TextMeshProUGUI text = OverlayTextFactory.AddText(row, font, material, 25f, TextAlignmentOptions.TopRight);
    text.color = Color.white;
    return text;
  }

  private void AppendLine(string label, double value, string color)
  {
    _builder
      .Append("<color=#FFFFFF>")
      .Append(label)
      .Append(" |</color> <color=#")
      .Append(color)
      .Append('>')
      .Append(Math.Round(value, 2).ToString(CultureInfo.InvariantCulture))
      .Append("</color>");
  }
}

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

  // Displayed values (two decimals) decide when the text needs rebuilding.
  private long _lastTileBpm = long.MinValue;
  private long _lastCurrentBpm = long.MinValue;
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
    if (_text == null || !snapshot.IsValid)
      return;
    long tileBpm = NumberText.Scaled(snapshot.TileBpm, 2);
    long currentBpm = NumberText.Scaled(snapshot.CurrentBpm, 2);
    if (_lastTileBpm == tileBpm && _lastCurrentBpm == currentBpm && _lastThemeVersion == OverlayTheme.Version)
      return;

    Color tileColor = OverlayTheme.BpmColor(snapshot.TileBpm);
    Color currentColor = OverlayTheme.BpmColor(snapshot.CurrentBpm);
    _text.Assign(AppendLine("TBPM", snapshot.TileBpm, tileColor));
    _currentText.Assign(AppendLine("CBPM", snapshot.CurrentBpm, currentColor));
    _keysText.Assign(AppendLine("KPS", snapshot.KeysPerSecond, currentColor));

    _lastTileBpm = tileBpm;
    _lastCurrentBpm = currentBpm;
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

  private StringBuilder AppendLine(string label, double value, Color color)
  {
    return _builder
      .Clear()
      .Append("<color=#FFFFFF>")
      .Append(label)
      .Append(" |</color> <color=#")
      .AppendHexRgb(color)
      .Append('>')
      .AppendRounded(value, 2)
      .Append("</color>");
  }
}

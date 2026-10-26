using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Presentation.Layout;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

// A visual-only key sample for the gallery and disconnected editor. It never
// captures keys, opens IPC sessions, or appears during ordinary gameplay.
internal sealed class NativeViewerMock : MonoBehaviour
{
  public const float ViewportHeight = 270;
  private SettingsUiFactory _ui;
  private int _themeVersion = -1;
  private TextMeshProUGUI _statusText;
  private GameObject _statusPanel;
  private NativeViewerStatus? _status;

  public static float Width(string kind) => kind == "hand" ? 430 : 210;

  public static RectTransform Create(string kind, Transform parent, TMP_FontAsset font, bool expanded)
  {
    SettingsUiFactory ui = new(font);
    float width = Width(kind);
    float height = ViewportHeight * .45f;
    RectTransform root = ui.Rect(kind + " key viewer sample", parent, 0, 0, width, expanded ? ViewportHeight : height);
    root.gameObject.AddComponent<NativeViewerMock>()._ui = ui;
    float top = expanded ? ViewportHeight - height : 0;
    bool hand = kind == "hand";
    int columns = hand ? 8 : 2;
    int rows = hand ? 2 : 1;
    float keyWidth = (width - (columns - 1) * 4) / columns;
    float keyHeight = (height - 24 - (rows - 1) * 4) / rows;
    string[] labels = hand
      ? new[] { "Tab", "1", "2", "E", "P", "=", "Back", "\\", "Caps", "Ctrl", "C", "Space", "Alt", "↑", ".", "6" }
      : new[] { "Left", "Right" };
    for (int i = 0; i < labels.Length; i++)
    {
      Image key = ui.Panel(
        "Key " + labels[i],
        root,
        (i % columns) * (keyWidth + 4),
        top + (i / columns) * (keyHeight + 4),
        keyWidth,
        keyHeight,
        OverlayTheme.Accent
      );
      bool pressed = i == (hand ? 3 : 0);
      ui.Bind(() => key.color = Color.Lerp(new Color(.06f, .06f, .06f), OverlayTheme.Accent, pressed ? .9f : .55f));
      TextMeshProUGUI text = ui.Text(labels[i], key.transform, 0, 0, keyWidth, keyHeight, hand ? 15 : 23);
      text.alignment = TextAlignmentOptions.Midline;
    }
    Image stats = ui.Panel("KPS sample", root, 0, top + height - 20, width, 20, OverlayTheme.Accent);
    ui.Bind(() => stats.color = Color.Lerp(new Color(.06f, .06f, .06f), OverlayTheme.Accent, .4f));
    ui.Text("KPS", stats.transform, 8, 0, 60, 20, 14);
    TextMeshProUGUI value = ui.Text(hand ? "12" : "4", stats.transform, width - 48, 0, 40, 20, 14);
    value.alignment = TextAlignmentOptions.MidlineRight;
    if (expanded)
    {
      // The empty note area above the keys explains why the live viewer is absent.
      NativeViewerMock mock = root.GetComponent<NativeViewerMock>();
      Image panel = ui.Panel("Status", root, 8, 8, width - 16, top - 16, new Color(0, 0, 0, .62f));
      mock._statusPanel = panel.gameObject;
      mock._statusText = ui.Text(string.Empty, panel.transform, 10, 8, width - 36, top - 32, 15);
      mock._statusText.alignment = TextAlignmentOptions.Center;
      mock._statusText.textWrappingMode = TextWrappingModes.Normal;
      mock._statusText.enableAutoSizing = true;
      mock._statusText.fontSizeMin = 10;
      mock._statusText.fontSizeMax = 15;
      mock._statusText.color = new Color(1, 1, 1, .92f);
      mock._statusPanel.SetActive(false);
    }
    return root;
  }

  public void SetStatus(NativeViewerStatus status)
  {
    if (_statusText == null || _status == status)
      return;
    _status = status;
    string message = NativeViewerStatusText.Get(status);
    _statusText.text = message;
    _statusPanel.SetActive(message.Length > 0);
  }

  private void Update()
  {
    if (_themeVersion == OverlayTheme.Version)
      return;
    _themeVersion = OverlayTheme.Version;
    _ui?.RefreshTheme();
  }

  private void OnDestroy() => _ui?.Dispose();
}

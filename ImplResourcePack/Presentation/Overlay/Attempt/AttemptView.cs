using System.Text;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.Attempts;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.Attempt;

internal sealed class AttemptView : IOverlayView<AttemptCounts>
{
  private const float FontSize = 25f;

  private readonly StringBuilder _builder = new(32);
  private GameObject _root;
  private TextMeshProUGUI _attemptText;
  private TextMeshProUGUI _fullAttemptText;
  private int _lastAttempt = -1;
  private int _lastFullAttempt = -1;

  public AttemptView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    // JRP's attempt text: bottom center + (310, 35), 300 × 30, bottom-left aligned.
    _root = OverlayTextFactory.CreateRect(
      "Attempt",
      host.Root.transform,
      new Vector2(0.5f, 0f),
      new Vector2(0.5f, 0f),
      new Vector2(310f, 35f),
      new Vector2(300f, 30f)
    );
    // JRP writes "Attempt" and "Full Attempt" as two lines of one text, so the
    // first sits one line above the second.
    _attemptText = CreateRow("Attempt", LineAdvance(font), font, material);
    _fullAttemptText = CreateRow("FullAttempt", 0f, font, material);
    host.RegisterLayout("attempt", "시도 횟수", _attemptText.gameObject, _attemptText, "Attempt 3");
    host.RegisterLayout(
      "full-attempt",
      "전체 시도 횟수",
      _fullAttemptText.gameObject,
      _fullAttemptText,
      "Full Attempt 27"
    );
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

  public void Render(AttemptCounts counts)
  {
    if (_attemptText == null)
      return;

    if (_lastAttempt != counts.Attempt)
    {
      _attemptText.Assign(_builder.Clear().Append("Attempt ").AppendInt(counts.Attempt));
      _lastAttempt = counts.Attempt;
    }
    if (_lastFullAttempt != counts.FullAttempt)
    {
      _fullAttemptText.Assign(_builder.Clear().Append("Full Attempt ").AppendInt(counts.FullAttempt));
      _lastFullAttempt = counts.FullAttempt;
    }
  }

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);

    _root = null;
    _attemptText = null;
    _fullAttemptText = null;
  }

  private TextMeshProUGUI CreateRow(string name, float y, TMP_FontAsset font, Material material)
  {
    GameObject row = OverlayTextFactory.CreateRect(
      name,
      _root.transform,
      new Vector2(0.5f, 0f),
      new Vector2(0.5f, 0f),
      new Vector2(0f, y),
      new Vector2(300f, 30f)
    );
    TextMeshProUGUI text = OverlayTextFactory.AddText(row, font, material, FontSize, TextAlignmentOptions.BottomLeft);
    text.color = Color.white;
    return text;
  }

  // TMP moves each new line down by the font's line height at this size.
  private static float LineAdvance(TMP_FontAsset font)
  {
    FaceInfo face = font.faceInfo;
    return face.pointSize > 0f ? face.lineHeight / face.pointSize * face.scale * FontSize : FontSize;
  }
}

using Element.Presentation.Ui.Theme;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell.Dock;

/// <summary>
/// The input dock's line: what is edited, its formula with values as soft
/// chips and a blinking caret at its end, and the result on the right. A long
/// formula keeps its end, where the caret is, in view. The whole line is
/// where a carried value joins the formula (<see cref="Area"/>).
/// </summary>
internal sealed class FormulaLineView : NativeView
{
  public const float Height = 54;

  private readonly InputDockViewModel _vm;
  private TextMeshProUGUI _title,
    _formula,
    _result;
  private RectTransform _viewport;
  private Image _caret;
  private float _width;
  private bool _empty;

  public FormulaLineView(InputDockViewModel vm) => _vm = vm;

  /// <summary>The line's rect, once it is built: where a dropped value joins the formula.</summary>
  public RectTransform Area => Rect;

  protected override void Build(RectTransform rect)
  {
    rect.gameObject.name = "Formula line";
    Image hit = rect.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    _title = Label(rect, "Name", 17, TextAlignmentOptions.MidlineLeft);
    _title.overflowMode = TextOverflowModes.Ellipsis;
    _viewport = RectPeer.Child(rect, "Formula");
    _viewport.gameObject.AddComponent<RectMask2D>();
    _formula = Label(_viewport, "Formula text", 26, TextAlignmentOptions.MidlineLeft);
    _formula.richText = true;
    _formula.overflowMode = TextOverflowModes.Overflow;
    _caret = RectPeer.Child(_viewport, "Caret").gameObject.AddComponent<Image>();
    _caret.color = Color.white;
    _caret.raycastTarget = false;
    _caret.enabled = false;
    _result = Label(rect, "Result", 22, TextAlignmentOptions.MidlineRight);
    _result.color = UiPalette.SoftAccent;
    Watch(() => _vm.Title.Value, text => Show(_title, text));
    Watch(() => _vm.TitleInk.Value, color => _title.color = color);
    Watch(() => _vm.Formula.Value, text => Show(_formula, text));
    Watch(() => _vm.FormulaInk.Value, color => _formula.color = color);
    Watch(
      () => _vm.FormulaEmpty.Value,
      empty =>
      {
        _empty = empty;
        LayOut();
      }
    );
    Watch(() => _vm.Result.Value, text => Show(_result, text));
    Watch(() => _vm.Caret.Value, on => _caret.enabled = on);
  }

  private TextMeshProUGUI Label(RectTransform parent, string name, float size, TextAlignmentOptions alignment)
  {
    TextMeshProUGUI label = RectPeer.Child(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
    if (UguiTheme.Font != null)
      label.font = UguiTheme.Font;
    label.fontSize = size;
    label.alignment = alignment;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.raycastTarget = false;
    label.color = UiPalette.Foreground;
    return label;
  }

  private void Show(TextMeshProUGUI label, string text)
  {
    label.text = text;
    LayOut();
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 400, Height);

  protected override void Resized(float width, float height)
  {
    _width = width;
    LayOut();
  }

  // The name takes what it needs, the result its width on the right, and the
  // formula the room between them, its end in view.
  private void LayOut()
  {
    if (_title == null || _width <= 0)
      return;
    float x = 0;
    float titleWidth = Mathf.Min(_width, Mathf.Ceil(_title.GetPreferredValues(_title.text).x) + 4);
    Place(_title.rectTransform, x, titleWidth);
    x += titleWidth + 12;
    bool result = _result.text.Length > 0;
    _result.enabled = result;
    float resultWidth = result ? Mathf.Ceil(_result.GetPreferredValues(_result.text).x) + 4 : 0;
    Place(_result.rectTransform, _width - resultWidth, resultWidth);
    float available = Mathf.Max(40, _width - resultWidth - 24 - x);
    Place(_viewport, x, available);
    bool empty = _empty;
    float textWidth = Mathf.Ceil(_formula.GetPreferredValues(_formula.text).x) + 2;
    float left = !empty && _formula.text.Length > 0 ? Mathf.Min(0, available - textWidth - 6) : 0;
    Place(_formula.rectTransform, left, Mathf.Max(textWidth, 10));
    RectTransform caret = _caret.rectTransform;
    caret.anchorMin = caret.anchorMax = caret.pivot = new Vector2(0, 1);
    caret.anchoredPosition = new Vector2(empty ? 0 : left + textWidth + 2, -15);
    caret.sizeDelta = new Vector2(2, Height - 30);
  }

  private static void Place(RectTransform rect, float x, float width)
  {
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = new Vector2(x, 0);
    rect.sizeDelta = new Vector2(width, Height);
  }
}

using System;
using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;
using Element.Application.Layout;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// With several elements selected, the element toolbar lines them up with
/// each other or spaces them evenly. Each press is one undo step.
/// </summary>
internal sealed class LayoutArrangeBar
{
  private const float Button = 36,
    Gap = 4;
  private readonly LayoutElementBar _bar;
  private readonly LayoutSession _session;
  private readonly LayoutSelection _selection;
  private readonly LayoutStatusLine _status;
  private readonly ILayoutEditing _editing;
  private readonly List<LayoutIcon> _icons = new();
  private readonly List<IOverlayLayoutTarget> _targets = new();
  private readonly List<LayoutBounds> _bounds = new();
  private readonly List<LayoutBounds> _before = new();
  private readonly List<Vector2> _offsets = new();
  private Button _distributeHorizontal,
    _distributeVertical;
  private bool _built;

  public LayoutArrangeBar(
    LayoutElementBar bar,
    LayoutSession session,
    LayoutSelection selection,
    LayoutStatusLine status,
    ILayoutEditing editing
  )
  {
    _bar = bar;
    _session = session;
    _selection = selection;
    _status = status;
    _editing = editing;
  }

  /// <summary>Whether the bar shows the arrange buttons.</summary>
  public bool Built => _built;

  public void Build()
  {
    float x = 32;
    float width = x + 6 * (Button + Gap) + 9 + 2 * (Button + Gap) + 8;
    RectTransform panel = _bar.Begin(null, width);
    _built = true;
    _icons.Clear();
    (LayoutAction Icon, string Hint, LayoutAlignment Alignment)[] aligns =
    {
      (LayoutAction.AlignLeft, L("왼쪽 맞춤", "Align left"), LayoutAlignment.Left),
      (LayoutAction.AlignCenter, L("가로 가운데 맞춤", "Align centers horizontally"), LayoutAlignment.Center),
      (LayoutAction.AlignRight, L("오른쪽 맞춤", "Align right"), LayoutAlignment.Right),
      (LayoutAction.AlignTop, L("위쪽 맞춤", "Align top"), LayoutAlignment.Top),
      (LayoutAction.AlignMiddle, L("세로 가운데 맞춤", "Align centers vertically"), LayoutAlignment.Middle),
      (LayoutAction.AlignBottom, L("아래쪽 맞춤", "Align bottom"), LayoutAlignment.Bottom),
    };
    foreach ((LayoutAction icon, string hint, LayoutAlignment alignment) in aligns)
    {
      ButtonAt(
        panel,
        icon,
        hint,
        x,
        () =>
          Arrange(boxes => LayoutArrange.Align(boxes, alignment), L("이미 맞춰져 있어요", "They're already lined up"))
      );
      x += Button + Gap;
    }
    _bar.Divider(x + 2);
    x += 9;
    _distributeHorizontal = ButtonAt(
      panel,
      LayoutAction.DistributeHorizontal,
      L("가로 간격 똑같이 · 3개 이상", "Space evenly across · 3 or more"),
      x,
      () =>
        Arrange(
          boxes => LayoutArrange.Distribute(boxes, true),
          L(
            "가로 간격이 이미 같아요. 세로로 맞추려면 옆 버튼을 누르세요",
            "Already evenly spaced across. To space them down, use the next button"
          )
        )
    );
    x += Button + Gap;
    _distributeVertical = ButtonAt(
      panel,
      LayoutAction.DistributeVertical,
      L("세로 간격 똑같이 · 3개 이상", "Space evenly down · 3 or more"),
      x,
      () =>
        Arrange(
          boxes => LayoutArrange.Distribute(boxes, false),
          L(
            "세로 간격이 이미 같아요. 가로로 맞추려면 옆 버튼을 누르세요",
            "Already evenly spaced down. To space them across, use the previous button"
          )
        )
    );
    Refresh();
  }

  /// <summary>Forgets the buttons; the bar they were on went.</summary>
  public void Reset()
  {
    _built = false;
    _distributeHorizontal = _distributeVertical = null;
    _icons.Clear();
  }

  private Button ButtonAt(RectTransform panel, LayoutAction kind, string hint, float x, Action click)
  {
    SettingsUiFactory ui = _bar.Ui;
    Button button = ui.Button("", panel, x, 10, Button, Button, click);
    button.gameObject.name = hint;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon icon = ui.Rect("Icon", button.transform, 8, 8, 20, 20).gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
    icon.rectTransform.anchoredPosition = Vector2.zero;
    icon.raycastTarget = false;
    _icons.Add(new LayoutIcon(icon, button));
    button.gameObject.AddComponent<LayoutActionHint>().Changed = _status.HintFor(hint);
    return _bar.Control(button);
  }

  // Spacing needs three; the buttons stay put and only grey out.
  public void Refresh()
  {
    if (!_built || _distributeHorizontal == null)
      return;
    bool distribute = _selection.Count >= 3;
    _distributeHorizontal.interactable = _distributeVertical.interactable = distribute;
    foreach (LayoutIcon icon in _icons)
      icon.Icon.color = icon.Button.interactable ? SettingsUiFactory.Foreground : LayoutColors.Disabled;
  }

  // Lines the selection up in one space: native viewers measure from their
  // own window origin, read from where the pointer is in both spaces.
  // When nothing would move, says so instead of looking like nothing happened.
  private void Arrange(Action<List<LayoutBounds>> arrange, string unchanged)
  {
    List<IOverlayLayoutTarget> selection = _selection.Targets;
    if (selection.Count < 2)
      return;
    bool native = false;
    foreach (IOverlayLayoutTarget target in selection)
      native |= target is IOverlayLayoutPointer;
    Vector2 mouse = native ? LayoutSession.Mouse : Vector2.zero;
    _targets.Clear();
    _bounds.Clear();
    _offsets.Clear();
    foreach (IOverlayLayoutTarget target in selection)
    {
      Vector2 offset = LayoutSession.PointerFor(target, mouse) - mouse;
      Rect bounds = target.Bounds;
      _targets.Add(target);
      _offsets.Add(offset);
      _bounds.Add(new LayoutBounds(bounds.x - offset.x, bounds.y - offset.y, bounds.width, bounds.height));
    }
    _before.Clear();
    _before.AddRange(_bounds);
    arrange(_bounds);
    bool moves = false;
    for (int i = 0; i < _bounds.Count; i++)
      moves |= Mathf.Abs(_bounds[i].X - _before[i].X) >= .5f || Mathf.Abs(_bounds[i].Y - _before[i].Y) >= .5f;
    if (!moves)
    {
      _status.ShowNote(unchanged);
      return;
    }
    for (int i = 0; i < _targets.Count; i++)
    {
      LayoutBounds box = _bounds[i];
      Vector2 offset = _offsets[i];
      _targets[i].SetBounds(new Rect(box.X + offset.x, box.Y + offset.y, box.Width, box.Height));
      _targets[i].CommitLayout(true);
    }
    _session.Commit();
    _editing.RefreshLayouts();
    _editing.UpdateFrames();
  }
}

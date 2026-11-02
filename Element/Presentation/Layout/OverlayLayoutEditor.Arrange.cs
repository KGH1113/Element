using System;
using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// With several elements selected, the element toolbar lines them up with
// each other or spaces them evenly. Each press is one undo step.
internal sealed partial class OverlayLayoutEditor
{
  private const float ArrangeButton = 36,
    ArrangeGap = 4;
  private readonly List<LayoutActionIcon> _arrangeIcons = new();
  private readonly List<IOverlayLayoutTarget> _arrangeTargets = new();
  private readonly List<LayoutBounds> _arrangeBounds = new();
  private readonly List<LayoutBounds> _arrangeBefore = new();
  private readonly List<Vector2> _arrangeOffsets = new();
  private Button _distributeHorizontal,
    _distributeVertical;
  private bool _settingsArrange;

  private void BuildArrangeSettings()
  {
    float x = 32;
    float width = x + 6 * (ArrangeButton + ArrangeGap) + 9 + 2 * (ArrangeButton + ArrangeGap) + 8;
    RectTransform panel = BeginSettings(null, width);
    _settingsArrange = true;
    _arrangeIcons.Clear();
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
      ArrangeButtonAt(
        panel,
        icon,
        hint,
        x,
        () =>
          Arrange(boxes => LayoutArrange.Align(boxes, alignment), L("이미 맞춰져 있어요", "They're already lined up"))
      );
      x += ArrangeButton + ArrangeGap;
    }
    Divider(x + 2);
    x += 9;
    _distributeHorizontal = ArrangeButtonAt(
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
    x += ArrangeButton + ArrangeGap;
    _distributeVertical = ArrangeButtonAt(
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
    RefreshArrange();
  }

  private Button ArrangeButtonAt(RectTransform panel, LayoutAction kind, string hint, float x, Action click)
  {
    Button button = _settingsUi.Button("", panel, x, 10, ArrangeButton, ArrangeButton, click);
    button.gameObject.name = hint;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon icon = _settingsUi
      .Rect("Icon", button.transform, 8, 8, 20, 20)
      .gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
    icon.rectTransform.anchoredPosition = Vector2.zero;
    icon.raycastTarget = false;
    _arrangeIcons.Add(icon);
    LayoutActionHint tooltip = button.gameObject.AddComponent<LayoutActionHint>();
    tooltip.Changed = visible => _actionHint = visible ? hint : "";
    return Control(button);
  }

  // Spacing needs three; the buttons stay put and only grey out.
  private void RefreshArrange()
  {
    if (!_settingsArrange || _distributeHorizontal == null)
      return;
    bool distribute = _selection.Count >= 3;
    _distributeHorizontal.interactable = _distributeVertical.interactable = distribute;
    foreach (LayoutActionIcon icon in _arrangeIcons)
      icon.color = icon.GetComponentInParent<Button>().interactable
        ? SettingsUiFactory.Foreground
        : new Color(.4f, .4f, .4f);
  }

  // Lines the selection up in one space: native viewers measure from their
  // own window origin, read from where the pointer is in both spaces.
  // When nothing would move, says so instead of looking like nothing happened.
  private void Arrange(Action<List<LayoutBounds>> arrange, string unchanged)
  {
    if (_selection.Count < 2)
      return;
    bool native = false;
    foreach (IOverlayLayoutTarget target in _selection)
      native |= target is IOverlayLayoutPointer;
    Vector2 mouse = native ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) : Vector2.zero;
    _arrangeTargets.Clear();
    _arrangeBounds.Clear();
    _arrangeOffsets.Clear();
    foreach (IOverlayLayoutTarget target in _selection)
    {
      Vector2 offset = PointerFor(target, mouse) - mouse;
      Rect bounds = target.Bounds;
      _arrangeTargets.Add(target);
      _arrangeOffsets.Add(offset);
      _arrangeBounds.Add(new LayoutBounds(bounds.x - offset.x, bounds.y - offset.y, bounds.width, bounds.height));
    }
    _arrangeBefore.Clear();
    _arrangeBefore.AddRange(_arrangeBounds);
    arrange(_arrangeBounds);
    bool moves = false;
    for (int i = 0; i < _arrangeBounds.Count; i++)
      moves |=
        Mathf.Abs(_arrangeBounds[i].X - _arrangeBefore[i].X) >= .5f
        || Mathf.Abs(_arrangeBounds[i].Y - _arrangeBefore[i].Y) >= .5f;
    if (!moves)
    {
      ShowEditorNote(unchanged);
      return;
    }
    for (int i = 0; i < _arrangeTargets.Count; i++)
    {
      LayoutBounds box = _arrangeBounds[i];
      Vector2 offset = _arrangeOffsets[i];
      _arrangeTargets[i].SetBounds(new Rect(box.X + offset.x, box.Y + offset.y, box.Width, box.Height));
      _arrangeTargets[i].CommitLayout(true);
    }
    _history.Commit(_host.LayoutSettings);
    RefreshLayouts();
    UpdateFrames();
  }
}

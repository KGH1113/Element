using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor
{
  private void OpenResetConfirmation()
  {
    if (_colorPopover != null && !_colorPopover.Commit())
      return;
    _colorPopover?.Hide();
    FinishDrag();
    _toolbarDragging = false;
    CloseGallery();
    _resetConfirmation = true;
    _galleryWidth = ViewWidth;
    _galleryRebuildAt = float.PositiveInfinity;
    _galleryHeight = ViewHeight;
    _galleryUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    _galleryRoot = _galleryUi.Rect("Reset all confirmation", _root.transform, 0, 0, ViewWidth, ViewHeight).gameObject;
    Image backdrop = _galleryUi.Panel(
      "Backdrop",
      _galleryRoot.transform,
      0,
      0,
      ViewWidth,
      ViewHeight,
      new Color(0, 0, 0, .7f),
      false
    );
    backdrop.raycastTarget = true;
    float scale = Mathf.Min(UiScale, Mathf.Min((ViewWidth - 32) / 500f, (ViewHeight - 32) / 260f));
    RectTransform panel = _galleryUi
      .Panel(
        "Confirmation",
        _galleryRoot.transform,
        (ViewWidth - 500 * scale) * .5f,
        (ViewHeight - 260 * scale) * .5f,
        500,
        260,
        SettingsUiFactory.Background
      )
      .rectTransform;
    panel.localScale = Vector3.one * scale;
    _galleryUi.Text(L("오버레이를 처음 상태로 되돌릴까요?", "Reset all overlays?"), panel, 28, 24, 444, 40, 23);
    _galleryUi.Text(
      L(
        "삭제한 기본 요소를 복원하고 추가한 요소를 제거합니다.\n모든 요소의 위치와 크기도 기본값으로 돌아갑니다.\n초기화 후에도 실행 취소할 수 있습니다.",
        "Restore deleted default elements and remove added ones.\nAll positions and sizes return to their defaults.\nYou can undo this reset."
      ),
      panel,
      28,
      80,
      444,
      100,
      15,
      SettingsUiFactory.Muted
    );
    Button cancel = _galleryUi.Button(L("취소", "Cancel"), panel, 232, 196, 112, 40, CloseGallery);
    _galleryUi.Button(L("전체 초기화", "Reset all"), panel, 356, 196, 116, 40, RestoreDefaultOverlays, true);
    EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
  }

  private void RestoreDefaultOverlays()
  {
    HashSet<string> defaults = new();
    foreach (IOverlayLayoutTarget target in _host.GalleryTargets)
      defaults.Add(target.Id);
    if (_galleryExtras != null)
      foreach (IOverlayLayoutTarget target in _galleryExtras)
        defaults.Add(target.Id);
    OverlayLayoutDefaults.Restore(_host.LayoutSettings, defaults);
    _selected = -1;
    RefreshLayouts();
    _history.Commit(_host.LayoutSettings);
    CloseGallery();
    UpdateFrames();
  }
}

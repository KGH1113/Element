using System;
using System.Collections.Generic;
using Element.Domain.Elements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor
{
  private void OpenResetConfirmation()
  {
    if (!CommitPopovers())
      return;
    ClosePopovers();
    FinishDrag();
    _toolbarDragging = false;
    OpenConfirmation(
      "Reset all confirmation",
      L("오버레이를 처음 상태로 되돌릴까요?", "Reset all overlays?"),
      L(
        "삭제한 기본 요소를 복원하고 추가한 요소를 제거합니다.\n기본 요소의 글자와 위치, 크기도 처음으로 돌아갑니다.\n초기화 후에도 실행 취소할 수 있습니다.",
        "Restore deleted default elements and remove added ones.\nDefault texts, positions and sizes return to the start.\nYou can undo this reset."
      ),
      L("전체 초기화", "Reset all"),
      RestoreDefaultOverlays,
      OpenResetConfirmation
    );
  }

  // Asks before a change that drops work. Cancel is focused, so Enter is safe.
  private void OpenConfirmation(
    string name,
    string title,
    string body,
    string confirm,
    Action confirmed,
    Action rebuild
  )
  {
    RectTransform panel = OpenModal(name, 500, 260, rebuild);
    _modalUi.Text(title, panel, 28, 24, 444, 40, 23);
    _modalUi.Text(body, panel, 28, 80, 444, 100, 15, Settings.SettingsUiFactory.Muted);
    Button cancel = _modalUi.Button(L("취소", "Cancel"), panel, 232, 196, 112, 40, CloseModal);
    _modalUi.Button(
      confirm,
      panel,
      356,
      196,
      116,
      40,
      () =>
      {
        CloseModal();
        confirmed();
      },
      true
    );
    EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
  }

  private void RestoreDefaultOverlays()
  {
    List<string> others = new();
    if (_keyViewers != null)
      others.AddRange(_keyViewers.DefaultIds);
    OverlayElementDefaults.Restore(_host.LayoutSettings, others);
    // Reset may bring back built-in texts whose entries were missing.
    _host.ConfigureLayouts(_host.LayoutSettings);
    RebuildFrames();
    foreach (TargetFrame frame in _frames)
      frame.Target.SetPreview(true);
    _selected = -1;
    _settingsStale = true;
    RefreshLayouts();
    _history.Commit(_host.LayoutSettings);
    UpdateFrames();
  }
}

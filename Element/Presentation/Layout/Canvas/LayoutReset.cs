using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Defaults;
using Element.Presentation.Layout.Kinds;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Canvas;

/// <summary>Reset all: asks first, then brings every overlay back to how it started, as one undo step.</summary>
internal sealed class LayoutReset
{
  private readonly LayoutSession _session;
  private readonly LayoutModal _modal;
  private readonly ILayoutEditing _editing;
  private readonly Action _rebuildFrames;
  private readonly Action _restored;

  /// <param name="rebuildFrames">Makes the frames again for the restored elements.</param>
  /// <param name="restored">After the restore, before it is placed: what follows the selection is built again.</param>
  public LayoutReset(
    LayoutSession session,
    LayoutModal modal,
    ILayoutEditing editing,
    Action rebuildFrames,
    Action restored
  )
  {
    _session = session;
    _modal = modal;
    _editing = editing;
    _rebuildFrames = rebuildFrames;
    _restored = restored;
  }

  public void Confirm()
  {
    if (!_editing.Settle())
      return;
    _modal.Confirm(
      "Reset all confirmation",
      L("오버레이를 처음 상태로 되돌릴까요?", "Reset all overlays?"),
      L(
        "삭제한 기본 요소를 복원하고 추가한 요소를 제거합니다.\n기본 요소의 글자와 위치, 크기도 처음으로 돌아갑니다.\n초기화 후에도 실행 취소할 수 있습니다.",
        "Restore deleted default elements and remove added ones.\nDefault texts, positions and sizes return to the start.\nYou can undo this reset."
      ),
      L("전체 초기화", "Reset all"),
      Restore,
      Confirm
    );
  }

  private void Restore()
  {
    List<string> others = new();
    if (_session.KeyViewers != null)
      others.AddRange(_session.KeyViewers.DefaultIds);
    OverlayElementDefaults.Restore(_session.Host.LayoutSettings, others);
    // Reset may bring back built-in texts whose entries were missing.
    _session.Host.ConfigureLayouts(_session.Host.LayoutSettings);
    _rebuildFrames();
    foreach (LayoutFrame frame in _session.Frames)
      frame.Target.SetPreview(true);
    _editing.Selection.Selected = -1;
    _restored();
    _editing.RefreshLayouts();
    _session.Commit();
    _editing.UpdateFrames();
  }
}

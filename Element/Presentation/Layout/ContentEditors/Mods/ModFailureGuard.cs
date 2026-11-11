using System;
using System.Collections.Generic;
using Element.Application.Mods;
using Element.Presentation.Layout.ContentEditors.Shell;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Where a mod's code runs on its editing screen: a failure shows as a
/// notice once per kind of work and is logged once, and the screen goes on.
/// </summary>
internal sealed class ModFailureGuard
{
  private readonly HashSet<string> _failed = new();
  private readonly Action<ElementClass, string, Exception> _report;

  /// <param name="report">Logs a mod's failure once per kind; see <see cref="Overlay.Mods.ModElements.Failed"/>.</param>
  public ModFailureGuard(Action<ElementClass, string, Exception> report) => _report = report;

  /// <summary>The type being edited.</summary>
  public ElementClass Type { get; set; }

  /// <summary>The screen its notices show on.</summary>
  public OverlayEditorFrame Screen { get; set; }

  public void Clear() => _failed.Clear();

  /// <summary>Whether <paramref name="doing"/> failed before; the preview stops updating then.</summary>
  public bool HasFailed(string doing) => _failed.Contains(doing);

  public bool Run(string doing, Action action)
  {
    try
    {
      action();
      return true;
    }
    catch (Exception exception)
    {
      Failed(doing, exception);
      return false;
    }
  }

  public void Failed(string doing, Exception exception)
  {
    if (!_failed.Add(doing))
      return;
    _report?.Invoke(Type, doing + " in its editing screen", exception);
    Screen?.Notice(
      L(
        Type.ModId
          + " 모드가 이 요소를 "
          + KoreanDoing(doing)
          + " 오류가 났어요. 모드를 업데이트하거나 만든 분께 알려 주세요.",
        Type.ModId + " stopped while " + doing + ". Update the mod or tell its author."
      )
    );
  }

  private static string KoreanDoing(string doing) =>
    doing switch
    {
      "being made" or "starting" => "여는 중에",
      "laying out its editing screen" => "편집 화면을 만드는 중에",
      "updating" => "그리는 중에",
      _ => "바꾸는 중에",
    };
}

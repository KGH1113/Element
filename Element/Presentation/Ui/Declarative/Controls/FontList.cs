using System;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// The overlay font list (search, the default, added and installed fonts) as
/// a view, for a popover's content. A click picks a font at once; the list
/// stays open to try another. It is Element's <see cref="UiFontPopover"/>,
/// which lists only the rows in view.
/// </summary>
internal sealed class FontList : NativeView
{
  private readonly FontListRequest _request;
  private UiFactory _ui;
  private UiFontPopover _list;

  public FontList(FontListRequest request) => _request = request ?? throw new ArgumentNullException(nameof(request));

  /// <summary>The list, for checks.</summary>
  internal UiFontPopover List => _list;

  protected override void Build(RectTransform rect)
  {
    bool reduceMotion = _request.ReduceMotion;
    _ui = new UiFactory(UguiTheme.Font, () => reduceMotion);
    _list = new UiFontPopover(_ui, rect, _request.Fonts, reduceMotion, _request.Picked);
    _list.Show(_request.Title, _request.Current, _request.DefaultName, _request.DefaultDetail);
    _request.Ticks?.Invoke(_list.Tick);
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(
      Fit(proposal.Width, UiFontPopover.PreferredWidth, 260),
      Fit(proposal.Height, UiFontPopover.PreferredHeight, UiFontPopover.MinimumHeight)
    );

  private static float Fit(float offered, float preferred, float minimum) =>
    float.IsNaN(offered) || float.IsInfinity(offered) ? preferred : Mathf.Max(minimum, Mathf.Min(preferred, offered));

  protected override void Resized(float width, float height) => _list?.Place(0, 0, width, height);

  protected override void Teardown()
  {
    _request.Ticks?.Invoke(null);
    _list?.Dispose();
    _ui?.Dispose();
    _list = null;
    _ui = null;
  }
}

/// <summary>What a <see cref="FontList"/> opens with, taken when it opens.</summary>
internal sealed class FontListRequest
{
  public OverlayFonts Fonts;

  /// <summary>Over the list, such as "Font to use instead".</summary>
  public string Title;

  /// <summary>The font marked: a key, or empty for the default row.</summary>
  public string Current;

  /// <summary>The default row's name and the words beside it.</summary>
  public string DefaultName,
    DefaultDetail;
  public bool ReduceMotion;

  /// <summary>A font was clicked: its key.</summary>
  public Action<string> Picked;

  /// <summary>Takes the list's per-frame work while it is open, and null when it closes; the owner runs it every frame.</summary>
  public Action<Action> Ticks;
}

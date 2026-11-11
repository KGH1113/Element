using System;
using System.Collections.Generic;
using Element.Presentation.Settings;
using ElementKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A card or a tab page: parts laid out one under another inside it, the
/// whole group taking one place in the column.
/// </summary>
internal sealed class ModEditorGroup : ModEditorControl
{
  private readonly Func<bool> _shown;

  public ModEditorGroup(
    ModEditorPage page,
    RectTransform rect,
    float padding,
    float header,
    Func<bool> shown,
    bool transparent = false
  )
    : base(page, rect, padding * 2 + header)
  {
    Padding = padding;
    Header = header;
    _shown = shown;
    Transparent = transparent;
    Layout = LayOut;
  }

  /// <summary>
  /// A tab page at the top of the column: its parts flow into the column one
  /// by one, as if it weren't there, rather than taking one place together.
  /// </summary>
  public bool Transparent { get; }

  public List<ModEditorControl> Children { get; } = new();

  public float Padding { get; }

  public float Header { get; }

  public override bool Shown => _shown == null || _shown();

  private void LayOut(float width)
  {
    float inner = Mathf.Max(40, width - Padding * 2);
    float y = Padding + Header;
    bool any = false;
    foreach (ModEditorControl child in Children)
    {
      if (!child.Flows)
        continue;
      bool shown = child.Shown;
      if (child.Rect.gameObject.activeSelf != shown && child is ModEditorGroup)
        child.Rect.gameObject.SetActive(shown);
      if (!shown)
        continue;
      child.Layout?.Invoke(child.WidthOverride ?? inner);
      SettingsUiFactory.Place(child.Rect, Padding, y, child.WidthOverride ?? inner, child.Height);
      y += child.Height + ModEditorPage.RowGap;
      any = true;
    }
    if (any)
      y -= ModEditorPage.RowGap;
    Height = y + Padding;
  }
}

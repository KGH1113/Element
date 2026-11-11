using System;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>A part on a mod's editing screen: one row of the column, or a group of them.</summary>
internal class ModEditorControl : EditorControl
{
  private readonly ModEditorPage _page;
  private Image _hitArea;
  private ModHint _hint;

  public ModEditorControl(ModEditorPage page, RectTransform rect, float height)
  {
    _page = page;
    Rect = rect;
    Height = height;
  }

  public override RectTransform Rect { get; }

  /// <summary>Lays the part's pieces across a row this wide, and may set <see cref="Height"/>.</summary>
  public Action<float> Layout { get; set; }

  public float Height { get; set; }

  public float? WidthOverride { get; private set; }

  public bool Flows { get; private set; } = true;

  /// <summary>The card or tab page it belongs to; null at the top of the column.</summary>
  public ModEditorGroup Group { get; set; }

  /// <summary>Whether it takes room in the column: not in a tab that isn't picked.</summary>
  public virtual bool Shown => true;

  public override EditorControl At(float x, float y)
  {
    Flows = false;
    // Out of any card: placed on the screen itself.
    if (Group != null)
    {
      Group.Children.Remove(this);
      Group = null;
      Rect.SetParent(_page.Root, false);
    }
    UiFactory.Place(Rect, x, y, WidthOverride ?? ModEditorPage.ColumnWidth, Height);
    _page.Changed();
    return this;
  }

  public override EditorControl Width(float width)
  {
    WidthOverride = Mathf.Max(40, width);
    Layout?.Invoke(WidthOverride.Value);
    if (!Flows)
      Rect.sizeDelta = new Vector2(WidthOverride.Value, Height);
    _page.Changed();
    return this;
  }

  public override EditorControl Hint(string text)
  {
    if (_hitArea == null)
    {
      // The whole row answers the pointer, its label included.
      _hitArea = Rect.gameObject.GetComponent<Image>() ?? Rect.gameObject.AddComponent<Image>();
      if (_hitArea.sprite == null)
        _hitArea.color = Color.clear;
      _hitArea.raycastTarget = true;
      _hint = Rect.gameObject.AddComponent<ModHint>();
      _hint.Page = _page;
      _hint.Anchor = Rect;
    }
    _hint.Text = text;
    return this;
  }

  public override bool Visible
  {
    get => Rect.gameObject.activeSelf;
    set
    {
      if (Rect.gameObject.activeSelf != value)
        Rect.gameObject.SetActive(value);
    }
  }
}

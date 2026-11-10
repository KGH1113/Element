using System;
using System.Collections.Generic;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>What a mod's editing screen asks of the editor around it.</summary>
internal interface IModEditorHost
{
  /// <summary>
  /// Runs a part's change as one edit: undo, then the mod's callback, then the
  /// preview; <paramref name="rebuild"/> makes the parts again to show it.
  /// </summary>
  void Change(object key, Action apply, bool rebuild);

  void OpenColor(ModColorRequest request);

  void OpenPaint(ModPaintRequest request);

  void OpenList(ModListRequest request);

  void OpenFont(ModFontRequest request);

  void ShowTip(string text, RectTransform anchor);

  void HideTip();

  /// <summary>The tab last picked in the tabs named <paramref name="key"/>, so undo keeps it.</summary>
  int TabOf(string key);

  void SetTab(string key, int index);

  OverlayFonts Fonts { get; }
}

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
    SettingsUiFactory.Place(Rect, x, y, WidthOverride ?? ModEditorPage.ColumnWidth, Height);
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

/// <summary>Tabs on a mod's editing screen: a bar, and a page of parts per tab.</summary>
internal sealed class ModEditorTabs : EditorTabs
{
  private readonly ModEditorPage _page;
  private readonly string _key;
  private readonly List<ModEditorGroup> _pages = new();
  private SettingsSegmentedControl _segments;
  private int _selected;

  public ModEditorTabs(ModEditorPage page, string key, int selected)
  {
    _page = page;
    _key = key;
    _selected = selected;
  }

  public override EditorControl Bar => BarControl;

  public ModEditorControl BarControl { get; set; }

  public SettingsSegmentedControl Segments
  {
    set => _segments = value;
  }

  public List<ModEditorGroup> Pages => _pages;

  public override int Selected
  {
    get => _selected;
    set
    {
      value = Mathf.Clamp(value, 0, Mathf.Max(0, _pages.Count - 1));
      if (value == _selected)
        return;
      _selected = value;
      _segments?.Select(value, false);
      _page.TabPicked(_key, value);
    }
  }

  public override IDisposable Page(int index)
  {
    if (index < 0 || index >= _pages.Count)
      return _page.Into(null);
    return _page.Into(_pages[index]);
  }
}

/// <summary>Shows a part's hint while the pointer is over it.</summary>
internal sealed class ModHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  public ModEditorPage Page;
  public RectTransform Anchor;
  public string Text;

  public void OnPointerEnter(PointerEventData eventData)
  {
    if (!string.IsNullOrEmpty(Text))
      Page?.ShowTip(Text, Anchor);
  }

  public void OnPointerExit(PointerEventData eventData) => Page?.HideTip();

  private void OnDisable() => Page?.HideTip();
}

/// <summary>Turns presses and drags on a range slider's track into the handle they move.</summary>
internal sealed class ModRangeDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
  /// <summary>The pointer at a fraction across the track; true when it just went down.</summary>
  public Action<float, bool> Moved;

  public RectTransform Track;

  public void OnPointerDown(PointerEventData eventData) => Report(eventData, true);

  public void OnDrag(PointerEventData eventData) => Report(eventData, false);

  private void Report(PointerEventData eventData, bool down)
  {
    if (
      Track == null
      || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
        Track,
        eventData.position,
        eventData.pressEventCamera,
        out Vector2 local
      )
    )
      return;
    Rect rect = Track.rect;
    float fraction = rect.width > 0 ? Mathf.Clamp01((local.x - rect.xMin) / rect.width) : 0;
    Moved?.Invoke(fraction, down);
  }
}

/// <summary>A color part asking the editor to open the picker beside it.</summary>
internal sealed class ModColorRequest
{
  public ModColorRequest(string title, Color color, bool opacity, RectTransform anchor, Action<Color> changed)
  {
    Title = title;
    Color = color;
    Opacity = opacity;
    Anchor = anchor;
    Changed = changed;
  }

  public string Title { get; }
  public Color Color { get; }
  public bool Opacity { get; }
  public RectTransform Anchor { get; }
  public Action<Color> Changed { get; }
}

/// <summary>A paint part asking for the full picker: highlight, custom color or gradient.</summary>
internal sealed class ModPaintRequest
{
  public ModPaintRequest(string title, Paint paint, RectTransform anchor, Action<Paint> changed)
  {
    Title = title;
    Paint = paint;
    Anchor = anchor;
    Changed = changed;
  }

  public string Title { get; }
  public Paint Paint { get; }
  public RectTransform Anchor { get; }
  public Action<Paint> Changed { get; }
}

/// <summary>A dropdown asking for its list of options under it.</summary>
internal sealed class ModListRequest
{
  public ModListRequest(
    string title,
    IReadOnlyList<string> options,
    int selected,
    RectTransform anchor,
    Action<int> picked
  )
  {
    Title = title;
    Options = options;
    Selected = selected;
    Anchor = anchor;
    Picked = picked;
  }

  public string Title { get; }
  public IReadOnlyList<string> Options { get; }
  public int Selected { get; }
  public RectTransform Anchor { get; }
  public Action<int> Picked { get; }
}

/// <summary>A font part asking for Element's font list beside it.</summary>
internal sealed class ModFontRequest
{
  public ModFontRequest(string title, string font, RectTransform anchor, Action<string> picked)
  {
    Title = title;
    Font = font;
    Anchor = anchor;
    Picked = picked;
  }

  public string Title { get; }
  public string Font { get; }
  public RectTransform Anchor { get; }
  public Action<string> Picked { get; }
}

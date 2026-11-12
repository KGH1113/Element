using System;
using System.Collections.Generic;
using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A part on a mod's <see cref="ModEditorPage"/>: what it shows (its control,
/// made each time the page mounts), and where: in the column, in a card or
/// tab page, or placed by the mod with <see cref="At"/>.
/// </summary>
internal class ModPart : EditorControl
{
  private readonly ModEditorPage _page;
  private RectTransform _rect;
  private ModPartView _mounted;

  public ModPart(ModEditorPage page, int id)
  {
    _page = page;
    Id = id;
  }

  /// <summary>The part's number on its page, in the order the mod made them.</summary>
  public int Id { get; }

  /// <summary>Makes the part's control; called again each time the page mounts.</summary>
  public Func<View> Control { get; set; }

  /// <summary>Hidden by the mod: the part keeps its place.</summary>
  public readonly State<bool> Hidden = new(false);

  /// <summary>Shown while the pointer is over the part; null for none.</summary>
  public readonly State<string> HintText = new(null);

  /// <summary>The column it stands in now; -1 while it doesn't flow (its tab isn't picked).</summary>
  public readonly State<int> Column = new(-1);

  /// <summary>Its height in the column, measured when the page mounted.</summary>
  public float Height { get; set; }

  /// <summary>The column the page is putting it in, before it moves there.</summary>
  internal int Arranged;

  public float? WidthOverride { get; private set; }

  /// <summary>Where the mod put it (screen units, top left); null while it flows.</summary>
  public Vector2? Position { get; private set; }

  /// <summary>The card or tab page it belongs to; null at the top of the column.</summary>
  public ModGroup Group { get; set; }

  /// <summary>Its width in a column, or wherever it stands.</summary>
  public float FrameWidth => WidthOverride ?? ModEditorPage.ColumnWidth;

  /// <summary>The object made for <see cref="Rect"/>; null while no one asked for it.</summary>
  public RectTransform OwnRect => _rect;

  public override RectTransform Rect
  {
    get
    {
      if (_rect == null)
      {
        _rect = _page.NewPartRect(this);
        _page.StructureChanged();
      }
      return _rect;
    }
  }

  public override EditorControl At(float x, float y)
  {
    Position = new Vector2(x, y);
    _page.Free(this);
    return this;
  }

  public override EditorControl Width(float width)
  {
    WidthOverride = Mathf.Max(40, width);
    _page.StructureChanged();
    return this;
  }

  public override EditorControl Hint(string text)
  {
    bool had = HintText.Value != null;
    HintText.Value = text ?? "";
    if (!had)
      _page.StructureChanged();
    return this;
  }

  public override bool Visible
  {
    get => !Hidden.Value;
    set => Hidden.Value = !value;
  }

  /// <summary>Takes the object made for <see cref="Rect"/> back from a view going away.</summary>
  internal void Park() => _page.Park(_rect);

  // The view showing the part now, so the page can measure it.
  internal void Mounted(ModPartView view) => _mounted = view;

  internal void Unmounted(ModPartView view)
  {
    if (_mounted == view)
      _mounted = null;
  }

  /// <summary>Its height at <see cref="FrameWidth"/>, measured on the view showing it; the last one when none does.</summary>
  public float Measure() =>
    _mounted != null ? _mounted.SizeThatFits(new Proposal(FrameWidth, float.NaN)).Height : Height;
}

/// <summary>
/// A card, or the page of a tab: parts laid out one under another inside it,
/// the whole group taking one place in the column. A tab page at the top of
/// the column is <see cref="Transparent"/>: its parts flow on their own.
/// </summary>
internal sealed class ModGroup : ModPart
{
  public ModGroup(ModEditorPage page, int id, string title)
    : base(page, id) => Title = title;

  /// <summary>A tab's page.</summary>
  public ModGroup(ModEditorPage page, int id, ModEditorTabs tabs, int index, bool transparent)
    : base(page, id)
  {
    Tabs = tabs;
    Index = index;
    Transparent = transparent;
  }

  /// <summary>A card's title; null for none.</summary>
  public string Title { get; }

  /// <summary>The tabs whose page this is; null for a card.</summary>
  public ModEditorTabs Tabs { get; }

  public int Index { get; }

  public bool Transparent { get; }

  public List<ModPart> Children { get; } = new();

  /// <summary>Whether it shows: always for a card, while its tab is picked for a page.</summary>
  public bool Shown => Tabs == null || Tabs.Selected == Index;
}

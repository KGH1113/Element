using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// The dock's values: under words that name where they go
/// (<see cref="Place"/>), a section per built-in category and per mod, one
/// under the other. Only values that fit where the dock puts values now are
/// listed; how many were left out is said after them. With nothing open, or a
/// sign to pick, it lists nothing and says what to do. The chips show what
/// their values read now, a few times a second. A chip clicked goes up as the
/// dock's <see cref="IInputDockActions"/>. <see cref="ValueBrowserView"/> draws it.
/// </summary>
internal sealed class ValueBrowserViewModel : ViewModel
{
  private const float FollowSeconds = .25f;

  private static readonly string MineKey = OverlayValueCategory.Mine.ToString();

  private readonly OverlayValues _values;
  private readonly FormulaEditing _editing;
  private readonly bool _caret;
  private readonly List<ValueFolderViewModel> _folders = new();
  private readonly List<ValueChipViewModel> _listed = new();
  private readonly StringBuilder _scratch = new(64);
  private ValuePlace _place;
  private long _seen;
  private int _made = OverlayCustomValues.Version;

  /// <param name="values">What the overlay shows now; null shows names only.</param>
  /// <param name="actions">Where a clicked value goes.</param>
  /// <param name="editing">What the dock has open: where values go.</param>
  /// <param name="caret">With nothing open, values go in at a text's caret.</param>
  public ValueBrowserViewModel(OverlayValues values, IInputDockActions actions, FormulaEditing editing, bool caret)
  {
    _values = values;
    _editing = editing;
    _caret = caret;
    // The built-in categories in order, then each mod that adds values.
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
      if (!folder.IsMod)
        _folders.Add(Own(new ValueFolderViewModel(folder, actions.Pick)));
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
      if (folder.IsMod)
        _folders.Add(Own(new ValueFolderViewModel(folder, actions.Pick)));
    // What the dock has open tells the place when it changes.
    Place = Computed(() => ValuePlace.Of(_editing, _caret, Choice.Value));
    Refit(ValuePlace.Of(editing, caret, ""));
  }

  /// <summary>
  /// A field picked from a fixed set (a font, a point) is open: what the
  /// list's place says instead of listing values; empty while none is.
  /// </summary>
  public readonly State<string> Choice = new("");

  /// <summary>Where the dock puts values now; it changes only as what the dock has open, or picked, changes.</summary>
  public readonly Computed<ValuePlace> Place;

  /// <summary>The place takes values from the list; while it doesn't, <see cref="Rest"/> shows instead.</summary>
  public readonly State<bool> Listing = new(false);

  /// <summary>In place of the list while it lists nothing: what to do.</summary>
  public readonly State<string> Rest = new("");

  /// <summary>What goes where the dock puts values, for the mark beside <see cref="Title"/>.</summary>
  public readonly State<OverlayValueType> Type = new(OverlayValueType.Any);

  /// <summary>Over the list: where the values go, "숫자 칸에 넣을 수 있는 값".</summary>
  public readonly State<string> Title = new("");

  /// <summary>After the list: how many values were left out as not fitting; empty when none were.</summary>
  public readonly State<string> Hidden = new("");

  /// <summary>The sections in order: the built-in categories that have values, then one per mod.</summary>
  public IReadOnlyList<ValueFolderViewModel> Sections => _folders;

  /// <summary>Ticks a few times a second while the chips are shown: when they look at what their values read.</summary>
  public readonly Timeline Live = Timeline.Periodic(FollowSeconds);

  /// <summary>
  /// The listed chips take what their values read now. With nothing new it
  /// compares one number and makes nothing.
  /// </summary>
  public void Follow()
  {
    if (_values == null || _values.Stamp == _seen)
      return;
    OverlayValueMask changed = _values.ChangedSince(_seen);
    _seen = _values.Stamp;
    for (int i = 0; i < _listed.Count; i++)
      if (_listed[i].Shows(changed))
        _listed[i].Follow(_values, _scratch);
  }

  /// <summary>
  /// Each frame: when a value of the player's was made or renamed, "My
  /// values" takes the catalog's list again. With nothing new it compares one
  /// number.
  /// </summary>
  public void FollowMade()
  {
    if (_made == OverlayCustomValues.Version)
      return;
    _made = OverlayCustomValues.Version;
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
    {
      ValueFolderViewModel model = _folders.Find(each => each.Key == folder.Key);
      if (model != null && folder.Key == MineKey)
        model.Reload(folder);
    }
    Refit(_place);
  }

  /// <summary>
  /// Where the dock puts values is now <paramref name="place"/>: only the
  /// values that fit there stay listed. Runs when the place changes, never
  /// each frame; chips whose fit didn't change are left as they are.
  /// </summary>
  public void Refit(ValuePlace place)
  {
    _place = place;
    int hidden = 0;
    foreach (ValueFolderViewModel folder in _folders)
      hidden += folder.Fit(place);
    Listing.Value = place.Lists;
    Rest.Value = place.Hint;
    Title.Value = place.Title;
    Type.Value = place.Type;
    Hidden.Value =
      place.Lists && hidden > 0
        ? L(
          "이 자리에 맞지 않는 값 " + hidden.ToString(CultureInfo.InvariantCulture) + "개는 숨겼어요.",
          hidden.ToString(CultureInfo.InvariantCulture)
            + (hidden == 1 ? " value that doesn't fit here is hidden." : " values that don't fit here are hidden.")
        )
        : "";
    // The chips now listed show their values at once.
    _listed.Clear();
    foreach (ValueFolderViewModel folder in _folders)
    foreach (ValueChipViewModel chip in folder.Chips)
      if (chip.Fits.Value)
        _listed.Add(chip);
    foreach (ValueChipViewModel chip in _listed)
      chip.Follow(_values, _scratch);
    _seen = _values?.Stamp ?? 0;
  }

  protected override void OnDisposed() => Live.Dispose();
}

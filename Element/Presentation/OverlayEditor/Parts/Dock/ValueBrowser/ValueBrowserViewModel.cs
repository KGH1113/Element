using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// The dock's values: under the mark of the type that goes there, a section
/// per built-in category and per mod, one under the other. Only values that
/// fit where the dock puts values now are listed; where none can go, the
/// list is clear. The chips show what their values read now, a few times a
/// second. A chip clicked goes up as the dock's
/// <see cref="IInputDockActions"/>. <see cref="ValueBrowserView"/> draws it.
/// </summary>
internal sealed class ValueBrowserViewModel : ViewModel
{
  private const float FollowSeconds = .25f;

  private readonly OverlayValues _values;
  private readonly FormulaEditing _editing;
  private readonly bool _caret;
  private readonly List<ValueFolderViewModel> _folders = new();
  private readonly List<ValueChipViewModel> _listed = new();
  private readonly StringBuilder _scratch = new(64);
  private long _seen;

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
    Place = Computed(() => ValuePlace.Of(_editing, _caret, Closed.Value));
    Refit(ValuePlace.Of(editing, caret, false));
  }

  /// <summary>A field picked from a list of its own (a font) is open: no values are listed for it.</summary>
  public readonly State<bool> Closed = new(false);

  /// <summary>Where the dock puts values now; it changes only as what the dock has open, or picked, changes.</summary>
  public readonly Computed<ValuePlace> Place;

  /// <summary>The place takes values from the list; while it doesn't, the list is clear.</summary>
  public readonly State<bool> Listing = new(false);

  /// <summary>What goes where the dock puts values, for the mark over the list.</summary>
  public readonly State<OverlayValueType> Type = new(OverlayValueType.Any);

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
  /// Where the dock puts values is now <paramref name="place"/>: only the
  /// values that fit there stay listed. Runs when the place changes, never
  /// each frame; chips whose fit didn't change are left as they are.
  /// </summary>
  public void Refit(ValuePlace place)
  {
    foreach (ValueFolderViewModel folder in _folders)
      folder.Fit(place);
    Listing.Value = place.Lists;
    Type.Value = place.Type;
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

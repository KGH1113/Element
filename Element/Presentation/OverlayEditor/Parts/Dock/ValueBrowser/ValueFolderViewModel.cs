using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// One place in the dock's list (a built-in category, or everything one mod
/// adds) and the page it opens: its values, in a row per group under the
/// group's heading when a mod made several. Only the values that fit where
/// the dock puts values now are listed and counted, and a place none of
/// whose values fit is not listed at all (<see cref="Fit"/>). It is lit while
/// open, and dimmed while a search shows instead. "My values" changes while
/// the dock is open (<see cref="Reload"/>).
/// </summary>
internal sealed class ValueFolderViewModel : ViewModel
{
  // A row at rest is the list's own opaque base; lit and dimmed it is opaque too.
  private static readonly UiColor Rest = Color.clear,
    RestInk = new Color(.8f, .8f, .8f);
  private readonly UiColor _lit;
  private readonly Action<OverlayValueDefinition> _pick;
  private List<ValueChipViewModel> _chips = new();

  public ValueFolderViewModel(
    OverlayValueFolder folder,
    Action<OverlayValueDefinition> pick,
    Action<ValueFolderViewModel> open
  )
  {
    Key = folder.Key;
    Name = ValueSectionNames.FolderName(folder);
    IsMod = folder.IsMod;
    _pick = pick;
    TakeValues(folder);
    _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .3f);
    Select = Command(() => open(this));
  }

  /// <summary>Takes <paramref name="folder"/>'s values in place of its own; its page is drawn again.</summary>
  public void Reload(OverlayValueFolder folder)
  {
    List<ValueChipViewModel> old = _chips;
    TakeValues(folder);
    foreach (ValueChipViewModel chip in old)
      chip.Dispose();
    Version.Value++;
  }

  private void TakeValues(OverlayValueFolder folder)
  {
    List<ValueChipRow> groups = new();
    List<ValueChipViewModel> all = new();
    foreach (OverlayValueSection section in folder.Sections)
    {
      ValueChipViewModel[] chips = new ValueChipViewModel[section.Values.Count];
      for (int i = 0; i < chips.Length; i++)
      {
        OverlayValueDefinition value = section.Values[i];
        chips[i] = new ValueChipViewModel(value, OverlayValueSearch.Index(folder, value), _pick);
        all.Add(chips[i]);
      }
      // A heading only tells groups apart; one group needs none.
      string heading =
        folder.Sections.Count > 1 && section.Group != null ? ValueSectionNames.GroupName(section.Group) : null;
      groups.Add(new ValueChipRow(heading, chips));
    }
    Groups = groups;
    Chips = all;
    _chips = all;
  }

  protected override void OnDisposed()
  {
    foreach (ValueChipViewModel chip in _chips)
      chip.Dispose();
  }

  /// <summary>Names it the same way from one opening to the next; it names the drawn row too.</summary>
  public readonly string Key;

  public readonly string Name;

  /// <summary>How many of its values fit where the dock puts values now.</summary>
  public readonly State<string> Count = new("");

  /// <summary>Some of its values fit: it is listed.</summary>
  public readonly State<bool> Shown = new(true);

  /// <summary>Goes up when its values were taken again: its page and search rows are drawn anew.</summary>
  public readonly State<int> Version = new(0);

  public readonly bool IsMod;

  /// <summary>Its rows: one for a category or a mod with one group, else one per group.</summary>
  public IReadOnlyList<ValueChipRow> Groups { get; private set; }

  /// <summary>Every value it holds, in order.</summary>
  public IReadOnlyList<ValueChipViewModel> Chips { get; private set; }

  /// <summary>Its page shows.</summary>
  public readonly State<bool> Open = new(false);

  public readonly State<UiColor> Fill = new(Rest);

  public readonly State<UiColor> Ink = new(RestInk);

  /// <summary>The search finds some of its values.</summary>
  public readonly State<bool> Found = new(false);

  public Command Select { get; }

  /// <summary>Lit while it is the place open, dimmed while a search shows instead.</summary>
  public void Show(bool open, bool searching)
  {
    Open.Value = open && !searching;
    Fill.Value =
      !open ? Rest
      : searching ? UiPalette.EditorControl
      : _lit;
    Ink.Value = open && !searching ? UiPalette.Foreground : RestInk;
  }

  /// <summary>
  /// Keeps the values that fit <paramref name="place"/> listed, and their
  /// rows; it is listed while one does. How many don't fit.
  /// </summary>
  public int Fit(ValuePlace place)
  {
    int fits = 0;
    foreach (ValueChipRow row in Groups)
    {
      bool any = false;
      foreach (ValueChipViewModel chip in row.Chips)
      {
        bool fit = place.Fits(chip.Value);
        chip.Fits.Value = fit;
        any |= fit;
        if (fit)
          fits++;
      }
      row.Shown.Value = any;
    }
    Count.Value = fits.ToString(CultureInfo.InvariantCulture);
    Shown.Value = fits > 0;
    return Chips.Count - fits;
  }

  /// <summary>Marks the values that fit and <paramref name="folded"/> finds (none for empty); how many it found.</summary>
  public int Find(string folded)
  {
    int found = 0;
    foreach (ValueChipViewModel chip in Chips)
    {
      bool match = chip.Fits.Value && OverlayValueSearch.Matches(chip.Index, folded);
      chip.Found.Value = match;
      if (match)
        found++;
    }
    Found.Value = found > 0;
    return found;
  }
}

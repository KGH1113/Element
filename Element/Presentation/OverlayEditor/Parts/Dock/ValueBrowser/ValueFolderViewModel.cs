using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// One section of the dock's values (a built-in category, or everything one
/// mod adds): its heading and its values, in a row per group under the
/// group's heading when a mod made several. Only the values that fit where
/// the dock puts values now are listed, and a section none of whose values
/// fit is not listed at all (<see cref="Fit"/>). "My values" changes while
/// the dock is open (<see cref="Reload"/>).
/// </summary>
internal sealed class ValueFolderViewModel : ViewModel
{
  private readonly Action<OverlayValueDefinition> _pick;
  private List<ValueChipViewModel> _chips = new();

  public ValueFolderViewModel(OverlayValueFolder folder, Action<OverlayValueDefinition> pick)
  {
    Key = folder.Key;
    Name = ValueSectionNames.FolderName(folder);
    IsMod = folder.IsMod;
    _pick = pick;
    TakeValues(folder);
  }

  /// <summary>Takes <paramref name="folder"/>'s values in place of its own; the section is drawn again.</summary>
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
        chips[i] = new ValueChipViewModel(section.Values[i], _pick);
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

  /// <summary>Names it the same way from one opening to the next; it names the drawn section too.</summary>
  public readonly string Key;

  /// <summary>Its heading: "판정", or the mod's name.</summary>
  public readonly string Name;

  /// <summary>Some of its values fit: it is listed.</summary>
  public readonly State<bool> Shown = new(true);

  /// <summary>Goes up when its values were taken again: the section is drawn anew.</summary>
  public readonly State<int> Version = new(0);

  public readonly bool IsMod;

  /// <summary>Its rows: one for a category or a mod with one group, else one per group.</summary>
  public IReadOnlyList<ValueChipRow> Groups { get; private set; }

  /// <summary>Every value it holds, in order.</summary>
  public IReadOnlyList<ValueChipViewModel> Chips { get; private set; }

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
    Shown.Value = fits > 0;
    return Chips.Count - fits;
  }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// One place in the dock's list (a built-in category, or everything one mod
/// adds) and the page it opens: its values, in a row per group under the
/// group's heading when a mod made several. It is lit while open, and
/// dimmed while a search shows instead.
/// </summary>
internal sealed class ValueFolderModel : ViewModel
{
  // A row at rest is the list's own opaque base; lit and dimmed it is opaque too.
  private static readonly UiColor Rest = Color.clear,
    RestInk = new Color(.8f, .8f, .8f);
  private readonly UiColor _lit;

  public ValueFolderModel(
    OverlayValueFolder folder,
    Action<OverlayValueDefinition> pick,
    Action<ValueFolderModel> open
  )
  {
    Key = folder.Key;
    Name = ValueSectionNames.FolderName(folder);
    Count = folder.Count.ToString(CultureInfo.InvariantCulture);
    IsMod = folder.IsMod;
    List<ValueGroupModel> groups = new();
    List<ValueChipModel> all = new();
    foreach (OverlayValueSection section in folder.Sections)
    {
      ValueChipModel[] chips = new ValueChipModel[section.Values.Count];
      for (int i = 0; i < chips.Length; i++)
      {
        OverlayValueDefinition value = section.Values[i];
        chips[i] = Own(new ValueChipModel(value, OverlayValueSearch.Index(folder, value), pick));
        all.Add(chips[i]);
      }
      // A heading only tells groups apart; one group needs none.
      string heading =
        folder.Sections.Count > 1 && section.Group != null ? ValueSectionNames.GroupName(section.Group) : null;
      groups.Add(new ValueGroupModel(heading, chips));
    }
    Groups = groups;
    Chips = all;
    _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .3f);
    Select = Command(() => open(this));
  }

  /// <summary>Names it the same way from one opening to the next; it names the drawn row too.</summary>
  public readonly string Key;

  public readonly string Name;

  /// <summary>How many values it holds.</summary>
  public readonly string Count;

  public readonly bool IsMod;

  /// <summary>Its rows: one for a category or a mod with one group, else one per group.</summary>
  public readonly IReadOnlyList<ValueGroupModel> Groups;

  /// <summary>Every value it holds, in order.</summary>
  public readonly IReadOnlyList<ValueChipModel> Chips;

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

  /// <summary>Marks the values <paramref name="folded"/> finds (none for empty); how many it found.</summary>
  public int Find(string folded)
  {
    int found = 0;
    foreach (ValueChipModel chip in Chips)
    {
      bool match = OverlayValueSearch.Matches(chip.Index, folded);
      chip.Found.Value = match;
      if (match)
        found++;
    }
    Found.Value = found > 0;
    return found;
  }
}

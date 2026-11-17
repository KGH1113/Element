using System;
using System.Collections.Generic;
using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// A card, or the page of a tab: parts laid out one under another inside it,
/// the whole group taking one place in the column.
/// </summary>
internal sealed class ModGroup : ModPart
{
  public ModGroup(ModEditorPage page, int id, string title)
    : base(page, id) => Title = title;

  /// <summary>A tab's page.</summary>
  public ModGroup(ModEditorPage page, int id, ModEditorTabs tabs, int index)
    : base(page, id)
  {
    Tabs = tabs;
    Index = index;
  }

  /// <summary>A card's title; null for none.</summary>
  public string Title { get; }

  /// <summary>The tabs whose page this is; null for a card.</summary>
  public ModEditorTabs Tabs { get; }

  public int Index { get; }

  public List<ModPart> Children { get; } = new();

  /// <summary>Whether it shows: always for a card, while its tab is picked for a page.</summary>
  public bool Shown => Tabs == null || Tabs.Selected == Index;
}

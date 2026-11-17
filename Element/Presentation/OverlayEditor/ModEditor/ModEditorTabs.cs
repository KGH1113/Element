using System;
using System.Collections.Generic;
using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>Tabs on a mod's editing screen: a bar, and a page of parts per tab.</summary>
internal sealed class ModEditorTabs : EditorTabs
{
  private readonly ModEditorPage _page;
  private readonly string _key;
  private readonly List<ModGroup> _pages = new();

  public ModEditorTabs(ModEditorPage page, string key, int selected)
  {
    _page = page;
    _key = key;
    Pick = new ModValueViewModel<int>(selected, Picked);
  }

  /// <summary>The picked tab, for the bar to bind to and the pages to follow.</summary>
  public ModValueViewModel<int> Pick { get; }

  public override EditorControl Bar => BarPart;

  public ModPart BarPart { get; set; }

  public List<ModGroup> Pages => _pages;

  public override int Selected
  {
    get => Pick.Value;
    set
    {
      Pick.Value = Mathf.Clamp(value, 0, Mathf.Max(0, _pages.Count - 1));
      // Set by the mod rather than the bar: the bar and pages follow.
      Runtime.Probe();
    }
  }

  private void Picked(int index) => _page.TabPicked(_key, index);

  public override IDisposable Page(int index)
  {
    if (index < 0 || index >= _pages.Count)
      return _page.Into(null);
    return _page.Into(_pages[index]);
  }
}

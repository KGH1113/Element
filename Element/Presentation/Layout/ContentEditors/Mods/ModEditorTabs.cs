using System;
using System.Collections.Generic;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>Tabs on a mod's editing screen: a bar, and a page of parts per tab.</summary>
internal sealed class ModEditorTabs : EditorTabs
{
  private readonly ModEditorPage _page;
  private readonly string _key;
  private readonly List<ModEditorGroup> _pages = new();
  private UiSegmentedControl _segments;
  private int _selected;

  public ModEditorTabs(ModEditorPage page, string key, int selected)
  {
    _page = page;
    _key = key;
    _selected = selected;
  }

  public override EditorControl Bar => BarControl;

  public ModEditorControl BarControl { get; set; }

  public UiSegmentedControl Segments
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

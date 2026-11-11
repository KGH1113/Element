using System;

namespace ElementKit;

/// <summary>Tabs on the editing screen; see <see cref="EditorPage.Tabs"/>.</summary>
public abstract class EditorTabs
{
  // Only Element implements this: a mod uses the one Element hands it and
  // cannot subclass it, so new members never break mods.
  internal EditorTabs() { }

  /// <summary>The tab bar.</summary>
  public abstract EditorControl Bar { get; }

  /// <summary>The picked tab.</summary>
  public abstract int Selected { get; set; }

  /// <summary>Parts made until this is disposed belong to tab <paramref name="index"/>.</summary>
  public abstract IDisposable Page(int index);
}

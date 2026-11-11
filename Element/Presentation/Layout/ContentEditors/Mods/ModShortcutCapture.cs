using System;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>A shortcut part waiting for the next key.</summary>
internal sealed class ModShortcutCapture
{
  public ElementKit.Shortcut Current;
  public string Prompt;
  public Action Show;
  public Action<ElementKit.Shortcut> Assign;
}

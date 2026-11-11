using System;
using Element.Presentation.Ui.Graphics;

namespace Element.Presentation.Layout.Kinds;

/// <summary>A kind's row in the add menu.</summary>
internal sealed class AddMenuRow
{
  public AddMenuRow(UiIconKind icon, string title, string detail, bool enabled, Action pressed)
  {
    Icon = icon;
    Title = title;
    Detail = detail;
    Enabled = enabled;
    Pressed = pressed;
  }

  public UiIconKind Icon { get; }
  public string Title { get; }

  /// <summary>What it adds, or why it can't be added now.</summary>
  public string Detail { get; }
  public bool Enabled { get; }
  public Action Pressed { get; }
}

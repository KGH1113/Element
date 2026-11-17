using System;
using Element.Presentation.Ui.Graphics;

namespace Element.Presentation.OverlayEditor.Canvas.AddMenu;

/// <summary>A row of the add menu, or of a list it opens beside it.</summary>
internal sealed class AddMenuRow
{
  /// <param name="detail">What it adds, or why it can't be added now; null for a row of one line.</param>
  /// <param name="name">What finds the row, such as "Add Square"; "Add " and the icon by default.</param>
  public AddMenuRow(UiIconKind icon, string title, string detail, bool enabled, Action pressed, string name = null)
  {
    Icon = icon;
    Title = title;
    Detail = detail;
    Enabled = enabled;
    Pressed = pressed;
    Name = name ?? "Add " + icon;
  }

  public UiIconKind Icon { get; }
  public string Title { get; }

  /// <summary>What it adds, or why it can't be added now; null for none.</summary>
  public string Detail { get; }
  public bool Enabled { get; }
  public Action Pressed { get; }

  /// <summary>The name of the row's button.</summary>
  public string Name { get; }
}

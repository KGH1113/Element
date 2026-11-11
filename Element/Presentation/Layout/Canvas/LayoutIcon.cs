using Element.Presentation.Ui.Graphics;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>An icon on a button, with the button looked up once for its state each frame.</summary>
internal readonly struct LayoutIcon
{
  public readonly UiIcon Icon;
  public readonly Button Button;

  public LayoutIcon(UiIcon icon, Button button)
  {
    Icon = icon;
    Button = button;
  }
}

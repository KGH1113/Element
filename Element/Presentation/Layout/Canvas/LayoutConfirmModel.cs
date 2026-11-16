using System;
using ElementUI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>A question before a change that drops work: what it does, Cancel, and the change.</summary>
internal sealed class LayoutConfirmModel : ViewModel
{
  public LayoutConfirmModel(string title, string body, string confirm, Action cancelled, Action confirmed)
  {
    Title = title;
    Body = body;
    ConfirmLabel = confirm;
    Cancel = Command(() => cancelled());
    Confirm = Command(() => confirmed());
  }

  public readonly string Title;

  public readonly string Body;

  /// <summary>The change's button, such as "Reset all".</summary>
  public readonly string ConfirmLabel;

  public Command Cancel { get; }

  public Command Confirm { get; }
}

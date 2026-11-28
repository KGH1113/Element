using System;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Confirm;

/// <summary>A question before a change that drops work: what it does, Cancel, and the change.</summary>
internal sealed class ConfirmViewModel : ViewModel
{
  /// <param name="body">What the change does; empty for a question that says it all.</param>
  /// <param name="cancel">The button that changes nothing; null for "Cancel".</param>
  public ConfirmViewModel(
    string title,
    string body,
    string confirm,
    Action cancelled,
    Action confirmed,
    string cancel = null
  )
  {
    Title = title;
    Body = body ?? "";
    ConfirmLabel = confirm;
    CancelLabel = cancel ?? L("취소", "Cancel");
    Cancel = Command(() => cancelled());
    Confirm = Command(() => confirmed());
  }

  public readonly string Title;

  public readonly string Body;

  /// <summary>The change's button, such as "Reset all".</summary>
  public readonly string ConfirmLabel;

  /// <summary>The button that leaves things as they are, focused so Enter is safe.</summary>
  public readonly string CancelLabel;

  public Command Cancel { get; }

  public Command Confirm { get; }
}

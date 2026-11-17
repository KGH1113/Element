using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ElementUI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>The commands of a page's parts: buttons, steps, list rows. Disposed with the page, they stop with it.</summary>
internal sealed class ModCommandsViewModel : ViewModel
{
  /// <summary>A command running <paramref name="run"/>, off while <paramref name="can"/> is false.</summary>
  public Command Make(Action run, Expression<Func<bool>> can = null) =>
    Command(run, can == null ? null : Computed(can));
}

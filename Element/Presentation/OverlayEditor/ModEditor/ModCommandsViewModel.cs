using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ElementUI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>The commands of the column's rows and tools: switches, steps, the back row. Disposed with them, they stop with them.</summary>
internal sealed class ModCommandsViewModel : ViewModel
{
  /// <summary>A command running <paramref name="run"/>, off while <paramref name="can"/> is false.</summary>
  public Command Make(Action run, Expression<Func<bool>> can = null) =>
    Command(run, can == null ? null : Computed(can));
}

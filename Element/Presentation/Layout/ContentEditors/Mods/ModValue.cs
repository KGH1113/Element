using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// One value a part of a mod's <c>EditorPage</c> shows, for its control to
/// <c>Bind</c> to. The page hands the mod the value it starts with; a change
/// the player makes goes to <paramref name="changed"/>, which hands it on to
/// the mod's callback through the editor.
/// </summary>
internal sealed class ModValue<T> : ViewModel
{
  private readonly Action<T> _changed;
  private T _value;

  public ModValue(T value, Action<T> changed)
  {
    _value = value;
    _changed = changed;
  }

  public T Value
  {
    [Pure]
    get => _value;
    set
    {
      if (EqualityComparer<T>.Default.Equals(_value, value))
        return;
      _value = value;
      _changed?.Invoke(value);
    }
  }
}

/// <summary>The commands of a page's parts: buttons, steps, list rows. Disposed with the page, they stop with it.</summary>
internal sealed class ModCommands : ViewModel
{
  /// <summary>A command running <paramref name="run"/>, off while <paramref name="can"/> is false.</summary>
  public Command Make(Action run, Expression<Func<bool>> can = null) =>
    Command(run, can == null ? null : Computed(can));
}

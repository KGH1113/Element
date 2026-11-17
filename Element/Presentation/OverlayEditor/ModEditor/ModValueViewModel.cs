using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ElementUI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// One value a part of a mod's <c>EditorPage</c> shows, for its control to
/// <c>Bind</c> to. The page hands the mod the value it starts with; a change
/// the player makes goes to <paramref name="changed"/>, which hands it on to
/// the mod's callback through the editor.
/// </summary>
internal sealed class ModValueViewModel<T> : ViewModel
{
  private readonly Action<T> _changed;
  private T _value;

  public ModValueViewModel(T value, Action<T> changed)
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

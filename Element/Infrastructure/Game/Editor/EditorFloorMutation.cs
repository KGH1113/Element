using System;
using Element.Domain.Tweaks;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// A floor flip or rotation the editor is making, which the floor's events follow
/// once it finishes. Holds the editor's undo step open until it is disposed.
/// </summary>
internal sealed class EditorFloorMutation : IDisposable
{
  private SaveStateScope _scope;
  internal readonly int Floor;
  internal readonly EditorTransformKind Kind;
  internal readonly float Degrees;

  internal EditorFloorMutation(SaveStateScope scope, int floor, EditorTransformKind kind, float degrees)
  {
    _scope = scope;
    Floor = floor;
    Kind = kind;
    Degrees = degrees;
  }

  public void Dispose()
  {
    _scope?.Dispose();
    _scope = null;
  }
}

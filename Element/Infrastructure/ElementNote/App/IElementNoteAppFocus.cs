using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.App;

/// <summary>Lets the running ElementNote come in front of the game.</summary>
internal interface IElementNoteAppFocus
{
  /// <summary>
  /// Call while the game is in front, right before asking ElementNote for its
  /// window: the OS lets only the app in front hand the front to another app.
  /// </summary>
  void PassFocus();
}

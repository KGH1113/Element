using System;
using System.Reflection;
using ElementKit;

namespace Element.Application.Mods;

/// <summary>One kind of element a mod draws: its class and how players see it.</summary>
internal sealed class ElementClass
{
  public ElementClass(string modId, Type type, ElementAttribute info)
  {
    ModId = modId;
    Type = type;
    Info = info;
  }

  public string ModId { get; }
  public Type Type { get; }
  public ElementAttribute Info { get; }

  /// <summary>What layouts save: <c>ModId/key</c>.</summary>
  public string FullKey => ModId + "/" + Info.Key;

  /// <summary>A new instance, not yet bound; null with <paramref name="error"/> when its constructor failed.</summary>
  public OverlayElement Create(out Exception error)
  {
    error = null;
    try
    {
      return (OverlayElement)Activator.CreateInstance(Type, true);
    }
    catch (Exception exception)
    {
      error = exception is TargetInvocationException { InnerException: not null } wrapped
        ? wrapped.InnerException
        : exception;
      return null;
    }
  }
}

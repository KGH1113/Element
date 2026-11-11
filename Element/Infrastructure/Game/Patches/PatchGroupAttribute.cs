using System;

namespace Element.Infrastructure.Game.Patches;

/// <summary>Names the feature a Harmony patch class belongs to when its namespace is too broad.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class PatchGroupAttribute : Attribute
{
  public PatchGroupAttribute(string name) => Name = name;

  public string Name { get; }
}

using System;

namespace Element.Tests.Support;

/// <summary>
/// Marks a static method without arguments as a test suite. The runner finds
/// it, runs it and prints PASS with <see cref="Description"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class SuiteAttribute : Attribute
{
  public SuiteAttribute(string description) => Description = description;

  public string Description { get; }
}

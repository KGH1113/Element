using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Tests.Support;

namespace Element.Tests;

/// <summary>
/// Runs every [Suite] method in this assembly, ordered by namespace, class and
/// place in the class, and prints PASS with each suite's description. The
/// first failure stops the run.
/// </summary>
internal static class Program
{
  public static int Main()
  {
    try
    {
      int passed = 0;
      foreach ((string description, Action run) in Suites())
      {
        run();
        passed++;
        Console.WriteLine("PASS " + description);
      }
      Console.WriteLine("Element tests: " + passed + " passed");
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
  }

  private static List<(string Description, Action Run)> Suites()
  {
    const BindingFlags Declared =
      BindingFlags.Static
      | BindingFlags.Instance
      | BindingFlags.Public
      | BindingFlags.NonPublic
      | BindingFlags.DeclaredOnly;
    List<(string, Action)> suites = new();
    IEnumerable<MethodInfo> methods = typeof(Program)
      .Assembly.GetTypes()
      .OrderBy(type => type.FullName, StringComparer.Ordinal)
      .SelectMany(type => type.GetMethods(Declared).OrderBy(method => method.MetadataToken));
    foreach (MethodInfo method in methods)
    {
      SuiteAttribute suite = method.GetCustomAttribute<SuiteAttribute>();
      if (suite == null)
        continue;
      if (!method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length > 0)
        throw new InvalidOperationException(method.DeclaringType + "." + method.Name + " must be static void Name()");
      suites.Add((suite.Description, (Action)Delegate.CreateDelegate(typeof(Action), method)));
    }
    return suites;
  }
}

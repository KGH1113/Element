using System;

namespace Element.Tests.Support;

/// <summary>
/// The checks every test makes, imported with <c>using static</c>. A failed
/// check throws with its message, which stops the run and names the suite.
/// </summary>
internal static class Assert
{
  public static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  public static void Equal<T>(T expected, T actual)
  {
    if (!Equals(expected, actual))
      throw new InvalidOperationException("Expected " + expected + ", got " + actual);
  }

  /// <summary>Two numbers that may differ by at most <paramref name="tolerance"/>.</summary>
  public static void Equal(float expected, float actual, float tolerance, string message) =>
    Check(Near(expected, actual, tolerance), message + ": expected " + expected + ", got " + actual);

  /// <summary>Two numbers that may differ by at most <paramref name="tolerance"/>.</summary>
  public static void Equal(double expected, double actual, double tolerance) =>
    Check(Near(expected, actual, tolerance), "Expected " + expected + ", got " + actual);

  public static void Sequence(string[] expected, string[] actual) =>
    Equal(string.Join("|", expected), string.Join("|", actual));

  public static void Throws<T>(Action action, string message)
    where T : Exception
  {
    try
    {
      action();
    }
    catch (T)
    {
      return;
    }
    throw new InvalidOperationException(message + " (expected " + typeof(T).Name + ")");
  }

  public static void Throws<T1, T2>(Action action, string message)
    where T1 : Exception
    where T2 : Exception
  {
    try
    {
      action();
    }
    catch (Exception exception) when (exception is T1 or T2)
    {
      return;
    }
    throw new InvalidOperationException(message + " (expected " + typeof(T1).Name + " or " + typeof(T2).Name + ")");
  }

  /// <summary>Whether two numbers differ by at most <paramref name="tolerance"/>.</summary>
  public static bool Near(float a, float b, float tolerance) => Math.Abs(a - b) <= tolerance;

  /// <summary>Whether two numbers differ by at most <paramref name="tolerance"/>.</summary>
  public static bool Near(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance;
}

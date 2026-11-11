namespace Element.Application.KeyLimiter;

internal static class KeyLimiterKeyBounds
{
  public static bool AnyTooLong(this string[] keys)
  {
    foreach (string key in keys)
      if (key == null || key.Length > 128)
        return true;
    return false;
  }
}

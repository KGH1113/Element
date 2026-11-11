namespace Element.Application.KeyLimiter;

internal interface IKeyLimiterKeyResolver
{
  KeyLimiterKeySelection Resolve(string[] requestedKeys, bool enabled);
}

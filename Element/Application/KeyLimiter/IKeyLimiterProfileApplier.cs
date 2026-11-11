using Element.Domain.KeyLimiter;

namespace Element.Application.KeyLimiter;

internal interface IKeyLimiterProfileApplier
{
  KeyLimiterSnapshot Current { get; }
  KeyLimiterApplyResult Apply(KeyLimiterUpdate update);
  void Clear();
}

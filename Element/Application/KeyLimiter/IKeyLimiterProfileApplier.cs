using Element.Domain.Input;

namespace Element.Application.KeyLimiter;

internal interface IKeyLimiterProfileApplier
{
  KeyLimiterSnapshot Current { get; }
  KeyLimiterApplyResult Apply(KeyLimiterUpdate update);
  void Clear();
}

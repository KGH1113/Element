using Element.Domain.Attempts;

namespace Element.Application.Attempts;

/// <summary>The attempt counts the overlay shows.</summary>
internal interface IAttemptSource
{
  AttemptCounts Read();
}

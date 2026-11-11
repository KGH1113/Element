using Element.Domain.Attempts;

namespace Element.Application.Attempts;

/// <summary>Keeps attempt counts across game launches.</summary>
internal interface IAttemptStore
{
  AttemptBook Load();

  /// <summary>Saves the book as it is now; the write may finish later.</summary>
  void Save(AttemptBook book);

  /// <summary>Waits for saves that are still being written.</summary>
  void Flush();
}

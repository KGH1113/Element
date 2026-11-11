namespace Element.Application.Effects;

/// <summary>Why a name can't be given to an effect preset.</summary>
internal enum PresetNameProblem
{
  None,
  Empty,
  TooLong,
  Taken,
}

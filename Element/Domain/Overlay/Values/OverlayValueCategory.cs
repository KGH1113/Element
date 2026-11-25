namespace Element.Domain.Overlay.Values;

internal enum OverlayValueCategory
{
  Progress,
  Judgement,
  Time,
  Speed,
  Song,
  System,

  // The highlight color, and a color picked by hand. They only go where a
  // color does.
  Color,

  // Game-language labels from texts converted before 1.1. They still render,
  // but aren't offered; editing such a text turns them into plain words.
  Legacy,

  // Values another mod adds through ElementKit. The dock shows them under
  // their group's own name.
  Extension,
}

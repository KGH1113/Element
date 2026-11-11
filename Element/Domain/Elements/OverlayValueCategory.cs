namespace Element.Domain.Elements;

internal enum OverlayValueCategory
{
  Progress,
  Judgement,
  Time,
  Speed,
  Song,
  System,

  // Game-language labels from texts converted before 1.1. They still render,
  // but aren't offered; editing such a text turns them into plain words.
  Legacy,

  // Values another mod adds through ElementKit. The dock shows them under
  // their group's own name.
  Extension,
}

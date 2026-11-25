namespace Element.Domain.Overlay.Values;

internal enum OverlayValueCategory
{
  // Values made on the spot, each with its input method: a number, words,
  // true or false, a condition, a group. Never saved as values: put in a
  // variable, each becomes a piece of its own.
  Make,

  Progress,
  Judgement,
  Time,
  Speed,
  Song,
  System,

  // The highlight color, and a color picked by hand. They only go where a
  // color does.
  Color,

  // The values the player made, shared by every element, and a way to make one.
  Mine,

  // Game-language labels from texts converted before 1.1. They still render,
  // but aren't offered; editing such a text turns them into plain words.
  Legacy,

  // Values another mod adds through ElementKit. The dock shows them under
  // their group's own name.
  Extension,
}

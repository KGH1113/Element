using System;
using Element.Presentation.Overlay.Combo;

namespace Element.Tests;

// The game shows "artist - song" in its legacy UI Text; levels style it, hide
// parts of it and sometimes break it. Cases are taken from real levels.
internal static class SongTitleTests
{
  public static void Run()
  {
    // Sizes are the game's pixels, kept in proportion to its size.
    Equal("<size=150%>large</size> <size=50%>small</size>", Normalize("<size=150>large</size> <size=50>small</size>", 100));
    Equal("<size=145.833333%>scaled</size>", Normalize("<size=70>scaled</size>", 48));
    Equal("<size=60%>percent</size> <size=1.2em>em</size>", Normalize("<size=60%>percent</size> <size=1.2em>em</size>", 100));
    Equal("<size=25%>signed</size>", Normalize("<size=+25>signed</size>", 100));
    Equal("plain", Normalize("plain", 0));

    // Hidden by a negative size, the dash between artist and song stays hidden.
    Equal(
      "<color=#00FFFF>HyuN</color> <color=#AA0000>†</color> <color=#FFFFFF>Triple</color> <size=300%><color=#FF7770>Cross</color></size>",
      Normalize(
        "<color=#00FFFF>HyuN</color> <size=-1> - </size><color=#AA0000>†</color> <color=#FFFFFF>Triple</color> <size=150><color=#FF7770>Cross</color></size>",
        50
      )
    );

    // A see-through artist leaves only the song; trailing spaces go.
    Equal(
      "<size=246%>RUSH E</size>",
      Normalize("<color=#ffffff00>Sheet Music Boss - </color><size=123>RUSH E</size>                  ", 50)
    );

    // A name hidden entirely (size 0, see-through, size -1000) reads as plain text.
    Equal(
      "Plum - N",
      Normalize(
        "<size=0><color=#00000000>Plum</color></size><size=-1000> - </size><b><size=0><color=#00000000>N</color></size></b>",
        50
      )
    );
    Equal("Laur - Gears of Fate", Normalize("<color=#00000000>Laur - Gears of Fate</color>", 50));

    // A seven-digit color is dropped with its closing tag; a stray closing tag too.
    Equal(
      "<b><size=130%><color=#81F7F3>F</color>o<color=#F7FE2E>u</color></size></b>",
      Normalize("</size><b><size=65><color=#81F7F3>F</color><color=#F4FA588>o</color><color=#F7FE2E>u</color></size></b>", 50)
    );
    // A tiny size hides the separator, as the level meant.
    Equal(
      "<size=130%><color=#3104B4>かめりあ</color></size> First",
      Normalize("<size=65><color=#3104B4>かめりあ</color></size> <size= 1> - </size>First", 50)
    );

    // Quoted and named colors, line breaks, and text that only looks like a tag.
    Equal("<color=#999999>grey</color>", Normalize("<color=\"#999999\">grey</color>", 50));
    Equal("<color=#FF0000>red</color>", Normalize("<color=red>red</color>", 50));
    Equal("Feral\n[Hard]", Normalize("Feral\r\n[Hard]", 50));
    Equal("I <noparse><3 you></noparse>", Normalize("I <3 you>", 50));
    Equal("a < b", Normalize("a < b", 50));
  }

  private static string Normalize(string text, int size) => SongTitleMarkupNormalizer.Normalize(text, size);

  private static void Equal(string expected, string actual)
  {
    if (expected != actual)
      throw new InvalidOperationException("Expected '" + expected + "' but got '" + actual + "'");
  }
}

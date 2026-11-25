using System;
using System.Text;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// How a variable is written and saved. One value alone saves as its key, so
/// texts from 1.1 read unchanged; otherwise values go in braces
/// (<c>{combo}*2</c>), words in quotes (<c>"FC"</c>), colors as #RRGGBB,
/// true or false as such, a value the player made as <c>{@key}</c>, a
/// condition as <c>if(left;compare;right;result;default)</c> and a group in
/// parentheses. Pieces side by side are kept apart by a space.
/// </summary>
internal static class OverlayVariableSyntax
{
  private const int MaxDepth = 16;

  public static OverlayVariable Parse(string text)
  {
    if (string.IsNullOrEmpty(text))
      return new OverlayVariable();
    string trimmed = text.Trim();
    if (
      OverlayValueCatalog.TryResolve(trimmed, out OverlayValueDefinition single)
      && single.Id != OverlayValueId.CustomColor
    )
      return new OverlayVariable(OverlayPiece.ValuePiece(single.Id));
    int at = 0;
    OverlayVariable variable = ReadVariable(trimmed, ref at, 0);
    return variable;
  }

  public static string Serialize(OverlayVariable variable)
  {
    if (variable == null)
      return string.Empty;
    if (variable.IsSingleValue(out OverlayValueId only))
      return OverlayValueCatalog.Get(only).Key;
    StringBuilder builder = new();
    Write(builder, variable);
    return builder.ToString();
  }

  /// <summary>Readable form for tooltips, in the caller's words.</summary>
  public static string Describe(
    OverlayVariable variable,
    Func<OverlayValueDefinition, string> name,
    OverlayVariableWords words
  )
  {
    StringBuilder builder = new();
    DescribeInto(builder, variable, name, words ?? OverlayVariableWords.Plain, 0);
    return builder.ToString();
  }

  // ---- Writing ----

  private static void Write(StringBuilder builder, OverlayVariable variable)
  {
    for (int i = 0; i < variable.Count; i++)
    {
      OverlayPiece piece = variable[i];
      // Pieces side by side need a space; a calculation needs none.
      if (i > 0 && piece.Kind != OverlayPieceKind.Operator && variable[i - 1].Kind != OverlayPieceKind.Operator)
        builder.Append(' ');
      switch (piece.Kind)
      {
        case OverlayPieceKind.Number:
          builder.Append(piece.Text);
          break;
        case OverlayPieceKind.Text:
          builder.Append('"');
          foreach (char c in piece.Text)
          {
            if (c == '"' || c == '\\')
              builder.Append('\\');
            builder.Append(c);
          }
          builder.Append('"');
          break;
        case OverlayPieceKind.Color:
          builder.Append(piece.Text);
          break;
        case OverlayPieceKind.Bool:
          builder.Append(piece.Bool ? "true" : "false");
          break;
        case OverlayPieceKind.Compare:
          builder.Append(CompareText(piece.Op));
          break;
        case OverlayPieceKind.Operator:
          builder.Append(piece.Op);
          break;
        case OverlayPieceKind.Value:
          builder.Append('{').Append(OverlayValueCatalog.Get(piece.Value).Key).Append('}');
          break;
        case OverlayPieceKind.Custom:
          builder.Append("{@").Append(piece.Text).Append('}');
          break;
        case OverlayPieceKind.Condition:
          builder.Append("if(");
          for (int s = 0; s < piece.Slots.Length; s++)
          {
            if (s > 0)
              builder.Append(';');
            Write(builder, piece.Slots[s]);
          }
          builder.Append(')');
          break;
        case OverlayPieceKind.Group:
          builder.Append('(');
          Write(builder, piece.Slots[0]);
          builder.Append(')');
          break;
      }
    }
  }

  private static string CompareText(char op) =>
    op switch
    {
      '≤' => "<=",
      '≥' => ">=",
      '≠' => "!=",
      _ => op.ToString(),
    };

  // ---- Reading ----

  // Reads pieces until the end, a ';' or a ')' of the variable around it.
  private static OverlayVariable ReadVariable(string text, ref int at, int depth)
  {
    OverlayVariable variable = new();
    while (at < text.Length && variable.Count < OverlayVariable.MaxPieces)
    {
      char c = text[at];
      if (c == ';' || c == ')')
        break;
      if (char.IsWhiteSpace(c))
      {
        at++;
        continue;
      }
      OverlayPiece last = variable.Last;
      bool operand = last == null || last.Kind == OverlayPieceKind.Operator || last.Kind == OverlayPieceKind.Compare;
      if (c == '{')
      {
        int close = text.IndexOf('}', at + 1);
        if (close < 0)
        {
          at = text.Length;
          break;
        }
        string key = text.Substring(at + 1, close - at - 1);
        at = close + 1;
        if (key.StartsWith("@", StringComparison.Ordinal))
          variable.Add(OverlayPiece.CustomPiece(key.Substring(1)));
        // A built-in value that no longer exists drops out; a mod's value stays while that mod is missing.
        else if (OverlayValueCatalog.TryResolve(key, out OverlayValueDefinition value))
          variable.Add(OverlayPiece.ValuePiece(value.Id));
        continue;
      }
      if (c == '"')
      {
        StringBuilder words = new();
        at++;
        while (at < text.Length && text[at] != '"')
        {
          if (text[at] == '\\' && at + 1 < text.Length)
            at++;
          words.Append(text[at]);
          at++;
        }
        at++;
        variable.Add(OverlayPiece.TextPiece(words.ToString()));
        continue;
      }
      if (c == '#')
      {
        int length = 1;
        while (at + length < text.Length && length < 7 && Uri.IsHexDigit(text[at + length]))
          length++;
        if (length == 7)
          variable.Add(OverlayPiece.ColorPiece(text.Substring(at, 7)));
        at += length;
        continue;
      }
      if (c == '(' && depth < MaxDepth)
      {
        at++;
        OverlayVariable inner = ReadVariable(text, ref at, depth + 1);
        Close(text, ref at);
        variable.Add(OverlayPiece.GroupPiece(inner));
        continue;
      }
      if (Word(text, at, "if(") && depth < MaxDepth)
      {
        at += 3;
        OverlayVariable[] slots = new OverlayVariable[5];
        for (int s = 0; s < slots.Length; s++)
        {
          slots[s] = ReadVariable(text, ref at, depth + 1);
          if (at < text.Length && text[at] == ';')
            at++;
        }
        Close(text, ref at);
        variable.Add(OverlayPiece.ConditionPiece(slots[0], slots[1], slots[2], slots[3], slots[4]));
        continue;
      }
      if (Word(text, at, "true") || Word(text, at, "false"))
      {
        bool yes = text[at] == 't';
        at += yes ? 4 : 5;
        variable.Add(OverlayPiece.BoolPiece(yes));
        continue;
      }
      bool sign = c == '-' && operand;
      if (char.IsDigit(c) || c == '.' || sign)
      {
        int start = at;
        if (sign)
          at++;
        while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '.'))
          at++;
        string number = text.Substring(start, at - start);
        OverlayPiece piece = OverlayPiece.NumberPiece(number);
        if (piece.HasNumber || number == "-")
          variable.Add(piece);
        continue;
      }
      char compare = ReadCompare(text, ref at);
      if (compare != '\0')
      {
        variable.Add(OverlayPiece.ComparePiece(compare));
        continue;
      }
      char op = OverlayVariable.NormalizeOperator(c);
      at++;
      if (op == '\0' || last == null)
        continue;
      if (last.Kind == OverlayPieceKind.Operator)
        variable.Set(variable.Count - 1, OverlayPiece.OperatorPiece(op));
      else
        variable.Add(OverlayPiece.OperatorPiece(op));
    }
    Tidy(variable);
    return variable;
  }

  private static void Close(string text, ref int at)
  {
    while (at < text.Length && char.IsWhiteSpace(text[at]))
      at++;
    if (at < text.Length && text[at] == ')')
      at++;
  }

  private static bool Word(string text, int at, string word) =>
    string.CompareOrdinal(text, at, word, 0, word.Length) == 0
    && (
      word.EndsWith("(", StringComparison.Ordinal)
      || at + word.Length >= text.Length
      || !char.IsLetterOrDigit(text[at + word.Length])
    );

  // A comparison as saved (<, <=, =, !=, <>, >=, >) or as shown (≤ ≠ ≥); '\0' for none.
  private static char ReadCompare(string text, ref int at)
  {
    char c = text[at];
    char after = at + 1 < text.Length ? text[at + 1] : '\0';
    switch (c)
    {
      case '≤':
      case '≥':
      case '≠':
        at++;
        return c;
      case '<':
        at += after == '=' || after == '>' ? 2 : 1;
        return after == '=' ? '≤'
          : after == '>' ? '≠'
          : '<';
      case '>':
        at += after == '=' ? 2 : 1;
        return after == '=' ? '≥' : '>';
      case '!':
        at += after == '=' ? 2 : 1;
        return '≠';
      case '=':
        at += after == '=' ? 2 : 1;
        return '=';
      default:
        return '\0';
    }
  }

  // An operator needs a number on each side: words, colors and the like
  // can't be calculated with, and drop out with the operator that tried.
  private static void Tidy(OverlayVariable variable)
  {
    for (int i = variable.Count - 1; i >= 0; i--)
    {
      if (variable[i].Kind != OverlayPieceKind.Operator)
        continue;
      bool leftOk = i > 0 && Calculable(variable[i - 1]);
      bool rightOk = i + 1 < variable.Count && Calculable(variable[i + 1]);
      if (leftOk && rightOk)
        continue;
      if (!rightOk && i + 1 < variable.Count && variable[i + 1].Kind != OverlayPieceKind.Operator)
        variable.RemoveAt(i + 1);
      variable.RemoveAt(i);
      if (!leftOk && i > 0 && variable[i - 1].Kind != OverlayPieceKind.Operator)
        variable.RemoveAt(i - 1);
      i = Math.Min(i, variable.Count);
    }
    // A leading or trailing operator has nothing to join.
    while (variable.Count > 0 && variable[0].Kind == OverlayPieceKind.Operator)
      variable.RemoveAt(0);
    while (variable.Count > 0 && variable.Last.Kind == OverlayPieceKind.Operator)
      variable.RemoveAt(variable.Count - 1);
  }

  private static bool Calculable(OverlayPiece piece) =>
    piece.Kind switch
    {
      OverlayPieceKind.Number => true,
      OverlayPieceKind.Value => OverlayValueCatalog.Get(piece.Value).IsNumeric,
      OverlayPieceKind.Condition or OverlayPieceKind.Group or OverlayPieceKind.Custom => true,
      _ => false,
    };

  // ---- Reading aloud ----

  private static void DescribeInto(
    StringBuilder builder,
    OverlayVariable variable,
    Func<OverlayValueDefinition, string> name,
    OverlayVariableWords words,
    int depth
  )
  {
    for (int i = 0; i < variable.Count; i++)
    {
      if (builder.Length > 0 && builder[builder.Length - 1] != '(')
        builder.Append(' ');
      OverlayPiece piece = variable[i];
      switch (piece.Kind)
      {
        case OverlayPieceKind.Number:
          builder.Append(piece.Text.Replace('-', '−'));
          break;
        case OverlayPieceKind.Text:
          builder.Append('“').Append(piece.Text).Append('”');
          break;
        case OverlayPieceKind.Color:
          builder.Append(piece.Text);
          break;
        case OverlayPieceKind.Bool:
          builder.Append(piece.Bool ? words.Yes : words.No);
          break;
        case OverlayPieceKind.Compare:
        case OverlayPieceKind.Operator:
          builder.Append(OverlayVariable.Symbol(piece.Op));
          break;
        case OverlayPieceKind.Value:
          builder.Append(name(OverlayValueCatalog.Get(piece.Value)));
          break;
        case OverlayPieceKind.Custom:
          builder.Append('★').Append(OverlayCustomValues.Shown(piece.Text, words));
          break;
        case OverlayPieceKind.Condition when depth < MaxDepth:
          builder.Append(words.ConditionStart).Append(" (");
          DescribeInto(builder, piece.Slots[OverlayPiece.Left], name, words, depth + 1);
          DescribeInto(builder, piece.Slots[OverlayPiece.Compare], name, words, depth + 1);
          DescribeInto(builder, piece.Slots[OverlayPiece.Right], name, words, depth + 1);
          builder.Append(' ').Append(words.Then);
          DescribeInto(builder, piece.Slots[OverlayPiece.Result], name, words, depth + 1);
          if (!piece.Slots[OverlayPiece.Default].IsEmpty)
          {
            builder.Append(' ').Append(words.Otherwise);
            DescribeInto(builder, piece.Slots[OverlayPiece.Default], name, words, depth + 1);
          }
          builder.Append(')');
          break;
        case OverlayPieceKind.Group when depth < MaxDepth:
          builder.Append('(');
          DescribeInto(builder, piece.Slots[0], name, words, depth + 1);
          builder.Append(')');
          break;
      }
    }
  }
}

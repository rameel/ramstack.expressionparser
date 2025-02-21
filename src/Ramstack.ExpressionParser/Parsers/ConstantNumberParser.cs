namespace Ramstack.Parsing.Parsers;

/// <summary>
/// Represents a parser for numeric literals following C# syntax rules.
/// </summary>
/// <remarks>
/// This parser only matches the number itself, since the unary sign
/// is intercepted by the parser responsible for unary operations.
/// </remarks>
internal sealed class ConstantNumberParser : Parser<object>
{
    /// <summary>
    /// Gets an instance of the numeric literal parser.
    /// </summary>
    /// <remarks>
    /// This parser only matches the number itself, since the unary sign
    /// is intercepted by the parser responsible for unary operations.
    /// </remarks>
    public static Parser<object> NumericLiteral { get; } = new ConstantNumberParser();

    /// <summary>
    /// A lookup table for converting hexadecimal characters to their corresponding
    /// integer values. Each entry in the table corresponds to a character code,
    /// and the value indicates the integer value of the hexadecimal digit (0-15).
    /// A value of <c>0xFF</c> indicates that the character is not a valid hexadecimal digit.
    /// </summary>
    private static ReadOnlySpan<byte> HexTable =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0x0A, 0x0B, 0x0C, 0xD,  0x0E, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    ];

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, [NotNullWhen(true)] out object? value)
    {
        var s = context.Remaining;
        if (s.Length == 0)
            goto FAIL;

        var count = TryParseHexInteger(s, out var number);
        if (count == 0)
        {
            count = TryParseBinInteger(s, out number);
            if (count == 0)
            {
                count = TryParseInteger(s, out number);
                if (count == 0)
                    goto FAIL;

                var p = TryParseReal(s, count, out var real);
                if (p != 0)
                {
                    value = real!;
                    context.Advance(p);
                    return true;
                }
            }
        }

        count += AdjustNumericType(s[count..], number, out value);
        context.Advance(count);
        return true;

        FAIL:
        value = null;
        return false;
    }

    private static int TryParseInteger(ReadOnlySpan<char> s, out ulong value)
    {
        if (s.Length != 0)
        {
            var p = 0;
            var r = 0ul;

            for (; (uint)p < (uint)s.Length; p++)
            {
                var d = (uint)s[p] - '0';
                if (d > 9)
                {
                    if (p == 0)
                    {
                        goto FAIL;
                    }

                    break;
                }

                //
                // Check for overflow
                //
                if (r > ulong.MaxValue / 10
                    || (r == ulong.MaxValue / 10 && d > ulong.MaxValue % 10))
                    goto FAIL;

                r = r * 10 + d;
            }

            value = r;
            return p;
        }

        FAIL:
        value = 0;
        return 0;
    }

    private static int TryParseHexInteger(ReadOnlySpan<char> s, out ulong value)
    {
        if (s.StartsWith("0x") || s.StartsWith("0X"))
        {
            var p = 2;
            var r = 0ul;

            for (; (uint)p < (uint)s.Length; p++)
            {
                var c = s[p];
                var d = c < HexTable.Length ? (uint)HexTable[c] : 0xFF;

                if (d == 0xFF)
                {
                    if (p == 2)
                    {
                        goto FAIL;
                    }

                    break;
                }

                //
                // Check for overflow
                //
                if (r > ulong.MaxValue / 16)
                    goto FAIL;

                r = r * 16 + d;
            }

            value = r;
            return p;
        }

        FAIL:
        value = 0;
        return 0;
    }

    private static int TryParseBinInteger(ReadOnlySpan<char> s, out ulong value)
    {
        if (s.StartsWith("0b") || s.StartsWith("0B"))
        {
            var p = 2;
            var r = 0ul;

            for (; (uint)p < (uint)s.Length; p++)
            {
                var d = (uint)s[p] - '0';
                if (d > 1)
                {
                    if (p == 2)
                    {
                        goto FAIL;
                    }

                    break;
                }

                //
                // Check for overflow
                //
                if (r > ulong.MaxValue >> 1)
                    goto FAIL;

                r = (r << 1) + d;
            }

            value = r;
            return p;
        }

        FAIL:
        value = 0;
        return 0;
    }

    private static int TryParseReal(ReadOnlySpan<char> s, int p, out object? value)
    {
        //
        // Float number pattern: \d+(\.\d+)?([Ee][-+]?\d+)?
        //

        var i = p;

        if ((uint)i < (uint)s.Length && s[i] == '.')
        {
            var index = i + 1;

            if ((uint)index < (uint)s.Length && IsAsciiDigit(s[index]))
            {
                index++;
                while ((uint)index < (uint)s.Length && IsAsciiDigit(s[index]))
                    index++;

                i = index;
            }
        }

        if ((uint)i < (uint)s.Length && (s[i] & ~0x20) == 'E')
        {
            var index = i + 1;

            if ((uint)index < (uint)s.Length)
            {
                // c == '+' || c == '-'
                if ((s[index] - '+' & -3) == 0)
                    index++;

                if ((uint)index < (uint)s.Length && IsAsciiDigit(s[index]))
                {
                    index++;
                    while ((uint)index < (uint)s.Length && IsAsciiDigit(s[index]))
                        index++;

                    i = index;
                }
            }
        }

        if (i != p)
        {
            if ((uint)i <= (uint)s.Length)
            {
                var suffix = (uint)i < (uint)s.Length ? s[i] & ~0x20 : '\0';
                s = s[..i];

                switch (suffix)
                {
                    case 'F':
                        i++;
                        value = ParseSingle(s);
                        return i;

                    case 'M':
                        i++;
                        value = ParseDecimal(s);
                        return i;

                    case 'D':
                        i++;
                        goto default;

                    default:
                        value = ParseDouble(s);
                        return i;
                }
            }
        }

        value = null;
        return 0;

        static bool IsAsciiDigit(char c) =>
            (uint)c - '0' <= 9;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static object ParseSingle(ReadOnlySpan<char> s) =>
            float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static object ParseDouble(ReadOnlySpan<char> s) =>
            double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static object ParseDecimal(ReadOnlySpan<char> s) =>
            decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int AdjustNumericType(ReadOnlySpan<char> s, ulong number, out object value)
    {
        var hasL = false;
        var hasU = false;

        if (s.Length != 0)
        {
            var c0 = s[0] & ~0x20;

            if (c0 == 'D')
            {
                value = (double)number;
                return 1;
            }

            if (c0 == 'F')
            {
                value = (float)number;
                return 1;
            }

            if (c0 == 'M')
            {
                value = new decimal(number);
                return 1;
            }

            if (c0 is 'L' or 'U')
            {
                var c1 = s.Length > 1 ? s[1] & ~0x20 : '\0';
                switch ((c0, c1))
                {
                    case ('L', 'U'):
                    case ('U', 'L'):
                        hasL = true;
                        hasU = true;
                        break;

                    case ('L', _):
                        hasL = true;
                        break;

                    case ('U', _):
                        hasU = true;
                        break;
                }
            }
        }

        //
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#6453-integer-literals
        //
        // 6.4.5.3 Integer literals
        //
        // The type of an integer literal is determined as follows:
        //
        // * If the literal has no suffix, it has the first of these types in which its value can be represented: int, uint, long, ulong.
        // * If the literal is suffixed by U or u, it has the first of these types in which its value can be represented: uint, ulong.
        // * If the literal is suffixed by L or l, it has the first of these types in which its value can be represented: long, ulong.
        // * If the literal is suffixed by UL, Ul, uL, ul, LU, Lu, lU, or lu, it is of type ulong.
        //

        //
        // Note: The following part of the spec is not implemented here. It is implemented in the unary minus analysis.
        // The current parser matches only the number itself, since the unary sign (in this case, minus) is intercepted
        // by the parser responsible for unary operations.
        //
        // * When an integer-literal representing the value 2147483648 (2**31) and no integer-type-suffix appears
        //   as the token immediately following a unary minus operator token (§12.9.3), the result (of both tokens)
        //   is a constant of type int with the value −2147483648 (−2**31). In all other situations,
        //   such an integer-literal is of type uint.
        //
        // * When an integer-literal representing the value 9223372036854775808 (2**63) and no integer-type-suffix
        //   or the integer-type-suffix L or l appears as the token immediately following a unary minus operator token (§12.9.3),
        //   the result (of both tokens) is a constant of type long with the value −9223372036854775808 (−2**63).
        //   In all other situations, such an integer-literal is of type ulong.
        //

        //
        // If the literal has no suffix, it has the first of these types
        // in which its value can be represented: int, uint, long, ulong.
        //
        if (!hasL && !hasU)
        {
            switch (number)
            {
                case <= int.MaxValue:
                    value = (int)number;
                    break;

                case <= uint.MaxValue:
                    value = (uint)number;
                    break;

                case <= long.MaxValue:
                    value = (long)number;
                    break;

                default:
                    value = number;
                    break;
            }

            return 0;
        }

        //
        // If the literal is suffixed by U or u, it has the first of these types
        // in which its value can be represented: uint, ulong.
        //
        if (hasU && !hasL)
        {
            if (number <= uint.MaxValue)
                value = (uint)number;
            else
                value = number;

            return 1;
        }

        //
        // If the literal is suffixed by L or l, it has the first of these types
        // in which its value can be represented: long, ulong.
        //
        if (hasL && !hasU)
        {
            switch (number)
            {
                case <= long.MaxValue:
                    value = (long)number;
                    break;

                default:
                    value = number;
                    break;
            }

            return 1;
        }

        //
        // If the literal is suffixed by UL, Ul, uL, ul, LU, Lu, lU, or lu, it is of type ulong.
        //
        value = number;
        return 2;
    }
}

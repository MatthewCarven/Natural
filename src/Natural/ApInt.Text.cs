using System.Text;

namespace Natural;

public readonly partial struct ApInt
{
    // ------------------------------------------------------------------
    // Formatting
    // ------------------------------------------------------------------

    public override string ToString() => ToString("D");

    /// <summary>
    /// "D" (default) for decimal, "X" / "x" for hexadecimal, "B" for binary.
    /// Hex and binary are sign + magnitude with no prefix: -255 is "-FF".
    /// </summary>
    public string ToString(string? format)
    {
        string digits = format switch
        {
            null or "" or "D" or "d" => ToDecimalDigits(Mag),
            "X" => ToRadixDigits(Mag, 4, "0123456789ABCDEF"),
            "x" => ToRadixDigits(Mag, 4, "0123456789abcdef"),
            "B" or "b" => ToRadixDigits(Mag, 1, "01"),
            _ => throw new FormatException($"Unknown format \"{format}\". Use D, X, x or B."),
        };
        return _negative ? "-" + digits : digits;
    }

    /// <summary>Hex and binary: every digit is just a group of bits, read straight off the limbs.</summary>
    private static string ToRadixDigits(uint[] mag, int bitsPerDigit, string alphabet)
    {
        long bits = Magnitude.BitLength(mag);
        if (bits == 0) return "0";

        int count = (int)((bits + bitsPerDigit - 1) / bitsPerDigit);
        uint mask = (1u << bitsPerDigit) - 1;
        var chars = new char[count];
        for (int d = 0; d < count; d++)
        {
            long bit = (long)d * bitsPerDigit;          // 1 and 4 both divide 32, so a digit never straddles limbs
            uint value = (mag[bit >> 5] >> (int)(bit & 31)) & mask;
            chars[count - 1 - d] = alphabet[(int)value];
        }
        return new string(chars);
    }

    /// <summary>
    /// Binary to decimal by "double dabble" (shift-and-add-3), the way hardware
    /// does it with no divider: shift the number's bits, top first, into a BCD
    /// register one at a time; before each shift, add 3 to every BCD digit that is
    /// 5 or more, so that doubling it carries correctly into the next decimal digit.
    ///
    /// The BCD register is packed eight digits to a uint, and the add-3 step does
    /// all eight nibbles at once with a mask: a digit d is 5 or more exactly when
    /// d + 3 sets its nibble's top bit.
    /// </summary>
    private static string ToDecimalDigits(uint[] mag)
    {
        long bits = Magnitude.BitLength(mag);
        if (bits == 0) return "0";

        // Decimal digits needed: bits * log10(2), where 1233 / 4096 ~ log10(2); +2 for slack.
        long maxDigits = ((bits * 1233) >> 12) + 2;
        var bcd = new uint[(maxDigits + 7) / 8 + 1];
        int used = 1;   // BCD words that can be non-zero so far

        for (long i = bits - 1; i >= 0; i--)
        {
            // Add 3 to every nibble >= 5. Nibbles are <= 9 here, so +3 never carries
            // between nibbles and the plain adds below can't spill.
            for (int w = 0; w < used; w++)
            {
                uint x = bcd[w];
                uint fives = (x + 0x33333333u) & 0x88888888u;   // top bit of each nibble that was >= 5
                bcd[w] = x + ((fives >> 2) | (fives >> 3));    // 8 -> 0b0011 in the same nibble
            }

            // Shift the whole register left one bit, feeding in the next bit of the number.
            uint carry = (mag[i >> 5] >> (int)(i & 31)) & 1;
            for (int w = 0; w < used; w++)
            {
                uint x = bcd[w];
                bcd[w] = (x << 1) | carry;
                carry = x >> 31;
            }
            if (carry != 0) bcd[used++] = carry;
        }

        var sb = new StringBuilder((int)Math.Min(maxDigits, int.MaxValue));
        bool leading = true;
        for (int w = used - 1; w >= 0; w--)
        {
            for (int nibble = 7; nibble >= 0; nibble--)
            {
                uint digit = (bcd[w] >> (nibble << 2)) & 0xF;
                if (leading && digit == 0) continue;
                leading = false;
                sb.Append((char)('0' | digit));   // '0' is 0x30, so OR-ing in 0..9 gives the digit
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses an optional sign followed by decimal digits, or "0x" + hex digits,
    /// or "0b" + binary digits. Underscores between digits are allowed ("1_000_000").
    /// </summary>
    public static ApInt Parse(string s) =>
        TryParse(s, out ApInt result) ? result : throw new FormatException($"Not an integer: \"{s}\".");

    public static bool TryParse(string? s, out ApInt result)
    {
        result = Zero;
        if (s is null) return false;

        ReadOnlySpan<char> span = s.AsSpan().Trim();
        bool negative = false;
        if (span.Length > 0 && (span[0] == '-' || span[0] == '+'))
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        uint[]? mag;
        if (span.Length > 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
            mag = ParsePowerOfTwoRadix(span[2..], 4);
        else if (span.Length > 2 && span[0] == '0' && (span[1] == 'b' || span[1] == 'B'))
            mag = ParsePowerOfTwoRadix(span[2..], 1);
        else
            mag = ParseDecimal(span);

        if (mag is null) return false;
        result = new ApInt(mag, negative);
        return true;
    }

    /// <summary>
    /// Hex or binary: each digit is a fixed group of bits, so drop them straight into
    /// the limbs, working from the least significant (rightmost) digit.
    /// </summary>
    private static uint[]? ParsePowerOfTwoRadix(ReadOnlySpan<char> digits, int bitsPerDigit)
    {
        if (!ValidDigitRun(digits)) return null;

        var mag = new uint[(digits.Length * bitsPerDigit + 31) / 32];
        long bit = 0;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            if (digits[i] == '_') continue;
            int value = HexValue(digits[i]);
            if (value < 0 || value >> bitsPerDigit != 0) return null;
            mag[bit >> 5] |= (uint)value << (int)(bit & 31);
            bit += bitsPerDigit;
        }
        return Magnitude.Trim(mag);
    }

    /// <summary>
    /// Decimal: for each digit, acc = acc * 10 + digit, where acc * 10 is worked out
    /// as (acc &lt;&lt; 3) + (acc &lt;&lt; 1) through the bitwise adder, in place.
    /// </summary>
    private static uint[]? ParseDecimal(ReadOnlySpan<char> digits)
    {
        if (!ValidDigitRun(digits)) return null;

        // Each decimal digit is log2(10) ~ 3.32 bits; 10/3 per digit overestimates safely.
        var acc = new uint[(long)digits.Length * 10 / 3 / 32 + 2];
        int used = 0;

        foreach (char c in digits)
        {
            if (c == '_') continue;
            if (c < '0' || c > '9') return null;
            used = TimesTenPlus(acc, used, (uint)(c & 0xF));   // '0'..'9' are 0x30..0x39: the low nibble is the digit
        }
        return Magnitude.Trim(acc[..used]);
    }

    /// <summary>acc[0..used) = acc * 10 + digit, in place; returns the new used length.</summary>
    private static int TimesTenPlus(uint[] acc, int used, uint digit)
    {
        // acc * 8 + acc * 2, one limb at a time. The bits each shift pushes out of a
        // limb (prev >> 29 and prev >> 31) come in at the bottom of the next one.
        // acc * 10 < acc * 16, so the product needs at most one extra limb.
        uint prev = 0, carry = 0;
        for (int i = 0; i <= used; i++)
        {
            uint x = i < used ? acc[i] : 0;
            uint times8 = (x << 3) | (prev >> 29);
            uint times2 = (x << 1) | (prev >> 31);
            acc[i] = Magnitude.FullAdd(times8, times2, ref carry);
            prev = x;
        }

        // Then ripple the digit in from the bottom.
        carry = 0;
        acc[0] = Magnitude.FullAdd(acc[0], digit, ref carry);
        for (int i = 1; carry != 0; i++)
            acc[i] = Magnitude.FullAdd(acc[i], 0, ref carry);

        return acc[used] != 0 ? used + 1 : used;
    }

    /// <summary>Non-empty, and underscores only between digits.</summary>
    private static bool ValidDigitRun(ReadOnlySpan<char> digits) =>
        digits.Length > 0 && digits[0] != '_' && digits[^1] != '_' && !digits.Contains("__", StringComparison.Ordinal);

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}

using System.Buffers;
using System.Runtime.CompilerServices;
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
    /// Decimal digits to reserve for a magnitude of <paramref name="bits"/> bits, which
    /// is bits × log10(2). The multiplier must over-estimate: 4096 × log10(2) is
    /// 1233.0188..., so 1233 is its floor and falls short by 4.6e-6 of a digit per bit.
    /// That shortfall passes unnoticed until it outgrows the slack -- at 1,955,154 bits
    /// it does, and then the BCD register below is a word too short, which is a write
    /// past the end rather than a slow path. 1234 over-estimates instead.
    /// </summary>
    internal static long DecimalDigitsForBits(long bits) => ((bits * 1234) >> 12) + 2;

    /// <summary>Decimal digits: double dabble, below, run to the end.</summary>
    private static string ToDecimalDigits(uint[] mag)
    {
        var result = new StrongBox<string>();
        foreach (long _ in DecimalStringSteps(mag, result)) { }
        return result.Value!;
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
    ///
    /// As steps a job can pause between: a bit costs a limb step per BCD word in use for
    /// each of the two passes, plus one, and the steps yield those counts after every
    /// <see cref="DigitStepUnits"/> or so (a step ends on a bit). Reading the digits out at
    /// the end costs one more per word. The string is in <paramref name="result"/> when the
    /// steps end.
    /// </summary>
    internal static IEnumerable<long> DecimalStringSteps(uint[] mag, StrongBox<string> result)
    {
        long bits = Magnitude.BitLength(mag);
        if (bits == 0)
        {
            result.Value = "0";
            yield break;
        }

        var bcd = new uint[(DecimalDigitsForBits(bits) + 7) / 8 + 1];
        int used = 1;   // BCD words that can be non-zero so far
        long next = bits - 1, pending = 0;
        while (next >= 0)
        {
            used = Dabble(mag, bcd, used, ref next, ref pending);
            if (pending >= DigitStepUnits)
            {
                yield return pending;
                pending = 0;
            }
        }

        var sb = new StringBuilder((int)Math.Min(DecimalDigitsForBits(bits), int.MaxValue));
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
        result.Value = sb.ToString();
        yield return pending + used;
    }

    /// <summary>
    /// Double dabble's work: shifts bits next, next - 1, ... into the register until a step's
    /// worth of limb steps is pending or the bits run out, and returns the words now in use.
    /// Out of the iterator, so its loops run on locals: in the iterator they'd be fields, and
    /// F0 of 1e30000 took half as long again (300 ms, not 200).
    /// </summary>
    private static int Dabble(uint[] mag, uint[] bcd, int used, ref long next, ref long pending)
    {
        long i = next, units = pending;
        for (; i >= 0 && units < DigitStepUnits; i--)
        {
            units += 2L * used + 1;

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
        (next, pending) = (i, units);
        return used;
    }

    /// <summary>
    /// What <see cref="DecimalStringSteps"/> will count for n bits, near enough: the words in
    /// use grow by log10(2)/8 a bit, so the sum is about n²·log10(2)/8 + 3n.
    /// </summary>
    internal static long DecimalStringUnits(long n) =>
        (long)Int128.Min((Int128)n * n * 3763 / 100_000 + 3 * (Int128)n, long.MaxValue / 4);

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

    /// <summary>Decimal: the digit loop below, run to the end.</summary>
    private static uint[]? ParseDecimal(ReadOnlySpan<char> digits)
    {
        if (!ValidDigitRun(digits) || digits.ContainsAnyExcept(DecimalDigitsAndUnderscore)) return null;
        var result = new StrongBox<uint[]>();
        foreach (long _ in DecimalDigitSteps(digits.ToString(), result)) { }
        return result.Value;
    }

    private static readonly SearchValues<char> DecimalDigitsAndUnderscore = SearchValues.Create("0123456789_");

    /// <summary>
    /// The decimal loops both ways (digits in, double dabble out) yield after at least this
    /// many limb steps (a step ends on a digit or a bit). About 0.065 ms of work here:
    /// fine-grained enough for a time budget, and coarse enough that the yields cost nothing
    /// measurable.
    /// </summary>
    internal const int DigitStepUnits = 4096;

    /// <summary>
    /// Decimal digits to a magnitude, as steps a job can pause between: for each digit,
    /// acc = acc * 10 + digit, where acc * 10 is worked out as (acc &lt;&lt; 3) + (acc &lt;&lt; 1)
    /// through the bitwise adder, in place. A digit costs one limb step per limb of acc, plus
    /// two; the steps yield those counts. <paramref name="digits"/> holds only decimal digits
    /// and underscores, at least one digit. The magnitude is in <paramref name="result"/> when
    /// the steps end.
    /// </summary>
    internal static IEnumerable<long> DecimalDigitSteps(string digits, StrongBox<uint[]> result)
    {
        // Each decimal digit is log2(10) ~ 3.32 bits; 10/3 per digit overestimates safely.
        var acc = new uint[(long)digits.Length * 10 / 3 / 32 + 2];
        int used = 0;
        long pending = 0;

        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c == '_') continue;
            pending += used + 2;
            used = TimesTenPlus(acc, used, (uint)(c & 0xF));   // '0'..'9' are 0x30..0x39: the low nibble is the digit
            if (pending >= DigitStepUnits)
            {
                yield return pending;
                pending = 0;
            }
        }
        result.Value = Magnitude.Trim(acc[..used]);
        if (pending > 0) yield return pending;
    }

    /// <summary>
    /// What <see cref="DecimalDigitSteps"/> will count for n digits, near enough: the limbs
    /// grow by log2(10)/32 a digit, so the sum is about n²·log2(10)/64 + 2.5n.
    /// </summary>
    internal static long DecimalDigitUnits(long n) => (long)((Int128)n * n * 3322 / 64000) + 5 * n / 2;

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

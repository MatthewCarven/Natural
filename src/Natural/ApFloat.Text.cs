using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Natural;

// Text. Decimal output starts from the exact value, which is always a finite decimal
// (m × 2^-k = m × 5^k / 10^k), and rounds once, through RoundExact, in any mode.
// Decimal input builds the exact rational digits × 10^exp and rounds that once too.
public readonly partial struct ApFloat : ISpanFormattable, IParsable<ApFloat>
{
    // ------------------------------------------------------------------
    // Formatting
    // ------------------------------------------------------------------

    /// <summary>
    /// Every digit the precision carries, zeros kept, in scientific notation: 0.1 at 53 bits
    /// is "1.0000000000000001E-001". Values of one precision all print the same width, so
    /// a right-aligned column lines up on the point and the E, and the three-digit
    /// exponent reads like a log-scale bar. The same as
    /// <c>ToString("E" + (DecimalDigitsFor(Precision) - 1))</c>, in the current culture.
    /// </summary>
    public override string ToString() => ToString(null, null, RoundingMode.ToNearestEven);

    public string ToString(string? format) => ToString(format, null, RoundingMode.ToNearestEven);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ToString(format, formatProvider, RoundingMode.ToNearestEven);

    /// <summary>
    /// Formats the value, rounding correctly from its exact value in the given mode (an exact
    /// tie goes to the even digit when rounding to nearest, as everywhere in IEEE 754).
    /// <list type="bullet">
    /// <item>null or "": the default, as <see cref="ToString()"/>.</item>
    /// <item>"E&lt;n&gt;" / "e&lt;n&gt;": scientific, n digits after the point (default 6), the
    /// exponent signed and at least three digits wide: "3.3333E-001".</item>
    /// <item>"F&lt;n&gt;": fixed point, n decimals (default the culture's, 2), zeros kept so points line up.</item>
    /// <item>"R", "G": the shortest text that reads back as this value at its precision:
    /// plain from 1E-005 up to the precision's digit count, scientific outside that.</item>
    /// <item>"G&lt;n&gt;": n significant digits, trailing zeros dropped, laid out the same way.</item>
    /// <item>"B" / "B&lt;n&gt;": the binary point view, every bit or n bits after the point:
    /// 5.25 is "101.01". A column of these reads like a bar graph of powers of two.</item>
    /// <item>"A" / "X" (and lower case): hexadecimal floating point as C's printf("%a")
    /// writes it: 0.1 at 53 bits is "0x1.999999999999ap-4"; "a3" rounds to 3 hex digits.</item>
    /// <item>Anything else is a custom pattern of 0 (a digit, zero-padded), # (a digit if
    /// needed), "." and "," (grouping), with literal text before or after: "00000.0000",
    /// "#,##0.00", "'x = '0.###".</item>
    /// </list>
    /// Hex and binary always use "." for the point; the rest use the culture's separator and signs.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider, RoundingMode mode)
    {
        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(formatProvider);
        if (IsNaN) return nfi.NaNSymbol;
        if (IsInfinity) return _negative ? nfi.NegativeInfinitySymbol : nfi.PositiveInfinitySymbol;
        if (string.IsNullOrEmpty(format)) return Scientific(DecimalDigitsFor(Precision) - 1, 'E', nfi, mode);

        if (!TryStandardFormat(format, out char letter, out int? n)) return Custom(format, nfi, mode);
        return letter switch
        {
            'E' or 'e' => Scientific(n ?? 6, letter, nfi, mode),
            'F' or 'f' => Fixed(n ?? nfi.NumberDecimalDigits, nfi, mode),
            'G' or 'g' when n is null or 0 => Shortest(letter == 'g' ? 'e' : 'E', nfi),
            'G' or 'g' => General(n!.Value, letter == 'g' ? 'e' : 'E', nfi, mode),
            'R' or 'r' => Shortest('E', nfi),
            'B' or 'b' => BinaryPoint(n, mode),
            'A' or 'X' => Hex(n, upper: true, mode),
            'a' or 'x' => Hex(n, upper: false, mode),
            _ => throw new FormatException($"Unknown format \"{format}\". Use E, F, G, R, B, A or X, or a custom pattern."),
        };
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        string text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        if (text.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        text.CopyTo(destination);
        charsWritten = text.Length;
        return true;
    }

    /// <summary>A letter and up to nine digits, as .NET's standard numeric formats are.</summary>
    private static bool TryStandardFormat(string format, out char letter, out int? n)
    {
        letter = format[0];
        n = null;
        if (!char.IsAsciiLetter(letter) || format.Length > 10) return false;
        for (int i = 1; i < format.Length; i++)
            if (!char.IsAsciiDigit(format[i])) return false;
        if (format.Length > 1) n = int.Parse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }

    // ------------------------------------------------------------------
    // Decimal digits
    // ------------------------------------------------------------------

    /// <summary>
    /// The significant decimal digits that tell every value of a precision apart, so that
    /// text of that many digits always reads back exactly: floor(p · log10 2) + 2, the
    /// smallest N with 10^(N-1) &gt; 2^p. 17 for 53 bits, 9 for 24, 79 for 256.
    /// </summary>
    public static int DecimalDigitsFor(int precision)
    {
        CheckPrecision(precision);
        return (int)FloorLog10Of2Times(precision) + 2;
    }

    // log10(2) × 2^128, rounded down: 128 bits of it, so there's no floating-point log anywhere.
    private static readonly ApInt Log10Of2 = ApInt.Parse("0x4D104D427DE7FBCC47C4ACD605BE48BC");

    /// <summary>floor(n · log10 2), for any n (ApInt's &gt;&gt; floors, so negatives work too).</summary>
    private static long FloorLog10Of2Times(long n) => (long)(((ApInt)n * Log10Of2) >> 128);

    // 5^n for n below this are kept as they're made: text asks for the same few hundred
    // over and over (a double's decimal exponents run to about ±340). Never mutated.
    private const int CachedFives = 4096;
    private static uint[][] _fives = [[1]];
    private static readonly Lock FivesLock = new();

    /// <summary>5^n: from the cache, or by repeated squaring past it.</summary>
    private static uint[] PowerOfFive(long n)
    {
        if (n < CachedFives)
        {
            uint[][] fives = Volatile.Read(ref _fives);
            if (n < fives.Length) return fives[n];
            lock (FivesLock)
            {
                fives = _fives;
                if (n >= fives.Length)
                {
                    // Grow to at least n, doubling, each power five times the last: 4x + x.
                    var grown = new uint[Math.Min(CachedFives, Math.Max(n + 1, 2L * fives.Length))][];
                    Array.Copy(fives, grown, fives.Length);
                    for (int i = fives.Length; i < grown.Length; i++)
                        grown[i] = Magnitude.Add(Magnitude.ShiftLeft(grown[i - 1], 2), grown[i - 1]);
                    Volatile.Write(ref _fives, grown);
                    fives = grown;
                }
                return fives[n];
            }
        }

        uint[] result = [1], square = [5];
        for (; n > 0; n >>= 1)
        {
            if ((n & 1) != 0) result = Magnitude.Multiply(result, square);
            if (n > 1) square = Magnitude.Multiply(square, square);
        }
        return result;
    }

    /// <summary>
    /// |this| × 10^t, rounded to an integer in the given mode (the directed modes look at
    /// this value's sign). Finite, non-zero values only. The exact value is
    /// m × 5^t × 2^(e + t): a product when t &gt;= 0, and a quotient by 5^-t otherwise,
    /// taken with bits to spare below the point and its remainder as the sticky bit.
    /// </summary>
    private uint[] ScaledToInteger(long t, RoundingMode mode)
    {
        long e = checked(_exp + t);
        ApFloat r;
        if (t >= 0)
        {
            r = RoundExact(_negative, Magnitude.Multiply(Mant, PowerOfFive(t)), e, false, MaxPrecision, mode, minExp: 0);
        }
        else
        {
            // Enough extra bits that the quotient has two below the point and isn't zero.
            uint[] five = PowerOfFive(-t);
            long shift = Math.Max(0, Math.Max(e + 2, Magnitude.BitLength(five) - Magnitude.BitLength(Mant) + 1));
            uint[] q = Magnitude.DivRem(Magnitude.ShiftLeft(Mant, shift), five, out uint[] rem);
            r = RoundExact(_negative, q, checked(e - shift), rem.Length != 0, MaxPrecision, mode, minExp: 0);
        }
        return r.IsZero ? Magnitude.Empty : Magnitude.ShiftLeft(r.Mant, r._exp);
    }

    /// <summary>floor(log10 |this|), exactly. Finite, non-zero values only.</summary>
    private long DecimalExponent()
    {
        // 2^Top <= |v| < 2^(Top+1), so floor(Top · log10 2) is the answer or one short of it.
        long x = FloorLog10Of2Times(Top);
        while (true)
        {
            uint[] lead = ScaledToInteger(-x, RoundingMode.TowardZero);    // floor(|v| / 10^x)
            if (lead.Length == 0) x--;
            else if (Magnitude.Compare(lead, [10]) >= 0) x++;
            else return x;
        }
    }

    /// <summary>|this| rounded to k significant digits: exactly k digits, and the power of ten of the first.</summary>
    private (string Digits, long Exponent) SignificantDigits(int k, RoundingMode mode)
    {
        long x = DecimalExponent();
        string digits = Decimal(ScaledToInteger(checked(k - 1 - x), mode));
        if (digits.Length > k)
        {
            // 9.99... rounded up to 10.0...: one power of ten more, and the extra digit is a 0.
            digits = digits[..k];
            x++;
        }
        return (digits, x);
    }

    private static string Decimal(uint[] mag) => ApInt.FromLimbs(mag, false).ToString();

    // ------------------------------------------------------------------
    // The formats
    // ------------------------------------------------------------------

    private string Scientific(int decimals, char e, NumberFormatInfo nfi, RoundingMode mode)
    {
        var (digits, x) = IsZero ? (new string('0', decimals + 1), 0L) : SignificantDigits(checked(decimals + 1), mode);
        var sb = new StringBuilder();
        if (_negative) sb.Append(nfi.NegativeSign);
        sb.Append(digits[0]);
        if (decimals > 0) sb.Append(nfi.NumberDecimalSeparator).Append(digits, 1, decimals);
        return AppendExponent(sb, e, x, nfi).ToString();
    }

    /// <summary>E, a sign, and at least three digits: E+000, E-017, E+1234.</summary>
    private static StringBuilder AppendExponent(StringBuilder sb, char e, long x, NumberFormatInfo nfi) =>
        sb.Append(e).Append(x < 0 ? nfi.NegativeSign : nfi.PositiveSign)
          .Append(Math.Abs(x).ToString("000", CultureInfo.InvariantCulture));

    private string Fixed(int decimals, NumberFormatInfo nfi, RoundingMode mode)
    {
        string digits = IsZero ? "0" : Decimal(ScaledToInteger(decimals, mode));
        digits = digits.PadLeft(decimals + 1, '0');                  // at least one digit before the point
        var sb = new StringBuilder();
        if (_negative) sb.Append(nfi.NegativeSign);
        sb.Append(digits, 0, digits.Length - decimals);
        if (decimals > 0) sb.Append(nfi.NumberDecimalSeparator).Append(digits, digits.Length - decimals, decimals);
        return sb.ToString();
    }

    private string General(int k, char e, NumberFormatInfo nfi, RoundingMode mode)
    {
        if (IsZero) return (_negative ? nfi.NegativeSign : "") + "0";
        var (digits, x) = SignificantDigits(k, mode);
        return Layout(digits.TrimEnd('0'), x, k, e, nfi);
    }

    /// <summary>
    /// The fewest digits that read back as this value at its precision (rounding to
    /// nearest). If k digits can, so can k + 1: a k-digit decimal is a (k+1)-digit one too,
    /// and the (k+1)-digit neighbour of v on its side lies between it and v. So a binary
    /// search over k finds the fewest; DecimalDigitsFor(Precision) digits always work.
    /// </summary>
    private string Shortest(char e, NumberFormatInfo nfi)
    {
        if (IsZero) return (_negative ? nfi.NegativeSign : "") + "0";
        int most = DecimalDigitsFor(Precision);
        long x = DecimalExponent();
        (uint[] Q, long Scale) best = ReadsBackWith(most, x) ?? throw new InvalidOperationException("No round trip at full width.");
        for (int lo = 1, hi = most; lo < hi;)
        {
            int mid = (lo + hi) >> 1;
            var candidate = ReadsBackWith(mid, x);
            if (candidate is { } c) { best = c; hi = mid; }
            else lo = mid + 1;
        }
        string digits = Decimal(best.Q);
        return Layout(digits.TrimEnd('0'), best.Scale + digits.Length - 1, most, e, nfi);
    }

    /// <summary>
    /// A k-digit decimal q × 10^scale that reads back as this value, or null. The two
    /// candidates are the k-digit neighbours either side of v; if both read back, the
    /// nearer one (a tie going to the even digit).
    /// </summary>
    private (uint[] Q, long Scale)? ReadsBackWith(int k, long x)
    {
        long t = k - 1 - x;
        uint[] below = ScaledToInteger(t, RoundingMode.TowardZero);
        uint[] nearest = ScaledToInteger(t, RoundingMode.ToNearestEven);
        uint[] above = Magnitude.Add(below, [1]);
        uint[] other = Magnitude.Compare(nearest, below) == 0 ? above : below;
        foreach (uint[] q in new[] { nearest, other })
            if (CompareValues(FromDecimal(_negative, q, -t, Precision, RoundingMode.ToNearestEven), this) == 0)
                return (q, -t);
        return null;
    }

    /// <summary>
    /// G layout for significant digits d (no trailing zeros) whose first digit is worth
    /// 10^x: plain from 1E-005 up to 10^widest, scientific outside that.
    /// </summary>
    private string Layout(string d, long x, int widest, char e, NumberFormatInfo nfi)
    {
        var sb = new StringBuilder();
        if (_negative) sb.Append(nfi.NegativeSign);
        if (x < -5 || x >= widest)
        {
            sb.Append(d[0]);
            if (d.Length > 1) sb.Append(nfi.NumberDecimalSeparator).Append(d, 1, d.Length - 1);
            return AppendExponent(sb, e, x, nfi).ToString();
        }
        if (x < 0) return sb.Append('0').Append(nfi.NumberDecimalSeparator).Append('0', (int)(-x - 1)).Append(d).ToString();
        int whole = (int)x + 1;
        if (d.Length <= whole) return sb.Append(d).Append('0', whole - d.Length).ToString();
        return sb.Append(d, 0, whole).Append(nfi.NumberDecimalSeparator).Append(d, whole, d.Length - whole).ToString();
    }

    /// <summary>
    /// Binary positional notation: every bit, or rounded to n bits after the point. The
    /// integer part's length is the power of two, so a column of these is a bar graph.
    /// </summary>
    private string BinaryPoint(int? n, RoundingMode mode)
    {
        uint[] mag = Magnitude.Empty;
        long exp = 0;
        if (!IsZero && n is null) (mag, exp) = (Mant, _exp);
        else if (!IsZero)
        {
            ApFloat r = RoundExact(_negative, Mant, _exp, false, MaxPrecision, mode, minExp: -n!.Value);
            if (!r.IsZero) (mag, exp) = (r.Mant, r._exp);
        }
        long fractionBits = n ?? Math.Max(0, -exp);
        string bits = ApInt.FromLimbs(Magnitude.ShiftLeft(mag, checked(exp + fractionBits)), false).ToString("B");
        bits = bits.PadLeft(checked((int)fractionBits + 1), '0');
        int whole = bits.Length - (int)fractionBits;
        string sign = _negative ? "-" : "";
        return fractionBits == 0 ? sign + bits : $"{sign}{bits[..whole]}.{bits[whole..]}";
    }

    /// <summary>
    /// Hexadecimal floating point, as C's printf("%a") writes it: the leading 1 alone before
    /// the point, 0.1 is "0x1.999999999999ap-4". Exact, or rounded to n hex digits.
    /// </summary>
    private string Hex(int? n, bool upper, RoundingMode mode)
    {
        string sign = _negative ? "-" : "", prefix = upper ? "0X" : "0x";
        char p = upper ? 'P' : 'p';
        if (IsZero) return $"{sign}{prefix}0{(n > 0 ? "." + new string('0', n.Value) : "")}{p}+0";

        ApFloat v = n is int digits ? RoundExact(_negative, Mant, _exp, false, checked(1 + 4 * digits), mode) : this;
        long fractionBits = Magnitude.BitLength(v.Mant) - 1;
        long hexDigits = n ?? (fractionBits + 3) >> 2;
        // Pad the fraction bits out to whole hex digits: then the bit count is 1 + 4k, and the
        // hex string starts with the leading 1.
        string hex = ApInt.FromLimbs(Magnitude.ShiftLeft(v.Mant, 4 * hexDigits - fractionBits), false).ToString(upper ? "X" : "x");
        string body = hexDigits > 0 ? $"1.{hex[1..]}" : "1";
        return $"{sign}{prefix}{body}{p}{v.Top.ToString("+0;-0", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// A custom pattern: 0 and # digit placeholders, one ".", "," for grouping, and literal
    /// text (plain, quoted, or after a backslash) before or after the number. Sections (;),
    /// percent and exponents aren't supported and throw, rather than printing something wrong.
    /// </summary>
    private string Custom(string format, NumberFormatInfo nfi, RoundingMode mode)
    {
        var prefix = new StringBuilder();
        var suffix = new StringBuilder();
        int wholePlaces = 0, firstWholeZero = -1, fractionPlaces = 0, lastFractionZero = 0;
        bool point = false, grouping = false, inNumber = false, afterNumber = false;

        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (c is '0' or '#' or '.' or ',')
            {
                if (afterNumber) throw new FormatException($"\"{format}\": text between digit placeholders isn't supported.");
                inNumber = true;
                if (c == '.')
                {
                    if (point) throw new FormatException($"\"{format}\" has two decimal points.");
                    point = true;
                }
                else if (c == ',') grouping |= !point;
                else if (point)
                {
                    fractionPlaces++;
                    if (c == '0') lastFractionZero = fractionPlaces;
                }
                else
                {
                    if (c == '0' && firstWholeZero < 0) firstWholeZero = wholePlaces;
                    wholePlaces++;
                }
                continue;
            }

            string literal;
            if (c is '\'' or '"')
            {
                int end = format.IndexOf(c, i + 1);
                if (end < 0) throw new FormatException($"\"{format}\" has an unclosed quote.");
                literal = format[(i + 1)..end];
                i = end;
            }
            else if (c == '\\')
            {
                if (++i == format.Length) throw new FormatException($"\"{format}\" ends with a backslash.");
                literal = format[i].ToString();
            }
            else if (c is ';' or '%' or '‰' || (c is 'E' or 'e' && i + 1 < format.Length && format[i + 1] is '+' or '-' or '0'))
                throw new FormatException($"\"{format}\": sections, percent and exponents aren't supported in custom patterns. Use E<n> for scientific.");
            else literal = c.ToString();

            if (inNumber) afterNumber = true;
            (afterNumber ? suffix : prefix).Append(literal);
        }

        // Round to every fraction place, then drop trailing zeros back to the last 0 placeholder.
        string digits = IsZero ? "0" : Decimal(ScaledToInteger(fractionPlaces, mode));
        digits = digits.PadLeft(fractionPlaces + 1, '0');
        string whole = digits[..^fractionPlaces].TrimStart('0');
        string fraction = digits[^fractionPlaces..];
        int keep = fraction.Length;
        while (keep > lastFractionZero && fraction[keep - 1] == '0') keep--;
        fraction = fraction[..keep];

        int minWhole = firstWholeZero < 0 ? 0 : wholePlaces - firstWholeZero;
        whole = whole.PadLeft(minWhole, '0');
        if (whole.Length == 0 && fraction.Length == 0) whole = "0";
        if (grouping) whole = Group(whole, nfi);

        var sb = new StringBuilder();
        if (_negative) sb.Append(nfi.NegativeSign);
        sb.Append(prefix);
        if (inNumber)
        {
            sb.Append(whole);
            if (fraction.Length > 0) sb.Append(nfi.NumberDecimalSeparator).Append(fraction);
        }
        return sb.Append(suffix).ToString();
    }

    private static string Group(string whole, NumberFormatInfo nfi)
    {
        int size = nfi.NumberGroupSizes.Length > 0 && nfi.NumberGroupSizes[0] > 0 ? nfi.NumberGroupSizes[0] : 3;
        var sb = new StringBuilder();
        for (int i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % size == 0) sb.Append(nfi.NumberGroupSeparator);
            sb.Append(whole[i]);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    /// <summary>Parses at <see cref="DefaultPrecision"/>, rounding to nearest, in the current culture. See the full overload.</summary>
    public static ApFloat Parse(string s) => Parse(s, DefaultPrecision);

    public static ApFloat Parse(string s, IFormatProvider? provider) =>
        Parse(s, DefaultPrecision, RoundingMode.ToNearestEven, provider);

    /// <summary>
    /// Parses a number and rounds it once, correctly, to <paramref name="precision"/> bits.
    /// Accepts an optional sign, then: decimal ("1.5", ".5", "1e-7", "1_000.25"); hexadecimal
    /// floating point ("0x1.8p+1", C's syntax); binary ("0b101.01p-3"); NaN; Infinity (or inf, ∞).
    /// Hex and binary use "." for the point; decimal uses the culture's separator.
    /// </summary>
    public static ApFloat Parse(string s, int precision, RoundingMode mode = RoundingMode.ToNearestEven, IFormatProvider? provider = null) =>
        TryParse(s, precision, mode, provider, out ApFloat result) ? result : throw new FormatException($"Not a number: \"{s}\".");

    public static bool TryParse([NotNullWhen(true)] string? s, out ApFloat result) =>
        TryParse(s, DefaultPrecision, RoundingMode.ToNearestEven, null, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ApFloat result) =>
        TryParse(s, DefaultPrecision, RoundingMode.ToNearestEven, provider, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, int precision, RoundingMode mode, IFormatProvider? provider, out ApFloat result)
    {
        CheckPrecision(precision);
        result = default;
        if (s is null) return false;
        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<char> span = s.AsSpan().Trim();

        bool negative = false;
        if (span.Length > 0 && span[0] is '-' or '+')
        {
            negative = span[0] == '-';
            span = span[1..];
        }
        else if (nfi.NegativeSign.Length > 0 && span.StartsWith(nfi.NegativeSign, StringComparison.Ordinal))
        {
            negative = true;
            span = span[nfi.NegativeSign.Length..];
        }
        else if (nfi.PositiveSign.Length > 0 && span.StartsWith(nfi.PositiveSign, StringComparison.Ordinal))
            span = span[nfi.PositiveSign.Length..];

        if (IsWord(span, nfi.NaNSymbol, "NaN"))
        {
            result = NaNOf(precision);
            return true;
        }
        if (IsWord(span, nfi.PositiveInfinitySymbol, "Infinity", "inf", "∞"))
        {
            result = InfinityOf(negative, precision);
            return true;
        }

        try
        {
            if (span.Length > 2 && span[0] == '0' && span[1] is 'x' or 'X')
                return TryParsePowerOfTwoRadix(span[2..], 4, negative, precision, mode, out result);
            if (span.Length > 2 && span[0] == '0' && span[1] is 'b' or 'B')
                return TryParsePowerOfTwoRadix(span[2..], 1, negative, precision, mode, out result);
            return TryParseDecimal(span, negative, precision, mode, nfi, out result);
        }
        catch (OverflowException)
        {
            return false;   // an exponent beyond a long
        }
    }

    private static bool IsWord(ReadOnlySpan<char> span, params string[] words)
    {
        foreach (string word in words)
            if (word.Length > 0 && span.Equals(word, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>digits [separator digits] [e [sign] digits]: the exact rational digits × 10^exp, rounded once.</summary>
    private static bool TryParseDecimal(ReadOnlySpan<char> span, bool negative, int precision, RoundingMode mode,
                                        NumberFormatInfo nfi, out ApFloat result)
    {
        result = default;
        int e = span.IndexOfAny('e', 'E');
        ReadOnlySpan<char> mantissa = e < 0 ? span : span[..e];
        long exponent = 0;
        if (e >= 0 && !TryParseExponent(span[(e + 1)..], out exponent)) return false;

        string separator = nfi.NumberDecimalSeparator;
        int point = mantissa.IndexOf(separator, StringComparison.Ordinal);
        ReadOnlySpan<char> whole = point < 0 ? mantissa : mantissa[..point];
        ReadOnlySpan<char> fraction = point < 0 ? [] : mantissa[(point + separator.Length)..];
        if (whole.Length + fraction.Length == 0) return false;
        if (!DigitRun(whole, 10, allowEmpty: true) || !DigitRun(fraction, 10, allowEmpty: true)) return false;

        string digits = string.Concat(whole, fraction);
        ApInt d = ApInt.Parse(digits.Length == 0 ? "0" : digits);
        int fractionDigits = fraction.Length - fraction.Count('_');
        result = FromDecimal(negative, d.Limbs, checked(exponent - fractionDigits), precision, mode);
        return true;
    }

    /// <summary>Hex or binary digits [. digits] [p [sign] decimal digits]: exact, then rounded to the precision.</summary>
    private static bool TryParsePowerOfTwoRadix(ReadOnlySpan<char> span, int bitsPerDigit, bool negative, int precision,
                                                RoundingMode mode, out ApFloat result)
    {
        result = default;
        int p = span.IndexOfAny('p', 'P');
        ReadOnlySpan<char> mantissa = p < 0 ? span : span[..p];
        long exponent = 0;
        if (p >= 0 && !TryParseExponent(span[(p + 1)..], out exponent)) return false;

        int point = mantissa.IndexOf('.');
        ReadOnlySpan<char> whole = point < 0 ? mantissa : mantissa[..point];
        ReadOnlySpan<char> fraction = point < 0 ? [] : mantissa[(point + 1)..];
        if (whole.Length + fraction.Length == 0) return false;
        int radix = 1 << bitsPerDigit;
        if (!DigitRun(whole, radix, allowEmpty: true) || !DigitRun(fraction, radix, allowEmpty: true)) return false;

        string digits = string.Concat(whole, fraction);
        ApInt m = digits.Length == 0 ? ApInt.Zero : ApInt.Parse((bitsPerDigit == 4 ? "0x" : "0b") + digits);
        long exp = checked(exponent - (long)bitsPerDigit * (fraction.Length - fraction.Count('_')));
        result = m.IsZero ? ZeroOf(negative, precision) : new ApFloat(negative ? -m : m, exp, precision, mode);
        return true;
    }

    /// <summary>Digits of the radix, with underscores allowed only between two digits.</summary>
    private static bool DigitRun(ReadOnlySpan<char> run, int radix, bool allowEmpty)
    {
        if (run.Length == 0) return allowEmpty;
        for (int i = 0; i < run.Length; i++)
        {
            if (run[i] == '_')
            {
                if (i == 0 || i == run.Length - 1 || run[i - 1] == '_') return false;
                continue;
            }
            int value = run[i] switch
            {
                >= '0' and <= '9' => run[i] - '0',
                >= 'a' and <= 'f' => run[i] - 'a' + 10,
                >= 'A' and <= 'F' => run[i] - 'A' + 10,
                _ => 99,
            };
            if (value >= radix) return false;
        }
        return true;
    }

    private static bool TryParseExponent(ReadOnlySpan<char> text, out long exponent) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent);

    /// <summary>
    /// ±q × 10^s, correctly rounded: the exact rational, rounded once. For s &gt;= 0 that's
    /// the integer q × 5^s × 2^s; below, it's q × 2^s / 5^-s, divided as Divide does it
    /// (enough quotient bits for the rounding, and the remainder as the sticky bit).
    /// </summary>
    private static ApFloat FromDecimal(bool negative, uint[] q, long s, int precision, RoundingMode mode)
    {
        if (q.Length == 0) return ZeroOf(negative, precision);
        if (s >= 0) return RoundExact(negative, Magnitude.Multiply(q, PowerOfFive(s)), s, false, precision, mode);
        uint[] five = PowerOfFive(-s);
        long shift = Math.Max(0, precision + 2 + Magnitude.BitLength(five) - Magnitude.BitLength(q));
        uint[] quotient = Magnitude.DivRem(Magnitude.ShiftLeft(q, shift), five, out uint[] remainder);
        return RoundExact(negative, quotient, checked(s - shift), remainder.Length != 0, precision, mode);
    }
}

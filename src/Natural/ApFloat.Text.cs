using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    /// Long decimal output costs the square of its length (F0 of 1e100000 takes seconds);
    /// <see cref="StartFormat"/> does the same work a slice at a time.
    /// </summary>
    /// <exception cref="FormatException">An unknown format or a malformed custom pattern.</exception>
    /// <exception cref="OverflowException">F or a custom pattern on a value with more digits
    /// before the point than a string can hold.</exception>
    public string ToString(string? format, IFormatProvider? formatProvider, RoundingMode mode) =>
        new DecimalFormat(this, format, NumberFormatInfo.GetInstance(formatProvider), mode, ForcedRoute).RunToEnd();

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
    /// |v| × 10^t rounded to an integer, as engine steps: the first round has the result's bits
    /// and the guard bits. Apart from DecimalFormat.Scaled, so the closures are only made when
    /// the engine runs.
    /// </summary>
    private static IEnumerable<long> ScaledToIntegerSteps(ApFloat v, long t, long startBits, RoundingMode mode,
                                                          Route route, Pinning pinning) =>
        TimesPowerOfTenSteps(v.Mant, v._exp, t, startBits,
            (mag, exp) => RoundExact(v._negative, mag, exp, false, MaxPrecision, mode, minExp: 0),
            () => v.ScaledToIntegerExact(t, mode), route, pinning);

    /// <summary>
    /// |this| × 10^t rounded to an integer, the exact way (DecimalFormat.Scaled takes the
    /// interval engine first, past the cache). The value is m × 5^t × 2^(e + t): a product when
    /// t &gt;= 0, and a quotient by 5^-t otherwise, taken with bits to spare below the point
    /// and its remainder as the sticky bit. The result is an integer (exponent &gt;= 0), or zero.
    /// </summary>
    private ApFloat ScaledToIntegerExact(long t, RoundingMode mode)
    {
        long e = checked(_exp + t);
        if (t >= 0)
            return RoundExact(_negative, Magnitude.Multiply(Mant, PowerOfFive(t)), e, false, MaxPrecision, mode, minExp: 0);

        // Enough extra bits that the quotient has two below the point and isn't zero.
        uint[] five = PowerOfFive(-t);
        long shift = Math.Max(0, Math.Max(e + 2, Magnitude.BitLength(five) - Magnitude.BitLength(Mant) + 1));
        uint[] q = Magnitude.DivRem(Magnitude.ShiftLeft(Mant, shift), five, out uint[] rem);
        return RoundExact(_negative, q, checked(e - shift), rem.Length != 0, MaxPrecision, mode, minExp: 0);
    }

    // ------------------------------------------------------------------
    // The formats
    // ------------------------------------------------------------------

    /// <summary>E, a sign, and at least three digits: E+000, E-017, E+1234.</summary>
    private static StringBuilder AppendExponent(StringBuilder sb, char e, long x, NumberFormatInfo nfi) =>
        sb.Append(e).Append(x < 0 ? nfi.NegativeSign : nfi.PositiveSign)
          .Append(Math.Abs(x).ToString("000", CultureInfo.InvariantCulture));

    /// <summary>
    /// G layout for significant digits d (no trailing zeros) whose first digit is worth
    /// 10^x: plain from 1E-005 up to 10^widest, scientific outside that.
    /// </summary>
    private static string Layout(bool negative, string d, long x, int widest, char e, NumberFormatInfo nfi)
    {
        var sb = new StringBuilder();
        if (negative) sb.Append(nfi.NegativeSign);
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
    private sealed record CustomPattern(string Prefix, string Suffix, int WholePlaces, int FirstWholeZero,
                                        int FractionPlaces, int LastFractionZero, bool Grouping, bool InNumber)
    {
        public static CustomPattern Parse(string format)
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
            return new(prefix.ToString(), suffix.ToString(), wholePlaces, firstWholeZero, fractionPlaces, lastFractionZero, grouping, inNumber);
        }
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

    /// <summary>
    /// One format's work, as steps a job can pause between: decimal scalings (|x| × 10^t to an
    /// integer, by the interval engine or the exact route), read-backs (for R), and digit
    /// strings (double dabble). ToString runs it to the end and StartFormat a slice at a time,
    /// so the two can't disagree. The format, and the output's length, are checked when it's made.
    /// Each stage leaves its answer in a field for the stage that called it, rather than in a
    /// box of its own: a short ToString would pay for every box.
    /// </summary>
    private sealed class DecimalFormat
    {
        private enum Kind { Text, Scientific, Fixed, General, Shortest, Binary, Hex, Custom }

        /// <summary>A k-digit decimal q × 10^Scale that reads back, and the scaling that made it.</summary>
        private sealed record Candidate(uint[] Q, long Scale, Pinning Pinning);

        private readonly ApFloat _x;
        private readonly NumberFormatInfo _nfi;
        private readonly RoundingMode _mode;
        private readonly Route _route;       // the test hook, read by the caller: steps may run on another thread
        private readonly Kind _kind;
        private readonly int _n;             // the number after the letter, defaults filled in
        private readonly int? _bits;         // B's and A/X's number, which may be absent
        private readonly char _e;            // 'E' or 'e' in the exponent; for hex, 'A'/'X' or 'a'/'x'
        private readonly CustomPattern? _pattern;
        private Pinning? _last;              // the last scaling or read-back: for the test hooks
        private Pinning? _printed;           // the scaling that made the printed digits: for the certificate
        private long _printedScale;
        private long _later;                 // estimated units of the printed digits' double dabble, until it starts

        // The stages' answers.
        private uint[] _scaled = Magnitude.Empty;
        private readonly StrongBox<string> _digits = new();
        private long _exponent;
        private (string Digits, long Exponent) _significant;
        private Candidate? _candidate;

        public DecimalFormat(ApFloat x, string? format, NumberFormatInfo nfi, RoundingMode mode, Route route)
        {
            (_x, _nfi, _mode, _route) = (x, nfi, mode, route);
            if (x.IsNaN || x.IsInfinity)
            {
                _kind = Kind.Text;
                Text = x.IsNaN ? nfi.NaNSymbol : x._negative ? nfi.NegativeInfinitySymbol : nfi.PositiveInfinitySymbol;
                return;
            }

            if (string.IsNullOrEmpty(format)) (_kind, _n, _e) = (Kind.Scientific, DecimalDigitsFor(x.Precision) - 1, 'E');
            else if (!TryStandardFormat(format, out char letter, out int? n)) (_kind, _pattern) = (Kind.Custom, CustomPattern.Parse(format));
            else
            {
                (_kind, _n, _e) = letter switch
                {
                    'E' or 'e' => (Kind.Scientific, n ?? 6, letter),
                    'F' or 'f' => (Kind.Fixed, n ?? nfi.NumberDecimalDigits, 'E'),
                    'G' or 'g' when n is null or 0 => (Kind.Shortest, 0, letter == 'g' ? 'e' : 'E'),
                    'G' or 'g' => (Kind.General, n!.Value, letter == 'g' ? 'e' : 'E'),
                    'R' or 'r' => (Kind.Shortest, 0, 'E'),
                    'B' or 'b' => (Kind.Binary, 0, 'E'),
                    'A' or 'X' or 'a' or 'x' => (Kind.Hex, 0, letter),
                    _ => throw new FormatException($"Unknown format \"{format}\". Use E, F, G, R, B, A or X, or a custom pattern."),
                };
                _bits = n;
            }

            // Plan the printed digits: they're the long part, and F's can't outgrow a string.
            if (x.IsZero) return;
            switch (_kind)
            {
                case Kind.Scientific: _later = ApInt.DecimalStringUnits(BitsForDigits(_n + 1L)); break;
                case Kind.General: _later = ApInt.DecimalStringUnits(BitsForDigits(_n)); break;
                case Kind.Shortest: _later = ApInt.DecimalStringUnits(x.Precision + 7); break;
                case Kind.Fixed: PlanFixed(_n); break;
                case Kind.Custom when _pattern!.InNumber: PlanFixed(_pattern.FractionPlaces); break;
            }
            ExpectedWork = _later;
        }

        private static long BitsForDigits(long digits) => digits * 3322 / 1000 + 1;   // log2(10) is 3.3219...

        private void PlanFixed(int places)
        {
            long top = _x.Top;
            long whole = top < 0 ? 1 : (long)((Int128)top * 1233 / 4096) + 1;   // the digits before the point, or one fewer
            if (whole > Array.MaxLength - places - 2L)
                throw new OverflowException($"This value has about {whole:N0} digits before the point: more than a string can hold.");
            _later = ApInt.DecimalStringUnits(Math.Max(top + 1, 0) + BitsForDigits(places));
        }

        public string? Text { get; private set; }

        public long WorkDone { get; private set; }

        /// <summary>An estimate of the whole: what's done, what the current stage still needs, and the printed digits.</summary>
        public long ExpectedWork { get; private set; }

        /// <summary>The steps. B, A and X are linear, so they're done here, with no steps.</summary>
        public IEnumerable<long> Steps()
        {
            switch (_kind)
            {
                case Kind.Binary:
                    Text = _x.BinaryPoint(_bits, _mode);
                    return [];
                case Kind.Hex:
                    Text = _x.Hex(_bits, _e is 'A' or 'X', _mode);
                    return [];
                case Kind.Scientific: return Scientific(_n);
                case Kind.Fixed: return Fixed(_n);
                case Kind.General: return General(_n);
                case Kind.Shortest: return Shortest();
                case Kind.Custom: return Custom(_pattern!);
                default: return [];
            }
        }

        /// <summary>All the steps, on this thread, leaving the last scaling's rounds and route in the test hooks.</summary>
        public string RunToEnd()
        {
            try
            {
                foreach (long _ in Steps()) { }
            }
            finally
            {
                if (_last is not null)
                {
                    LastRounds = _last.Rounds;
                    LastExact = _last.Exact;
                }
            }
            return Text!;
        }

        /// <summary>How the printed digits were pinned: Low ≤ x × 10^Scale ≤ High, signed.</summary>
        public DecimalCertificate Certificate()
        {
            if (_printed is null) return new DecimalCertificate(0, 0, true, null, null);
            ApFloat? low = _printed.Low, high = _printed.High;
            if (_x._negative && low is { } l && high is { } h) (low, high) = (-h, -l);
            return new DecimalCertificate(_printed.Rounds, _printed.WorkingBits, _printed.Exact, low, high, _printedScale);
        }

        // --- the stages: each yields from the ones below it ---

        /// <summary>|x| × 10^t rounded to an integer in the mode (which looks at x's sign), into _scaled.</summary>
        private IEnumerable<long> Scaled(long t, RoundingMode mode, Pinning pinning)
        {
            ApFloat v = _x;
            // The result has at most Top + 2 + t + floor(t · log2 5) bits: work with that many and a guard.
            long resultBits = Math.Max(1, checked(v.Top + 2 + t + FloorLog2Of5Times(t)));
            long startBits = checked(resultBits + GuardBits);
            _last = pinning;
            long first = FirstStepUnits(Magnitude.BitLength(v.Mant), t, startBits, _route);
            ExpectedWork = WorkDone + first + _later;
            if (ExactFromTheStart(t, _route))
            {
                // The engine's one step, taken here: a short ToString would pay for its iterator and closures.
                pinning.Exact = true;
                pinning.Result = v.ScaledToIntegerExact(t, mode);
                WorkDone += first;
                ExpectedWork = WorkDone + _later;
                yield return first;
            }
            else
            {
                foreach (long units in ScaledToIntegerSteps(v, t, startBits, mode, _route, pinning))
                {
                    WorkDone += units;
                    ExpectedWork = WorkDone + pinning.NextUnits + _later;
                    yield return units;
                }
            }
            ApFloat r = pinning.Result;
            _scaled = r.IsZero ? Magnitude.Empty : Magnitude.ShiftLeft(r.Mant, r._exp);
        }

        /// <summary>q × 10^s read back at x's precision, rounding to nearest, into pinning.Result.</summary>
        private IEnumerable<long> ReadBack(uint[] q, long s, Pinning pinning)
        {
            _last = pinning;
            long startBits = (long)_x.Precision + GuardBits;
            long first = FirstStepUnits(Magnitude.BitLength(q), s, startBits, _route);
            ExpectedWork = WorkDone + first + _later;
            if (ExactFromTheStart(s, _route))
            {
                // As in Scaled: the one step, without the iterator.
                pinning.Exact = true;
                pinning.Result = FromDecimalExact(_x._negative, q, s, _x.Precision, RoundingMode.ToNearestEven);
                WorkDone += first;
                ExpectedWork = WorkDone + _later;
                yield return first;
                yield break;
            }
            foreach (long units in FromDecimalSteps(_x._negative, q, s, _x.Precision, RoundingMode.ToNearestEven, _route, pinning))
            {
                WorkDone += units;
                ExpectedWork = WorkDone + pinning.NextUnits + _later;
                yield return units;
            }
        }

        /// <summary>The digits of a magnitude, by double dabble, into _digits.</summary>
        private IEnumerable<long> Digits(uint[] mag)
        {
            long total = ApInt.DecimalStringUnits(Magnitude.BitLength(mag)), done = 0;
            _later = 0;
            ExpectedWork = WorkDone + total;
            foreach (long units in ApInt.DecimalStringSteps(mag, _digits))
            {
                WorkDone += units;
                done += units;
                ExpectedWork = WorkDone + Math.Max(0, total - done);
                yield return units;
            }
        }

        /// <summary>floor(log10 |x|), exactly, into _exponent. Finite, non-zero values only.</summary>
        private IEnumerable<long> DecimalExponent()
        {
            // 2^Top <= |x| < 2^(Top+1), so floor(Top · log10 2) is the answer or one short of it.
            long x = FloorLog10Of2Times(_x.Top);
            while (true)
            {
                foreach (long units in Scaled(-x, RoundingMode.TowardZero, new Pinning())) yield return units;   // floor(|x| / 10^x)
                if (_scaled.Length == 0) x--;
                else if (Magnitude.Compare(_scaled, [10]) >= 0) x++;
                else break;
            }
            _exponent = x;
        }

        /// <summary>
        /// |x| rounded to k significant digits, into _significant: exactly k digits, and the
        /// power of ten of the first.
        /// </summary>
        private IEnumerable<long> SignificantDigits(int k)
        {
            foreach (long units in DecimalExponent()) yield return units;
            long x = _exponent;
            long t = checked(k - 1 - x);
            var pinning = new Pinning();
            foreach (long units in Scaled(t, _mode, pinning)) yield return units;
            (_printed, _printedScale) = (pinning, t);
            foreach (long units in Digits(_scaled)) yield return units;
            string digits = _digits.Value!;
            if (digits.Length > k)
            {
                // 9.99... rounded up to 10.0...: one power of ten more, and the extra digit is a 0.
                digits = digits[..k];
                x++;
            }
            _significant = (digits, x);
        }

        private IEnumerable<long> Scientific(int decimals)
        {
            string digits;
            long x = 0;
            if (_x.IsZero) digits = new string('0', decimals + 1);
            else
            {
                foreach (long units in SignificantDigits(checked(decimals + 1))) yield return units;
                (digits, x) = _significant;
            }
            var sb = new StringBuilder();
            if (_x._negative) sb.Append(_nfi.NegativeSign);
            sb.Append(digits[0]);
            if (decimals > 0) sb.Append(_nfi.NumberDecimalSeparator).Append(digits, 1, decimals);
            Text = AppendExponent(sb, _e, x, _nfi).ToString();
        }

        private IEnumerable<long> Fixed(int decimals)
        {
            string digits = "0";
            if (!_x.IsZero)
            {
                var pinning = new Pinning();
                foreach (long units in Scaled(decimals, _mode, pinning)) yield return units;
                (_printed, _printedScale) = (pinning, decimals);
                foreach (long units in Digits(_scaled)) yield return units;
                digits = _digits.Value!;
            }
            digits = digits.PadLeft(decimals + 1, '0');                  // at least one digit before the point
            var sb = new StringBuilder();
            if (_x._negative) sb.Append(_nfi.NegativeSign);
            sb.Append(digits, 0, digits.Length - decimals);
            if (decimals > 0) sb.Append(_nfi.NumberDecimalSeparator).Append(digits, digits.Length - decimals, decimals);
            Text = sb.ToString();
        }

        private IEnumerable<long> General(int k)
        {
            if (_x.IsZero)
            {
                Text = (_x._negative ? _nfi.NegativeSign : "") + "0";
                yield break;
            }
            foreach (long units in SignificantDigits(k)) yield return units;
            var (digits, x) = _significant;
            Text = Layout(_x._negative, digits.TrimEnd('0'), x, k, _e, _nfi);
        }

        /// <summary>
        /// The fewest digits that read back as x at its precision (rounding to nearest). If k
        /// digits can, so can k + 1: a k-digit decimal is a (k+1)-digit one too, and the
        /// (k+1)-digit neighbour of x on its side lies between it and x. So a binary search over
        /// k finds the fewest; DecimalDigitsFor(Precision) digits always work.
        /// </summary>
        private IEnumerable<long> Shortest()
        {
            if (_x.IsZero)
            {
                Text = (_x._negative ? _nfi.NegativeSign : "") + "0";
                yield break;
            }
            int most = DecimalDigitsFor(_x.Precision);
            foreach (long units in DecimalExponent()) yield return units;
            long x = _exponent;

            foreach (long units in ReadsBackWith(most, x)) yield return units;
            Candidate best = _candidate ?? throw new InvalidOperationException("No round trip at full width.");
            for (int lo = 1, hi = most; lo < hi;)
            {
                int mid = (lo + hi) >> 1;
                foreach (long units in ReadsBackWith(mid, x)) yield return units;
                if (_candidate is { } c)
                {
                    best = c;
                    hi = mid;
                }
                else lo = mid + 1;
            }

            (_printed, _printedScale) = (best.Pinning, -best.Scale);
            foreach (long units in Digits(best.Q)) yield return units;
            string digits = _digits.Value!;
            Text = Layout(_x._negative, digits.TrimEnd('0'), best.Scale + digits.Length - 1, most, _e, _nfi);
        }

        /// <summary>
        /// A k-digit decimal q × 10^scale that reads back as x, or null, into _candidate. The two
        /// candidates are the k-digit neighbours either side of x; if both read back, the nearer
        /// one (a tie going to the even digit).
        /// </summary>
        private IEnumerable<long> ReadsBackWith(int k, long x)
        {
            long t = k - 1 - x;
            Pinning chopped = new(), rounded = new();
            foreach (long units in Scaled(t, RoundingMode.TowardZero, chopped)) yield return units;
            uint[] below = _scaled;
            foreach (long units in Scaled(t, RoundingMode.ToNearestEven, rounded)) yield return units;
            uint[] nearest = _scaled;
            uint[] other = Magnitude.Compare(nearest, below) == 0 ? Magnitude.Add(below, [1]) : below;
            foreach (var (q, madeBy) in new[] { (nearest, rounded), (other, chopped) })
            {
                var readBack = new Pinning();
                foreach (long units in ReadBack(q, -t, readBack)) yield return units;
                if (CompareValues(readBack.Result, _x) == 0)
                {
                    _candidate = new Candidate(q, -t, madeBy);
                    yield break;
                }
            }
            _candidate = null;
        }

        private IEnumerable<long> Custom(CustomPattern pattern)
        {
            // Round to every fraction place, then drop trailing zeros back to the last 0 placeholder.
            // A pattern with no placeholders ("'hello'") prints no digits, so don't make any: on a
            // value with an enormous exponent, scaling to an integer overflows.
            int places = pattern.FractionPlaces;
            string digits = "0";
            if (pattern.InNumber && !_x.IsZero)
            {
                var pinning = new Pinning();
                foreach (long units in Scaled(places, _mode, pinning)) yield return units;
                (_printed, _printedScale) = (pinning, places);
                foreach (long units in Digits(_scaled)) yield return units;
                digits = _digits.Value!;
            }
            digits = digits.PadLeft(places + 1, '0');
            string whole = digits[..^places].TrimStart('0');
            string fraction = digits[^places..];
            int keep = fraction.Length;
            while (keep > pattern.LastFractionZero && fraction[keep - 1] == '0') keep--;
            fraction = fraction[..keep];

            int minWhole = pattern.FirstWholeZero < 0 ? 0 : pattern.WholePlaces - pattern.FirstWholeZero;
            whole = whole.PadLeft(minWhole, '0');
            if (whole.Length == 0 && fraction.Length == 0) whole = "0";
            if (pattern.Grouping) whole = Group(whole, _nfi);

            var sb = new StringBuilder();
            if (_x._negative) sb.Append(_nfi.NegativeSign);
            sb.Append(pattern.Prefix);
            if (pattern.InNumber)
            {
                sb.Append(whole);
                if (fraction.Length > 0) sb.Append(_nfi.NumberDecimalSeparator).Append(fraction);
            }
            Text = sb.Append(pattern.Suffix).ToString();
        }
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
    ///
    /// Any exponent is fine whose value's binary exponent fits a long (decimal ones up to about
    /// ±2.7 × 10^18). Past 10^±4096 decimal text goes through the certified interval engine, so
    /// "1e1000000000000000000" takes about 2 ms. Above about 1,100 bits of precision the engine
    /// starts later, where its first round is cheaper than the exact route: past about
    /// 10^±14,400 at 4096 bits. The result is the same either way; only the cost differs. The
    /// time also follows the length of the text: a 30,000-digit decimal takes most of a second.
    /// (There was a cap at ±100,000 until the engine came in; Matthew dropped it on 2026-09-30.)
    /// For long text, <see cref="StartParse"/> does the same work a slice at a time.
    /// </summary>
    /// <exception cref="FormatException">It isn't a number.</exception>
    /// <exception cref="OverflowException">The value's binary exponent wouldn't fit a long.</exception>
    public static ApFloat Parse(string s, int precision, RoundingMode mode = RoundingMode.ToNearestEven, IFormatProvider? provider = null) =>
        ParseCore(s, precision, mode, provider, out ApFloat result) switch
        {
            ParseOutcome.Parsed => result,
            ParseOutcome.TooLarge => throw TooLarge(s),
            _ => throw NotANumber(s),
        };

    private static OverflowException TooLarge(string? s, Exception? inner = null) =>
        new($"\"{s}\": the exponent is too large (the value's binary exponent must fit a long).", inner);

    private static FormatException NotANumber(string? s) => new($"Not a number: \"{s}\".");

    public static bool TryParse([NotNullWhen(true)] string? s, out ApFloat result) =>
        TryParse(s, DefaultPrecision, RoundingMode.ToNearestEven, null, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ApFloat result) =>
        TryParse(s, DefaultPrecision, RoundingMode.ToNearestEven, provider, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, int precision, RoundingMode mode, IFormatProvider? provider, out ApFloat result) =>
        ParseCore(s, precision, mode, provider, out result) == ParseOutcome.Parsed;

    private enum ParseOutcome { Parsed, NotANumber, TooLarge }

    private static ParseOutcome ParseCore(string? s, int precision, RoundingMode mode, IFormatProvider? provider, out ApFloat result)
    {
        ParseOutcome outcome = PrepareParse(s, precision, mode, provider, out result, out DecimalParse? work);
        if (outcome != ParseOutcome.Parsed || work is null) return outcome;
        try
        {
            result = work.RunToEnd();
            return ParseOutcome.Parsed;
        }
        catch (OverflowException)
        {
            return ParseOutcome.TooLarge;   // a value whose binary exponent wouldn't fit a long
        }
    }

    /// <summary>
    /// All of a parse but the decimal arithmetic: the syntax, the special values, and hex and
    /// binary (whose cost is linear). For decimal text, <paramref name="work"/> is the rest, to
    /// run to the end or a slice at a time; otherwise it's null and <paramref name="result"/>
    /// is the answer.
    /// </summary>
    private static ParseOutcome PrepareParse(string? s, int precision, RoundingMode mode, IFormatProvider? provider,
                                             out ApFloat result, out DecimalParse? work)
    {
        CheckPrecision(precision);
        result = default;
        work = null;
        if (s is null) return ParseOutcome.NotANumber;
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
            return ParseOutcome.Parsed;
        }
        if (IsWord(span, nfi.PositiveInfinitySymbol, "Infinity", "inf", "∞"))
        {
            result = InfinityOf(negative, precision);
            return ParseOutcome.Parsed;
        }

        try
        {
            if (span.Length > 2 && span[0] == '0' && span[1] is 'x' or 'X')
                return ParsePowerOfTwoRadix(span[2..], 4, negative, precision, mode, out result);
            if (span.Length > 2 && span[0] == '0' && span[1] is 'b' or 'B')
                return ParsePowerOfTwoRadix(span[2..], 1, negative, precision, mode, out result);
            return ScanDecimal(span, negative, precision, mode, nfi, out work);
        }
        catch (OverflowException)
        {
            return ParseOutcome.TooLarge;   // an exponent past a long, or a value whose binary exponent would be
        }
    }

    private static bool IsWord(ReadOnlySpan<char> span, params string[] words)
    {
        foreach (string word in words)
            if (word.Length > 0 && span.Equals(word, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// digits [separator digits] [e [sign] digits]: the syntax, and the work of making the exact
    /// rational digits × 10^exp and rounding it once.
    /// </summary>
    private static ParseOutcome ScanDecimal(ReadOnlySpan<char> span, bool negative, int precision, RoundingMode mode,
                                            NumberFormatInfo nfi, out DecimalParse? work)
    {
        work = null;
        int e = span.IndexOfAny('e', 'E');
        ReadOnlySpan<char> mantissa = e < 0 ? span : span[..e];
        long exponent = 0;
        if (e >= 0 && ParseExponent(span[(e + 1)..], out exponent) is var outcome and not ParseOutcome.Parsed) return outcome;

        string separator = nfi.NumberDecimalSeparator;
        int point = mantissa.IndexOf(separator, StringComparison.Ordinal);
        ReadOnlySpan<char> whole = point < 0 ? mantissa : mantissa[..point];
        ReadOnlySpan<char> fraction = point < 0 ? [] : mantissa[(point + separator.Length)..];
        if (whole.Length + fraction.Length == 0) return ParseOutcome.NotANumber;
        if (!DigitRun(whole, 10, allowEmpty: true) || !DigitRun(fraction, 10, allowEmpty: true)) return ParseOutcome.NotANumber;

        // At least one digit, and underscores only between digits: what the digit loop takes.
        string digits = string.Concat(whole, fraction);
        long n = checked(exponent - (fraction.Length - fraction.Count('_')));
        work = new DecimalParse(negative, digits, n, precision, mode, ForcedRoute);
        return ParseOutcome.Parsed;
    }

    /// <summary>Hex or binary digits [. digits] [p [sign] decimal digits]: exact, then rounded to the precision.</summary>
    private static ParseOutcome ParsePowerOfTwoRadix(ReadOnlySpan<char> span, int bitsPerDigit, bool negative, int precision,
                                                     RoundingMode mode, out ApFloat result)
    {
        result = default;
        int p = span.IndexOfAny('p', 'P');
        ReadOnlySpan<char> mantissa = p < 0 ? span : span[..p];
        long exponent = 0;
        if (p >= 0 && ParseExponent(span[(p + 1)..], out exponent) is var outcome and not ParseOutcome.Parsed) return outcome;

        int point = mantissa.IndexOf('.');
        ReadOnlySpan<char> whole = point < 0 ? mantissa : mantissa[..point];
        ReadOnlySpan<char> fraction = point < 0 ? [] : mantissa[(point + 1)..];
        if (whole.Length + fraction.Length == 0) return ParseOutcome.NotANumber;
        int radix = 1 << bitsPerDigit;
        if (!DigitRun(whole, radix, allowEmpty: true) || !DigitRun(fraction, radix, allowEmpty: true)) return ParseOutcome.NotANumber;

        string digits = string.Concat(whole, fraction);
        ApInt m = digits.Length == 0 ? ApInt.Zero : ApInt.Parse((bitsPerDigit == 4 ? "0x" : "0b") + digits);
        long exp = checked(exponent - (long)bitsPerDigit * (fraction.Length - fraction.Count('_')));
        result = m.IsZero ? ZeroOf(negative, precision) : new ApFloat(negative ? -m : m, exp, precision, mode);
        return ParseOutcome.Parsed;
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

    private static ParseOutcome ParseExponent(ReadOnlySpan<char> text, out long exponent)
    {
        exponent = 0;
        ReadOnlySpan<char> digits = text.Length > 0 && text[0] is '+' or '-' ? text[1..] : text;
        if (digits.IsEmpty || digits.ContainsAnyExceptInRange('0', '9')) return ParseOutcome.NotANumber;
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)
            ? ParseOutcome.Parsed
            : ParseOutcome.TooLarge;
    }

    /// <summary>
    /// ±q × 10^s, correctly rounded. Past the cache of powers of five this goes through the
    /// certified interval engine (ApFloat.Certified.cs), which falls back to the exact route
    /// when it can't settle the rounding.
    /// </summary>
    private static ApFloat FromDecimal(bool negative, uint[] q, long s, int precision, RoundingMode mode)
    {
        if (q.Length == 0) return ZeroOf(negative, precision);
        var pinning = new Pinning();
        return RunToEnd(FromDecimalSteps(negative, q, s, precision, mode, ForcedRoute, pinning), pinning);
    }

    /// <summary>FromDecimal as steps, for q non-zero: the first round has the precision and the guard bits.</summary>
    private static IEnumerable<long> FromDecimalSteps(bool negative, uint[] q, long s, int precision, RoundingMode mode,
                                                      Route route, Pinning pinning) =>
        TimesPowerOfTenSteps(q, 0, s, (long)precision + GuardBits,
            (mag, exp) => RoundExact(negative, mag, exp, false, precision, mode),
            () => FromDecimalExact(negative, q, s, precision, mode), route, pinning);

    /// <summary>
    /// FromDecimal the exact way: the exact rational, rounded once. For s &gt;= 0 that's the
    /// integer q × 5^s × 2^s; below, it's q × 2^s / 5^-s, divided as Divide does it (enough
    /// quotient bits for the rounding, and the remainder as the sticky bit). q is non-zero.
    /// </summary>
    private static ApFloat FromDecimalExact(bool negative, uint[] q, long s, int precision, RoundingMode mode)
    {
        if (s >= 0) return RoundExact(negative, Magnitude.Multiply(q, PowerOfFive(s)), s, false, precision, mode);
        uint[] five = PowerOfFive(-s);
        long shift = Math.Max(0, precision + 2 + Magnitude.BitLength(five) - Magnitude.BitLength(q));
        uint[] quotient = Magnitude.DivRem(Magnitude.ShiftLeft(q, shift), five, out uint[] remainder);
        return RoundExact(negative, quotient, checked(s - shift), remainder.Length != 0, precision, mode);
    }
}

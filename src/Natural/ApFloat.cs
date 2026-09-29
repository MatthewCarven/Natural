using System.Globalization;

namespace Natural;

/// <summary>The four IEEE 754 rounding directions.</summary>
public enum RoundingMode
{
    /// <summary>To the nearest representable value; an exact tie goes to the one whose last bit is 0. IEEE's default.</summary>
    ToNearestEven,
    /// <summary>Chop: toward zero.</summary>
    TowardZero,
    /// <summary>Toward +infinity (ceiling).</summary>
    TowardPositive,
    /// <summary>Toward -infinity (floor).</summary>
    TowardNegative,
}

/// <summary>
/// An arbitrary-precision binary floating-point number that behaves like IEEE 754:
/// every operation is correctly rounded (the exact result, rounded once), with signed
/// zeros, infinities and NaN, and the same results as <see cref="double"/> when the
/// precision is 53 bits.
///
/// A finite value is <c>±significand × 2^exponent</c>. The significand is an odd
/// magnitude of at most <see cref="Precision"/> bits (odd, so every value has exactly
/// one representation), and the exponent is a plain signed <see cref="long"/>: no bias
/// in memory. The biased exponent belongs to the IEEE byte formats.
///
/// Precision is carried per value. An operator rounds its result to the larger of its
/// operands' precisions; the static methods take the precision and rounding mode
/// explicitly. <c>default(ApFloat)</c> is +0 at <see cref="DefaultPrecision"/>.
/// </summary>
public readonly partial struct ApFloat : IEquatable<ApFloat>, IComparable<ApFloat>, IComparable
{
    /// <summary>
    /// 256 bits, about 77 significant decimal digits. (Matthew's choice. For comparison,
    /// <see cref="double"/> has 53 and IEEE binary256 has 237.)
    /// </summary>
    public const int DefaultPrecision = 256;
    public const int MaxPrecision = 1 << 30;

    private enum Kind : byte { Zero = 0, Finite, Infinity, NaN }   // Zero first, so default(ApFloat) is +0

    private readonly uint[]? _mant;     // Finite only: odd, at most Precision bits
    private readonly long _exp;         // Finite only
    private readonly int _prec;         // 0 means DefaultPrecision (so default(ApFloat) works)
    private readonly Kind _kind;
    private readonly bool _negative;    // the sign bit: kept for zeros and infinities, never set for NaN

    private ApFloat(Kind kind, bool negative, uint[]? mant, long exp, int precision)
    {
        _kind = kind;
        _negative = negative && kind != Kind.NaN;
        _mant = mant;
        _exp = exp;
        _prec = precision;
    }

    /// <summary>The value <c>significand × 2^exponent</c>, rounded to <paramref name="precision"/> bits.</summary>
    public ApFloat(ApInt significand, long exponent = 0, int precision = DefaultPrecision,
                   RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckPrecision(precision);
        this = RoundExact(significand.IsNegative, significand.Limbs, exponent, false, precision, mode);
    }

    private uint[] Mant => _mant ?? Magnitude.Empty;

    // ------------------------------------------------------------------
    // Properties and special values
    // ------------------------------------------------------------------

    public int Precision => _prec == 0 ? DefaultPrecision : _prec;

    public bool IsNaN => _kind == Kind.NaN;
    public bool IsInfinity => _kind == Kind.Infinity;
    public bool IsZero => _kind == Kind.Zero;
    /// <summary>Zero or an ordinary number: not infinite and not NaN.</summary>
    public bool IsFinite => _kind is Kind.Zero or Kind.Finite;
    /// <summary>The sign bit. True for -0 and -infinity; false for NaN.</summary>
    public bool IsNegative => _negative;

    /// <summary>For a finite value, the odd magnitude m in ±m × 2^e (0 for zero).</summary>
    public ApInt Significand => _kind switch
    {
        Kind.Finite => ApInt.FromLimbs(Mant, false),
        Kind.Zero => ApInt.Zero,
        _ => throw new InvalidOperationException("Infinity and NaN have no significand."),
    };

    /// <summary>For a finite value, the e in ±m × 2^e (0 for zero).</summary>
    public long Exponent => _kind switch
    {
        Kind.Finite => _exp,
        Kind.Zero => 0,
        _ => throw new InvalidOperationException("Infinity and NaN have no exponent."),
    };

    public static ApFloat Zero => default;
    public static ApFloat NegativeZero => ZeroOf(true, DefaultPrecision);
    public static ApFloat PositiveInfinity => InfinityOf(false, DefaultPrecision);
    public static ApFloat NegativeInfinity => InfinityOf(true, DefaultPrecision);
    public static ApFloat NaN => NaNOf(DefaultPrecision);

    private static ApFloat ZeroOf(bool negative, int precision) => new(Kind.Zero, negative, null, 0, precision);
    private static ApFloat InfinityOf(bool negative, int precision) => new(Kind.Infinity, negative, null, 0, precision);
    private static ApFloat NaNOf(int precision) => new(Kind.NaN, false, null, 0, precision);

    private static void CheckPrecision(int precision)
    {
        if (precision < 1 || precision > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision, $"Precision must be 1 to {MaxPrecision} bits.");
    }

    /// <summary>This value rounded to a different precision.</summary>
    public ApFloat WithPrecision(int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckPrecision(precision);
        return _kind switch
        {
            Kind.Finite => RoundExact(_negative, Mant, _exp, false, precision, mode),
            Kind.Zero => ZeroOf(_negative, precision),
            Kind.Infinity => InfinityOf(_negative, precision),
            _ => NaNOf(precision),
        };
    }

    // ------------------------------------------------------------------
    // Rounding: the one place a result is ever made inexact
    // ------------------------------------------------------------------

    /// <summary>
    /// Rounds ±mag × 2^exp to <paramref name="precision"/> bits. If <paramref name="sticky"/>
    /// is set, the true value is a little more than that (by less than 2^exp): it is how
    /// division reports a non-zero remainder.
    ///
    /// <paramref name="minExp"/>, if given, is the lowest place value the last kept bit may
    /// have. IEEE formats need it for subnormals, where precision shrinks near zero.
    /// </summary>
    private static ApFloat RoundExact(bool negative, uint[] mag, long exp, bool sticky, int precision,
                                      RoundingMode mode, long minExp = long.MinValue)
    {
        if (mag.Length == 0)
        {
            if (sticky) throw new InvalidOperationException("Sticky bit with a zero significand.");
            return ZeroOf(negative, precision);
        }

        // How many low bits must go.
        long n = Magnitude.BitLength(mag);
        long drop = n - precision;
        if (minExp != long.MinValue && minExp - exp > drop) drop = minExp - exp;

        if (drop <= 0)
        {
            if (!sticky) return Normalised(negative, mag, exp, precision);   // fits: exact
            // Sticky, but no bits to drop: widen with zeros so the rounding point has a
            // (zero) half bit below it and the sticky bit below that.
            mag = Magnitude.ShiftLeft(mag, 2 - drop);
            exp = checked(exp - (2 - drop));
            drop = 2;
        }

        uint[] kept = Magnitude.ShiftRight(mag, drop);
        bool half = Magnitude.TestBit(mag, drop - 1);                       // the first bit dropped
        bool rest = sticky || Magnitude.AnyBitsBelow(mag, drop - 1);         // anything below it
        bool roundUp = mode switch
        {
            RoundingMode.ToNearestEven => half && (rest || Magnitude.TestBit(kept, 0)),
            RoundingMode.TowardZero => false,
            RoundingMode.TowardPositive => !negative && (half || rest),
            RoundingMode.TowardNegative => negative && (half || rest),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        // Rounding up can carry all the way (0111..1 + 1 = 1000..0): one bit longer, but
        // then it's a power of two and Normalised strips it back down.
        if (roundUp) kept = Magnitude.Add(kept, [1]);
        if (kept.Length == 0) return ZeroOf(negative, precision);             // only possible with minExp
        return Normalised(negative, kept, checked(exp + drop), precision);
    }

    /// <summary>Strips trailing zero bits so the significand is odd.</summary>
    private static ApFloat Normalised(bool negative, uint[] mag, long exp, int precision)
    {
        long zeros = Magnitude.TrailingZeroCount(mag);
        if (zeros > 0)
        {
            mag = Magnitude.ShiftRight(mag, zeros);
            exp = checked(exp + zeros);
        }
        return new ApFloat(Kind.Finite, negative, mag, exp, precision);
    }

    /// <summary>Place value of the leading bit: a finite x has 2^Top &lt;= |x| &lt; 2^(Top+1).</summary>
    private long Top => _exp + Magnitude.BitLength(Mant) - 1;

    // ------------------------------------------------------------------
    // Arithmetic
    // ------------------------------------------------------------------

    public static ApFloat operator +(ApFloat a) => a;
    public static ApFloat operator -(ApFloat a) => new(a._kind, !a._negative, a._mant, a._exp, a.Precision);

    public static ApFloat Abs(ApFloat a) => new(a._kind, false, a._mant, a._exp, a.Precision);

    public static ApFloat operator +(ApFloat a, ApFloat b) => Add(a, b, Math.Max(a.Precision, b.Precision));
    public static ApFloat operator -(ApFloat a, ApFloat b) => Subtract(a, b, Math.Max(a.Precision, b.Precision));
    public static ApFloat operator *(ApFloat a, ApFloat b) => Multiply(a, b, Math.Max(a.Precision, b.Precision));
    public static ApFloat operator /(ApFloat a, ApFloat b) => Divide(a, b, Math.Max(a.Precision, b.Precision));

    public static ApFloat Add(ApFloat a, ApFloat b, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckPrecision(precision);
        if (a.IsNaN || b.IsNaN) return NaNOf(precision);
        if (a.IsInfinity)
            return b.IsInfinity && a._negative != b._negative ? NaNOf(precision) : InfinityOf(a._negative, precision);
        if (b.IsInfinity) return InfinityOf(b._negative, precision);
        if (a.IsZero && b.IsZero)
        {
            // IEEE: zeros of the same sign keep it; opposite signs give +0 (-0 when rounding down).
            bool negative = a._negative == b._negative ? a._negative : mode == RoundingMode.TowardNegative;
            return ZeroOf(negative, precision);
        }
        if (a.IsZero) return RoundExact(b._negative, b.Mant, b._exp, false, precision, mode);
        if (b.IsZero) return RoundExact(a._negative, a.Mant, a._exp, false, precision, mode);

        // Let a be the one with the higher leading bit.
        long topA = a.Top, topB = b.Top;
        if (topA < topB) { (a, b) = (b, a); (topA, topB) = (topB, topA); }

        // Exact addition means lining both up at the lower exponent, and 1 + 2^-1000000
        // would build a million-bit number to throw nearly all of it away. When b lies
        // wholly below both a's last bit and the result's rounding point, only the fact
        // that it's there can matter (it's pure sticky): any value in (0, 2^threshold)
        // rounds the same way. So swap it for the single bit 2^(threshold-1).
        // (The result's leading bit is at least topA - 1, so its half-ulp bit sits at or
        // above topA - precision - 1, above the threshold.)
        uint[] mb = b.Mant;
        long eb = b._exp;
        long threshold = Math.Min(a._exp, topA - precision - 1) - 1;
        if (topB < threshold)
        {
            mb = [1];
            eb = threshold - 1;
        }

        long low = Math.Min(a._exp, eb);
        uint[] x = Magnitude.ShiftLeft(a.Mant, a._exp - low);
        uint[] y = Magnitude.ShiftLeft(mb, eb - low);

        if (a._negative == b._negative)
            return RoundExact(a._negative, Magnitude.Add(x, y), low, false, precision, mode);

        int cmp = Magnitude.Compare(x, y);
        if (cmp == 0) return ZeroOf(mode == RoundingMode.TowardNegative, precision);   // x - x is +0 (or -0 rounding down)
        return cmp > 0
            ? RoundExact(a._negative, Magnitude.Subtract(x, y), low, false, precision, mode)
            : RoundExact(b._negative, Magnitude.Subtract(y, x), low, false, precision, mode);
    }

    public static ApFloat Subtract(ApFloat a, ApFloat b, int precision, RoundingMode mode = RoundingMode.ToNearestEven) =>
        Add(a, -b, precision, mode);

    public static ApFloat Multiply(ApFloat a, ApFloat b, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckPrecision(precision);
        bool negative = a._negative ^ b._negative;
        if (a.IsNaN || b.IsNaN) return NaNOf(precision);
        if (a.IsInfinity || b.IsInfinity)
            return a.IsZero || b.IsZero ? NaNOf(precision) : InfinityOf(negative, precision);   // 0 × ∞ is NaN
        if (a.IsZero || b.IsZero) return ZeroOf(negative, precision);

        return RoundExact(negative, Magnitude.Multiply(a.Mant, b.Mant), checked(a._exp + b._exp), false, precision, mode);
    }

    public static ApFloat Divide(ApFloat a, ApFloat b, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckPrecision(precision);
        bool negative = a._negative ^ b._negative;
        if (a.IsNaN || b.IsNaN) return NaNOf(precision);
        if (a.IsInfinity) return b.IsInfinity ? NaNOf(precision) : InfinityOf(negative, precision);
        if (b.IsInfinity) return ZeroOf(negative, precision);
        if (b.IsZero) return a.IsZero ? NaNOf(precision) : InfinityOf(negative, precision);    // x / 0 is ±∞; 0 / 0 is NaN
        if (a.IsZero) return ZeroOf(negative, precision);

        // Scale the dividend up so the integer quotient has at least precision + 2 bits:
        // enough for the result bits plus the half bit, with the remainder as the sticky bit.
        long na = Magnitude.BitLength(a.Mant), nb = Magnitude.BitLength(b.Mant);
        long shift = Math.Max(0, precision + 2 + nb - na);
        uint[] quotient = Magnitude.DivRem(Magnitude.ShiftLeft(a.Mant, shift), b.Mant, out uint[] remainder);
        long exp = checked(a._exp - shift - b._exp);
        return RoundExact(negative, quotient, exp, remainder.Length != 0, precision, mode);
    }

    // ------------------------------------------------------------------
    // Conversions to and from integers
    // ------------------------------------------------------------------

    /// <summary>Exact: the precision is the integer's bit length (at least <see cref="DefaultPrecision"/>).</summary>
    public static implicit operator ApFloat(ApInt value) =>
        new(value, 0, (int)Math.Max(DefaultPrecision, Math.Min(value.BitLength, MaxPrecision)));

    public static implicit operator ApFloat(long value) => (ApInt)value;

    /// <summary>Truncates toward zero, like (long)double. Infinity and NaN throw.</summary>
    public static explicit operator ApInt(ApFloat value)
    {
        if (!value.IsFinite) throw new OverflowException($"{value} has no integer value.");
        if (value.IsZero) return ApInt.Zero;
        uint[] mag = value._exp >= 0
            ? Magnitude.ShiftLeft(value.Mant, value._exp)
            : Magnitude.ShiftRight(value.Mant, -value._exp);
        return ApInt.FromLimbs(mag, value._negative);
    }

    // ------------------------------------------------------------------
    // Comparison and equality (IEEE: NaN is unordered, +0 == -0)
    // ------------------------------------------------------------------

    /// <summary>Compares two non-NaN values by value.</summary>
    private static int CompareValues(ApFloat a, ApFloat b)
    {
        int sa = a.SignOf(), sb = b.SignOf();
        if (sa != sb) return sa.CompareTo(sb);
        if (sa == 0) return 0;
        int magnitude = CompareMagnitudes(a, b);
        return sa > 0 ? magnitude : -magnitude;
    }

    private int SignOf() => _kind == Kind.Zero ? 0 : _negative ? -1 : 1;

    private static int CompareMagnitudes(ApFloat a, ApFloat b)
    {
        if (a.IsInfinity || b.IsInfinity) return (a.IsInfinity ? 1 : 0) - (b.IsInfinity ? 1 : 0);
        long topA = a.Top, topB = b.Top;
        if (topA != topB) return topA.CompareTo(topB);
        // Same leading place, so lining them up shifts by less than either's length.
        long low = Math.Min(a._exp, b._exp);
        return Magnitude.Compare(Magnitude.ShiftLeft(a.Mant, a._exp - low), Magnitude.ShiftLeft(b.Mant, b._exp - low));
    }

    /// <summary>Like double.CompareTo: NaN sorts below everything and equals itself; -0 equals +0.</summary>
    public int CompareTo(ApFloat other)
    {
        if (IsNaN || other.IsNaN) return (other.IsNaN ? 1 : 0) - (IsNaN ? 1 : 0);
        return CompareValues(this, other);
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ApFloat other => CompareTo(other),
        _ => throw new ArgumentException("Object is not an ApFloat.", nameof(obj)),
    };

    /// <summary>Like double.Equals: by value, ignoring precision; NaN equals NaN; -0 equals +0.</summary>
    public bool Equals(ApFloat other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is ApFloat other && Equals(other);

    public override int GetHashCode()
    {
        if (_kind != Kind.Finite) return _kind == Kind.Infinity ? (_negative ? -1 : 1) : (int)_kind;   // ±0 hash alike
        var hash = new HashCode();
        hash.Add(_negative);
        hash.Add(_exp);
        foreach (uint limb in Mant) hash.Add(limb);
        return hash.ToHashCode();
    }

    // The IEEE comparison operators: every comparison involving NaN is false, except !=.
    public static bool operator ==(ApFloat a, ApFloat b) => !a.IsNaN && !b.IsNaN && CompareValues(a, b) == 0;
    public static bool operator !=(ApFloat a, ApFloat b) => !(a == b);
    public static bool operator <(ApFloat a, ApFloat b) => !a.IsNaN && !b.IsNaN && CompareValues(a, b) < 0;
    public static bool operator >(ApFloat a, ApFloat b) => !a.IsNaN && !b.IsNaN && CompareValues(a, b) > 0;
    public static bool operator <=(ApFloat a, ApFloat b) => !a.IsNaN && !b.IsNaN && CompareValues(a, b) <= 0;
    public static bool operator >=(ApFloat a, ApFloat b) => !a.IsNaN && !b.IsNaN && CompareValues(a, b) >= 0;

    // ------------------------------------------------------------------
    // Text (decimal comes later; hex is exact and needs no rounding)
    // ------------------------------------------------------------------

    /// <summary>
    /// Hexadecimal floating point, as C's printf("%a") writes it: 3.0 is "0x1.8p+1"
    /// (1.1 in binary, times 2^1), 0.1 is "0x1.999999999999ap-4". Exact, always.
    /// </summary>
    public override string ToString()
    {
        switch (_kind)
        {
            case Kind.NaN: return "NaN";
            case Kind.Infinity: return _negative ? "-Infinity" : "Infinity";
            case Kind.Zero: return _negative ? "-0x0p+0" : "0x0p+0";
        }

        // The leading 1 sits alone before the point; pad the fraction bits out to whole
        // hex digits. That makes the bit count 1 + 4k, so the hex string starts with "1".
        long fractionBits = Magnitude.BitLength(Mant) - 1;
        long pad = (4 - fractionBits % 4) % 4;
        string hex = ApInt.FromLimbs(Magnitude.ShiftLeft(Mant, pad), false).ToString("x");
        string exponent = Top.ToString("+0;-0", CultureInfo.InvariantCulture);
        string body = hex.Length > 1 ? $"1.{hex[1..]}" : "1";
        return $"{(_negative ? "-" : "")}0x{body}p{exponent}";
    }
}

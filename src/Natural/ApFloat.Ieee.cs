namespace Natural;

// IEEE 754 interchange formats up to 64 bits wide: Half, float and double, and the
// encoder / decoder behind them, which takes any exponent width and precision.
public readonly partial struct ApFloat
{
    // ------------------------------------------------------------------
    // Half / float / double
    // ------------------------------------------------------------------

    /// <summary>
    /// Exact. The result keeps the double's 53 bits of precision, so arithmetic between
    /// two converted doubles rounds as double arithmetic does (in double's normal range:
    /// see the note on <see cref="ApFloat"/> about subnormal results).
    /// </summary>
    public static implicit operator ApFloat(double value) =>
        FromIeeeBits((ulong)BitConverter.DoubleToInt64Bits(value), 11, 53);

    /// <summary>Exact, keeping the float's 24 bits of precision.</summary>
    public static implicit operator ApFloat(float value) =>
        FromIeeeBits((uint)BitConverter.SingleToInt32Bits(value), 8, 24);

    /// <summary>Exact, keeping the Half's 11 bits of precision.</summary>
    public static implicit operator ApFloat(Half value) =>
        FromIeeeBits((ushort)BitConverter.HalfToInt16Bits(value), 5, 11);

    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator double(ApFloat value) => value.ToDouble();
    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator float(ApFloat value) => value.ToSingle();
    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator Half(ApFloat value) => value.ToHalf();

    /// <summary>Rounded once, straight to double: subnormal near zero, infinite (or the largest finite) past the top.</summary>
    public double ToDouble(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int64BitsToDouble(unchecked((long)ToIeeeBits(11, 53, mode)));

    /// <summary>Rounded once, straight to float.</summary>
    public float ToSingle(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int32BitsToSingle(unchecked((int)ToIeeeBits(8, 24, mode)));

    /// <summary>Rounded once, straight to Half.</summary>
    public Half ToHalf(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int16BitsToHalf(unchecked((short)ToIeeeBits(5, 11, mode)));

    // ------------------------------------------------------------------
    // The encoder and decoder
    // ------------------------------------------------------------------

    // A format with w exponent bits and precision p is w + p bits wide: the sign, w bits of
    // biased exponent, then p - 1 fraction bits (the leading 1 is implicit). The bias is the
    // largest exponent field over two, max / 2 = 0111...1: 15, 127, 1023. A field of all ones
    // is infinity (fraction 0) or NaN; all zeros is zero or a subnormal, whose leading bit is
    // explicit and whose place values continue on down from the smallest normal binade.

    /// <summary>
    /// Decodes an IEEE bit pattern held in the low w + p bits, exactly, at precision p.
    /// Every NaN (quiet or signalling, any payload) decodes as NaN.
    /// </summary>
    internal static ApFloat FromIeeeBits(ulong bits, int exponentBits, int precision)
    {
        CheckIeeeFormat(exponentBits, precision);
        int fractionBits = precision - 1;
        ulong maxField = LowBits(exponentBits);
        long bias = (long)(maxField >> 1);

        bool negative = ((bits >> (exponentBits + fractionBits)) & 1) != 0;
        ulong field = (bits >> fractionBits) & maxField;
        ulong fraction = bits & LowBits(fractionBits);

        if (field == maxField) return fraction == 0 ? InfinityOf(negative, precision) : NaNOf(precision);
        if (field == 0)
        {
            if (fraction == 0) return ZeroOf(negative, precision);
            return Normalised(negative, ToLimbs(fraction), 1 - bias - fractionBits, precision);   // subnormal
        }
        return Normalised(negative, ToLimbs(fraction | (1UL << fractionBits)), (long)field - bias - fractionBits, precision);
    }

    /// <summary>
    /// Encodes to an IEEE bit pattern, rounding once, straight to the format: to p bits in
    /// its normal range, to fewer among the subnormals, and past the largest finite value to
    /// infinity (or to the largest finite value, when rounding toward zero from that side).
    /// NaN is always the one quiet NaN: sign 0, exponent all ones, top fraction bit 1.
    /// </summary>
    internal ulong ToIeeeBits(int exponentBits, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        CheckIeeeFormat(exponentBits, precision);
        int fractionBits = precision - 1;
        ulong maxField = LowBits(exponentBits);
        long bias = (long)(maxField >> 1);
        long emax = bias, emin = 1 - bias;       // leading-bit place values of the normal binades
        long minExp = emin - fractionBits;       // the smallest subnormal is 2^minExp

        ulong sign = _negative ? 1UL << (exponentBits + fractionBits) : 0;
        ulong infinity = sign | (maxField << fractionBits);
        switch (_kind)
        {
            case Kind.NaN: return (maxField << fractionBits) | (1UL << (fractionBits - 1));
            case Kind.Infinity: return infinity;
            case Kind.Zero: return sign;
        }

        // Overflow is infinity, except rounding toward zero from this side, which stops at the
        // largest finite value: exponent field all ones but the last bit, fraction all ones.
        bool overflowsToInfinity = mode switch
        {
            RoundingMode.ToNearestEven => true,
            RoundingMode.TowardZero => false,
            RoundingMode.TowardPositive => !_negative,
            RoundingMode.TowardNegative => _negative,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        ulong overflow = overflowsToInfinity ? infinity : sign | ((maxField ^ 1) << fractionBits) | LowBits(fractionBits);

        // Already past the largest binade: overflow, whichever way it rounds. And under half the
        // smallest subnormal, only the sign and the fact that it isn't zero can matter (as with
        // Add's gap shortcut), so one bit there stands in for it and huge exponents stay cheap.
        if (Top > emax) return overflow;
        uint[] mant = Mant;
        long exp = _exp;
        if (Top < minExp - 1)
        {
            mant = [1];
            exp = minExp - 2;
        }

        ApFloat r = RoundExact(_negative, mant, exp, false, precision, mode, minExp);
        if (r.IsZero) return sign;                       // underflowed to a signed zero
        long top = r.Top;
        if (top > emax) return overflow;                 // rounded up past the largest finite value

        // Subnormal: field 0, and the bits sit at their place values above 2^minExp.
        if (top < emin) return sign | ToUInt64(Magnitude.ShiftLeft(r.Mant, r._exp - minExp));

        // Normal: line the significand up as p bits, then drop its leading 1, which is implicit.
        ulong significand = ToUInt64(Magnitude.ShiftLeft(r.Mant, r._exp - (top - fractionBits)));
        return sign | ((ulong)(top + bias) << fractionBits) | (significand & LowBits(fractionBits));
    }

    private static void CheckIeeeFormat(int exponentBits, int precision)
    {
        if (exponentBits < 2 || precision < 2 || exponentBits + precision > 64)
            throw new ArgumentOutOfRangeException(nameof(exponentBits),
                $"No IEEE format of 64 bits or fewer has {exponentBits} exponent bits and precision {precision}.");
    }

    /// <summary>The low <paramref name="count"/> bits set (count below 64).</summary>
    private static ulong LowBits(int count) => ~(~0UL << count);

    private static uint[] ToLimbs(ulong value) => Magnitude.Trim([(uint)value, (uint)(value >> 32)]);

    private static ulong ToUInt64(uint[] mag) => mag.Length switch
    {
        0 => 0,
        1 => mag[0],
        _ => ((ulong)mag[1] << 32) | mag[0],
    };
}

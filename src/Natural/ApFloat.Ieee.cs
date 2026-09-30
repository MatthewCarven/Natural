namespace Natural;

// IEEE 754 binary interchange formats: bytes in any IeeeFormat, and Half, float and double.
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
        DecodeIeee(ToLimbs((ulong)BitConverter.DoubleToInt64Bits(value)), IeeeFormat.Binary64);

    /// <summary>Exact, keeping the float's 24 bits of precision.</summary>
    public static implicit operator ApFloat(float value) =>
        DecodeIeee(ToLimbs((uint)BitConverter.SingleToInt32Bits(value)), IeeeFormat.Binary32);

    /// <summary>Exact, keeping the Half's 11 bits of precision.</summary>
    public static implicit operator ApFloat(Half value) =>
        DecodeIeee(ToLimbs((ushort)BitConverter.HalfToInt16Bits(value)), IeeeFormat.Binary16);

    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator double(ApFloat value) => value.ToDouble();
    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator float(ApFloat value) => value.ToSingle();
    /// <summary>Rounds to nearest, ties to even, as the hardware does.</summary>
    public static explicit operator Half(ApFloat value) => value.ToHalf();

    /// <summary>Rounded once, straight to double: subnormal near zero, infinite (or the largest finite) past the top.</summary>
    public double ToDouble(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int64BitsToDouble(unchecked((long)ToUInt64(EncodeIeee(IeeeFormat.Binary64, mode))));

    /// <summary>Rounded once, straight to float.</summary>
    public float ToSingle(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int32BitsToSingle(unchecked((int)ToUInt64(EncodeIeee(IeeeFormat.Binary32, mode))));

    /// <summary>Rounded once, straight to Half.</summary>
    public Half ToHalf(RoundingMode mode = RoundingMode.ToNearestEven) =>
        BitConverter.Int16BitsToHalf(unchecked((short)ToUInt64(EncodeIeee(IeeeFormat.Binary16, mode))));

    // ------------------------------------------------------------------
    // Bytes, in any format
    // ------------------------------------------------------------------

    /// <summary>
    /// Decodes a value from its IEEE bytes, exactly, at the format's precision. The span must
    /// be exactly the format's width. Little-endian by default, as <see cref="BitConverter"/>
    /// writes on x86 and ARM. Every NaN (quiet or signalling, any payload, either sign) decodes as NaN.
    /// </summary>
    public static ApFloat FromIeeeBytes(ReadOnlySpan<byte> bytes, IeeeFormat format, bool bigEndian = false)
    {
        int length = ByteLength(format);
        if (bytes.Length != length)
            throw new ArgumentException($"{format} is {length} bytes, not {bytes.Length}.", nameof(bytes));
        var pattern = new uint[(length + 3) >> 2];
        for (int i = 0; i < length; i++)
            pattern[i >> 2] |= (uint)bytes[bigEndian ? length - 1 - i : i] << ((i & 3) << 3);
        return DecodeIeee(pattern, format);
    }

    /// <summary>
    /// The IEEE bytes of this value in the format, rounded once, straight to it: to the
    /// format's precision in its normal range, to fewer bits among the subnormals, and past
    /// the largest finite value to infinity (or to the largest finite value, when rounding
    /// toward zero from that side). NaN is always the one quiet NaN: sign 0, exponent all
    /// ones, top fraction bit 1, the rest 0.
    /// </summary>
    public byte[] ToIeeeBytes(IeeeFormat format, RoundingMode mode = RoundingMode.ToNearestEven, bool bigEndian = false)
    {
        int length = ByteLength(format);
        uint[] pattern = EncodeIeee(format, mode);
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
            bytes[bigEndian ? length - 1 - i : i] = (byte)(pattern[i >> 2] >> ((i & 3) << 3));
        return bytes;
    }

    private static int ByteLength(IeeeFormat format)
    {
        CheckFormat(format);
        if ((format.Width & 7) != 0)
            throw new ArgumentException($"{format} is {format.Width} bits wide, which isn't whole bytes.", nameof(format));
        return format.Width >> 3;
    }

    private static void CheckFormat(IeeeFormat format)
    {
        if (format.ExponentBits == 0) throw new ArgumentException("default(IeeeFormat) isn't a format.", nameof(format));
    }

    // For the tests: formats up to 64 bits wide, as a ulong holding the low w + p bits.
    internal static ApFloat FromIeeeBits(ulong bits, int exponentBits, int precision) =>
        DecodeIeee(ToLimbs(bits), NarrowFormat(exponentBits, precision));

    internal ulong ToIeeeBits(int exponentBits, int precision, RoundingMode mode = RoundingMode.ToNearestEven) =>
        ToUInt64(EncodeIeee(NarrowFormat(exponentBits, precision), mode));

    private static IeeeFormat NarrowFormat(int exponentBits, int precision)
    {
        var format = new IeeeFormat(exponentBits, precision);
        if (format.Width > 64) throw new ArgumentOutOfRangeException(nameof(precision), "Wider than a ulong.");
        return format;
    }

    // ------------------------------------------------------------------
    // The encoder and decoder
    // ------------------------------------------------------------------

    // A bit pattern is held as little-endian words, like a magnitude but not trimmed: the
    // fraction's p - 1 bits at the bottom, the w-bit exponent field above them, and the sign
    // at the top, bit w + p - 1.

    private static ApFloat DecodeIeee(uint[] pattern, IeeeFormat format)
    {
        int precision = format.Precision, fractionBits = precision - 1;
        ulong maxField = LowBits(format.ExponentBits);
        bool negative = Magnitude.TestBit(pattern, format.Width - 1);
        ulong field = GetBits(pattern, fractionBits, format.ExponentBits);
        uint[] fraction = LowPart(pattern, fractionBits);

        if (field == maxField) return fraction.Length == 0 ? InfinityOf(negative, precision) : NaNOf(precision);
        if (field == 0)
        {
            if (fraction.Length == 0) return ZeroOf(negative, precision);
            // Subnormal: 0.fraction × 2^emin, so the bits keep the place values of the smallest normals.
            return Normalised(negative, fraction, format.MinExponent - fractionBits, precision);
        }
        uint[] significand = WithBit(fraction, fractionBits);    // put back the implicit leading 1
        return Normalised(negative, significand, (long)field - format.Bias - fractionBits, precision);
    }

    private uint[] EncodeIeee(IeeeFormat format, RoundingMode mode)
    {
        CheckFormat(format);
        int w = format.ExponentBits, fractionBits = format.Precision - 1;
        ulong maxField = LowBits(w);
        long emax = format.MaxExponent, emin = format.MinExponent;
        long minExp = emin - fractionBits;               // the smallest subnormal is 2^minExp

        var pattern = new uint[(format.Width + 31) >> 5];
        if (_negative) SetBit(pattern, format.Width - 1); // never set for NaN
        switch (_kind)
        {
            case Kind.NaN:
                PutBits(pattern, fractionBits, maxField, w);
                SetBit(pattern, fractionBits - 1);        // quiet
                return pattern;
            case Kind.Infinity:
                PutBits(pattern, fractionBits, maxField, w);
                return pattern;
            case Kind.Zero:
                return pattern;
        }

        // Already past the largest binade: overflow, whichever way it rounds.
        bool overflow = Top > emax;
        ApFloat r = default;
        if (!overflow)
        {
            // Under half the smallest subnormal, only the sign and the fact that it isn't zero
            // can matter (as with Add's gap shortcut), so one bit there stands in for it and
            // huge negative exponents stay cheap.
            uint[] mant = Mant;
            long exp = _exp;
            if (Top < minExp - 1)
            {
                mant = [1];
                exp = minExp - 2;
            }
            r = RoundExact(_negative, mant, exp, false, format.Precision, mode, minExp);
            if (r.IsZero) return pattern;                 // underflowed to a signed zero
            overflow = r.Top > emax;                      // or rounded up past the largest finite value
        }

        if (overflow)
        {
            // Infinity, except rounding toward zero from this side, which stops at the largest
            // finite value: exponent field all ones but the last bit, fraction all ones.
            bool toInfinity = mode switch
            {
                RoundingMode.ToNearestEven => true,
                RoundingMode.TowardZero => false,
                RoundingMode.TowardPositive => !_negative,
                RoundingMode.TowardNegative => _negative,
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            PutBits(pattern, fractionBits, toInfinity ? maxField : maxField ^ 1, w);
            if (!toInfinity) SetLowBits(pattern, fractionBits);
            return pattern;
        }

        long top = r.Top;
        if (top < emin)
        {
            // Subnormal: field 0, and the bits sit at their place values above 2^minExp.
            OrInto(pattern, Magnitude.ShiftLeft(r.Mant, r._exp - minExp));
            return pattern;
        }
        // Normal: the significand as p bits, then the exponent field written over its leading
        // 1, which is implicit.
        OrInto(pattern, Magnitude.ShiftLeft(r.Mant, r._exp - (top - fractionBits)));
        PutBits(pattern, fractionBits, (ulong)(top + format.Bias), w);
        return pattern;
    }

    // ------------------------------------------------------------------
    // Bit fields
    // ------------------------------------------------------------------

    /// <summary>The low <paramref name="count"/> bits set (count below 64).</summary>
    private static ulong LowBits(int count) => ~(~0UL << count);

    private static void SetBit(uint[] words, long index) => words[index >> 5] |= 1u << (int)(index & 31);

    /// <summary>Bits at .. at + count - 1 of the words, as a number.</summary>
    private static ulong GetBits(uint[] words, long at, int count)
    {
        ulong value = 0;
        for (int i = 0; i < count; i++)
            if (Magnitude.TestBit(words, at + i)) value |= 1UL << i;
        return value;
    }

    /// <summary>Overwrites bits at .. at + count - 1 of the words with the low bits of value.</summary>
    private static void PutBits(uint[] words, long at, ulong value, int count)
    {
        for (int i = 0; i < count; i++)
        {
            long index = at + i;
            uint bit = 1u << (int)(index & 31);
            if (((value >> i) & 1) != 0) words[index >> 5] |= bit;
            else words[index >> 5] &= ~bit;
        }
    }

    /// <summary>Sets bits 0 .. count - 1.</summary>
    private static void SetLowBits(uint[] words, long count)
    {
        long whole = count >> 5;
        for (long i = 0; i < whole; i++) words[i] = ~0u;
        int partial = (int)(count & 31);
        if (partial != 0) words[whole] |= ~(~0u << partial);
    }

    /// <summary>Bits 0 .. count - 1 of the words, as a trimmed magnitude.</summary>
    private static uint[] LowPart(uint[] words, long count)
    {
        long needed = (count + 31) >> 5;
        var low = new uint[Math.Min(words.Length, needed)];
        Array.Copy(words, low, low.Length);
        int partial = (int)(count & 31);
        if (partial != 0 && low.Length == needed) low[^1] &= ~(~0u << partial);
        return Magnitude.Trim(low);
    }

    /// <summary>A magnitude below 2^index, with bit index added on top.</summary>
    private static uint[] WithBit(uint[] mag, long index)
    {
        var result = new uint[(index >> 5) + 1];
        Array.Copy(mag, result, mag.Length);
        SetBit(result, index);
        return result;
    }

    private static void OrInto(uint[] words, uint[] mag)
    {
        for (int i = 0; i < mag.Length; i++) words[i] |= mag[i];
    }

    private static uint[] ToLimbs(ulong value) => Magnitude.Trim([(uint)value, (uint)(value >> 32)]);

    private static ulong ToUInt64(uint[] words) => words.Length switch
    {
        0 => 0,
        1 => words[0],
        _ => ((ulong)words[1] << 32) | words[0],
    };
}

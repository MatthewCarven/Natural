namespace Adpdt;

/// <summary>
/// An arbitrary-precision signed integer. Immutable; <c>default(ApInt)</c> is zero.
///
/// Stored as sign + magnitude. The magnitude is a little-endian array of 32-bit
/// limbs with no leading zero limbs; the arithmetic on it lives in
/// <see cref="Magnitude"/> and is built entirely from bitwise operations.
/// </summary>
public readonly partial struct ApInt : IEquatable<ApInt>, IComparable<ApInt>, IComparable
{
    private readonly uint[]? _mag;     // null or empty means zero
    private readonly bool _negative;   // never true for zero

    private ApInt(uint[] trimmedMagnitude, bool negative)
    {
        _mag = trimmedMagnitude;
        _negative = negative && trimmedMagnitude.Length != 0;
    }

    private uint[] Mag => _mag ?? Magnitude.Empty;

    public static ApInt Zero => default;
    public static ApInt One => new([1], false);
    public static ApInt MinusOne => new([1], true);

    public bool IsZero => Mag.Length == 0;
    public bool IsNegative => _negative;

    /// <summary>-1, 0 or 1.</summary>
    public int Sign => IsZero ? 0 : _negative ? -1 : 1;

    /// <summary>Number of bits in the magnitude (zero has 0).</summary>
    public long BitLength => Magnitude.BitLength(Mag);

    // ------------------------------------------------------------------
    // Construction and conversion
    // ------------------------------------------------------------------

    public static implicit operator ApInt(int value) => (long)value;
    public static implicit operator ApInt(uint value) => (ulong)value;

    public static implicit operator ApInt(long value)
    {
        // unchecked 0 - x gets long.MinValue's magnitude right, where -x would overflow.
        ulong magnitude = value < 0 ? unchecked(0UL - (ulong)value) : (ulong)value;
        return FromUInt64(magnitude, value < 0);
    }

    public static implicit operator ApInt(ulong value) => FromUInt64(value, false);

    private static ApInt FromUInt64(ulong magnitude, bool negative) =>
        new(Magnitude.Trim([(uint)magnitude, (uint)(magnitude >> 32)]), negative);

    public static explicit operator long(ApInt value)
    {
        uint[] m = value.Mag;
        if (m.Length > 2) throw new OverflowException("Value does not fit in a long.");
        ulong magnitude = m.Length switch
        {
            0 => 0,
            1 => m[0],
            _ => ((ulong)m[1] << 32) | m[0],
        };
        if (value._negative)
        {
            if (magnitude > 1UL << 63) throw new OverflowException("Value does not fit in a long.");
            return unchecked((long)(0UL - magnitude));
        }
        if (magnitude > long.MaxValue) throw new OverflowException("Value does not fit in a long.");
        return (long)magnitude;
    }

    public static explicit operator ulong(ApInt value)
    {
        uint[] m = value.Mag;
        if (value._negative || m.Length > 2) throw new OverflowException("Value does not fit in a ulong.");
        return m.Length switch
        {
            0 => 0,
            1 => m[0],
            _ => ((ulong)m[1] << 32) | m[0],
        };
    }

    /// <summary>Builds a value from its magnitude as little-endian bytes, plus a sign.</summary>
    public static ApInt FromMagnitude(ReadOnlySpan<byte> littleEndian, bool negative = false)
    {
        var mag = new uint[(littleEndian.Length + 3) / 4];
        for (int i = 0; i < littleEndian.Length; i++)
            mag[i >> 2] |= (uint)littleEndian[i] << ((i & 3) << 3);
        return new ApInt(Magnitude.Trim(mag), negative);
    }

    /// <summary>The magnitude as little-endian bytes, with no trailing zero bytes (zero gives an empty array).</summary>
    public byte[] ToMagnitudeBytes()
    {
        uint[] m = Mag;
        int length = (int)((BitLength + 7) >> 3);
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
            bytes[i] = (byte)(m[i >> 2] >> ((i & 3) << 3));
        return bytes;
    }

    // ------------------------------------------------------------------
    // Arithmetic
    // ------------------------------------------------------------------

    public static ApInt operator +(ApInt a) => a;
    public static ApInt operator -(ApInt a) => new(a.Mag, !a._negative);

    public static ApInt Abs(ApInt a) => new(a.Mag, false);

    public static ApInt operator +(ApInt a, ApInt b)
    {
        // Same sign: add magnitudes, keep the sign.
        if (a._negative == b._negative)
            return new(Magnitude.Add(a.Mag, b.Mag), a._negative);

        // Opposite signs: subtract the smaller magnitude from the larger, and the
        // result takes the sign of whichever was larger.
        int cmp = Magnitude.Compare(a.Mag, b.Mag);
        if (cmp == 0) return Zero;
        return cmp > 0
            ? new(Magnitude.Subtract(a.Mag, b.Mag), a._negative)
            : new(Magnitude.Subtract(b.Mag, a.Mag), b._negative);
    }

    public static ApInt operator -(ApInt a, ApInt b) => a + -b;

    public static ApInt operator *(ApInt a, ApInt b) =>
        new(Magnitude.Multiply(a.Mag, b.Mag), a._negative ^ b._negative);

    /// <summary>
    /// Quotient and remainder, truncating toward zero like C#'s own / and %: the
    /// quotient's sign is the XOR of the operands' signs and the remainder takes the
    /// dividend's sign. So -7 / 2 is -3 remainder -1, and always
    /// dividend == quotient * divisor + remainder, with |remainder| &lt; |divisor|.
    /// </summary>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    public static (ApInt Quotient, ApInt Remainder) DivRem(ApInt dividend, ApInt divisor)
    {
        uint[] quotient = Magnitude.DivRem(dividend.Mag, divisor.Mag, out uint[] remainder);
        return (new(quotient, dividend._negative ^ divisor._negative), new(remainder, dividend._negative));
    }

    /// <summary>Truncating division, like C#: -7 / 2 is -3. (Note -7 &gt;&gt; 1 is -4: shifts floor.)</summary>
    public static ApInt operator /(ApInt a, ApInt b) => DivRem(a, b).Quotient;

    /// <summary>Remainder with the dividend's sign, like C#: -7 % 2 is -1.</summary>
    public static ApInt operator %(ApInt a, ApInt b) => DivRem(a, b).Remainder;

    public static ApInt operator ++(ApInt a) => a + One;
    public static ApInt operator --(ApInt a) => a - One;

    /// <summary>Shifts left (multiplies by 2^shift). A negative shift shifts right.</summary>
    public static ApInt operator <<(ApInt a, int shift) =>
        shift < 0 ? ShiftRightBy(a, -(long)shift) : ShiftLeftBy(a, shift);

    /// <summary>
    /// Arithmetic right shift: floor(a / 2^shift), so -5 &gt;&gt; 1 is -3, matching
    /// two's-complement integers and System.Numerics.BigInteger.
    /// </summary>
    public static ApInt operator >>(ApInt a, int shift) =>
        shift < 0 ? ShiftLeftBy(a, -(long)shift) : ShiftRightBy(a, shift);

    // The shift amount is widened to long so that negating int.MinValue can't overflow.
    private static ApInt ShiftLeftBy(ApInt a, long shift) =>
        new(Magnitude.ShiftLeft(a.Mag, shift), a._negative);

    private static ApInt ShiftRightBy(ApInt a, long shift)
    {
        if (!a._negative) return new(Magnitude.ShiftRight(a.Mag, shift), false);

        // For negative a: a >> n == -(((|a| - 1) >> n) + 1). Shifting the magnitude
        // alone would round toward zero instead of toward minus infinity.
        uint[] one = [1];
        uint[] shifted = Magnitude.ShiftRight(Magnitude.Subtract(a.Mag, one), shift);
        return new(Magnitude.Add(shifted, one), true);
    }

    // ------------------------------------------------------------------
    // Comparison and equality
    // ------------------------------------------------------------------

    public int CompareTo(ApInt other)
    {
        if (_negative != other._negative) return _negative ? -1 : 1;
        int cmp = Magnitude.Compare(Mag, other.Mag);
        return _negative ? -cmp : cmp;
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ApInt other => CompareTo(other),
        _ => throw new ArgumentException("Object is not an ApInt.", nameof(obj)),
    };

    public bool Equals(ApInt other) =>
        _negative == other._negative && Mag.AsSpan().SequenceEqual(other.Mag);

    public override bool Equals(object? obj) => obj is ApInt other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_negative);
        foreach (uint limb in Mag) hash.Add(limb);
        return hash.ToHashCode();
    }

    public static bool operator ==(ApInt a, ApInt b) => a.Equals(b);
    public static bool operator !=(ApInt a, ApInt b) => !a.Equals(b);
    public static bool operator <(ApInt a, ApInt b) => a.CompareTo(b) < 0;
    public static bool operator >(ApInt a, ApInt b) => a.CompareTo(b) > 0;
    public static bool operator <=(ApInt a, ApInt b) => a.CompareTo(b) <= 0;
    public static bool operator >=(ApInt a, ApInt b) => a.CompareTo(b) >= 0;
}

namespace Natural;

// IEEE 754's other operations (§5.3, §5.4): fused multiply-add, the square root, rounding to an
// integral value, and the remainder. Each is worked out exactly and rounded at most once, as
// + − × ÷ are. The names are .NET's (Math.FusedMultiplyAdd, Math.Sqrt, Math.Floor,
// double.Ieee754Remainder), which the generic math interfaces will ask for by name.
public readonly partial struct ApFloat
{
    // ------------------------------------------------------------------
    // Fused multiply-add
    // ------------------------------------------------------------------

    /// <summary>a × b + c, rounded once, at the largest of the three precisions (as the operators choose).</summary>
    public static ApFloat FusedMultiplyAdd(ApFloat a, ApFloat b, ApFloat c) =>
        FusedMultiplyAdd(a, b, c, Math.Max(a.Precision, Math.Max(b.Precision, c.Precision)));

    /// <summary>
    /// a × b + c with one rounding, not two: the product is kept exact (it's a significand
    /// multiply), the sum is exact, and only then is it rounded. 0 × ∞ is NaN whatever c is
    /// (unless c is NaN), and so is ∞ × y + ∞ of the other sign.
    /// </summary>
    public static ApFloat FusedMultiplyAdd(ApFloat a, ApFloat b, ApFloat c, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        IeeeFlags ignored = IeeeFlags.None;
        return FusedMultiplyAdd(a, b, c, Target.Of(precision), mode, ref ignored);
    }

    /// <summary>a × b + c rounded once into the format: exactly the hardware's fma for binary64 and binary32.</summary>
    public static ApFloat FusedMultiplyAdd(ApFloat a, ApFloat b, ApFloat c, IeeeFormat format, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        IeeeFlags ignored = IeeeFlags.None;
        return FusedMultiplyAdd(a, b, c, Target.Of(format), mode, ref ignored);
    }

    internal static ApFloat FusedMultiplyAdd(ApFloat a, ApFloat b, ApFloat c, Target target, RoundingMode mode, ref IeeeFlags flags)
    {
        int precision = target.Precision;
        if (a.IsNaN || b.IsNaN || c.IsNaN) return NaNOf(precision);
        bool negative = a._negative ^ b._negative;
        if (a.IsInfinity || b.IsInfinity)
        {
            // 0 × ∞ has no value, and an infinite product meeting the opposite infinity is ∞ − ∞.
            if (a.IsZero || b.IsZero || (c.IsInfinity && c._negative != negative))
            {
                flags |= IeeeFlags.Invalid;
                return NaNOf(precision);
            }
            return InfinityOf(negative, precision);
        }

        // The exact product, as a value Add can take: Add only reads its significand, exponent
        // and sign, and rounds once, at the end, into the target. (An odd times an odd is odd,
        // so the product is already normalised.) Add's zero rules then give IEEE's signed zeros.
        ApFloat product = a.IsZero || b.IsZero
            ? ZeroOf(negative, precision)
            : new ApFloat(Kind.Finite, negative, Magnitude.Multiply(a.Mant, b.Mant), checked(a._exp + b._exp), MaxPrecision);
        return Add(product, c, target, mode, ref flags);
    }

    // ------------------------------------------------------------------
    // Square root
    // ------------------------------------------------------------------

    /// <summary>√x, correctly rounded, at x's precision (as Math.Sqrt keeps a double a double).</summary>
    public static ApFloat Sqrt(ApFloat x) => Sqrt(x, x.Precision);

    /// <summary>
    /// √x rounded once. √(−0) is −0 and √(+∞) is +∞, as IEEE says; the root of anything below
    /// zero, −∞ included, is NaN.
    /// </summary>
    public static ApFloat Sqrt(ApFloat x, int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        IeeeFlags ignored = IeeeFlags.None;
        return Sqrt(x, Target.Of(precision), mode, ref ignored);
    }

    /// <summary>√x rounded once into the format: exactly the hardware's sqrt for binary64 and binary32.</summary>
    public static ApFloat Sqrt(ApFloat x, IeeeFormat format, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        IeeeFlags ignored = IeeeFlags.None;
        return Sqrt(x, Target.Of(format), mode, ref ignored);
    }

    internal static ApFloat Sqrt(ApFloat x, Target target, RoundingMode mode, ref IeeeFlags flags)
    {
        int precision = target.Precision;
        if (x.IsNaN) return NaNOf(precision);
        if (x.IsZero) return ZeroOf(x._negative, precision);              // √(±0) is ±0
        if (x._negative)
        {
            flags |= IeeeFlags.Invalid;
            return NaNOf(precision);
        }
        if (x.IsInfinity) return InfinityOf(false, precision);

        // x = m × 2^e. Scale m to M = m × 2^s, with e − s even, so the root's exponent is the
        // whole number (e − s)/2, and M of 2(precision + 2) bits or one more, so its integer
        // root has precision + 2 bits or more: the result's bits and the half bit, with the
        // remainder as the sticky bit, as Divide does. A long m is shifted right instead, so
        // the work depends on the precision asked for, not x's. m is odd, so that drops a 1:
        // the sticky bit again. (The root is exact only if nothing was dropped.)
        long s = ((precision + 2L) << 1) - Magnitude.BitLength(x.Mant);
        if (((x._exp - s) & 1) != 0) s++;
        uint[] scaled = s >= 0 ? Magnitude.ShiftLeft(x.Mant, s) : Magnitude.ShiftRight(x.Mant, -s);
        uint[] root = Magnitude.Sqrt(scaled, out uint[] remainder);
        return target.Round(false, root, checked(x._exp - s) >> 1, s < 0 || remainder.Length != 0, mode, ref flags);
    }

    // ------------------------------------------------------------------
    // Rounding to an integral value
    // ------------------------------------------------------------------

    /// <summary>The largest integer ≤ x, at x's precision (the result always fits). ±0, ±∞ and NaN are unchanged.</summary>
    public static ApFloat Floor(ApFloat x) => RoundToIntegral(x, RoundingMode.TowardNegative);

    /// <summary>The smallest integer ≥ x, at x's precision. A negative x above -1 gives -0, as IEEE says.</summary>
    public static ApFloat Ceiling(ApFloat x) => RoundToIntegral(x, RoundingMode.TowardPositive);

    /// <summary>x with its fraction dropped, at x's precision: toward zero, keeping the sign (-0.5 gives -0).</summary>
    public static ApFloat Truncate(ApFloat x) => RoundToIntegral(x, RoundingMode.TowardZero);

    /// <summary>The nearest integer, a tie going to the even one (as Math.Round does), at x's precision.</summary>
    public static ApFloat Round(ApFloat x) => RoundToIntegral(x, RoundingMode.ToNearestEven);

    /// <summary>The nearest integer by .NET's midpoint rules, or a directed rounding, at x's precision.</summary>
    public static ApFloat Round(ApFloat x, MidpointRounding mode) => mode switch
    {
        MidpointRounding.ToEven => RoundToIntegral(x, RoundingMode.ToNearestEven),
        MidpointRounding.AwayFromZero => RoundHalfAwayFromZero(x),
        MidpointRounding.ToZero => RoundToIntegral(x, RoundingMode.TowardZero),
        MidpointRounding.ToNegativeInfinity => RoundToIntegral(x, RoundingMode.TowardNegative),
        MidpointRounding.ToPositiveInfinity => RoundToIntegral(x, RoundingMode.TowardPositive),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a MidpointRounding."),
    };

    /// <summary>
    /// x rounded to an integer in the mode, exactly: the rounding with no bit allowed below 2^0.
    /// The result fits x's precision. With e &lt; 0 the integer is at most 2^(top+1), and top is
    /// at most precision + e - 1 &lt; precision - 1. A value already integral (e &gt;= 0) is returned as
    /// it is, however large its exponent.
    /// </summary>
    internal static ApFloat RoundToIntegral(ApFloat x, RoundingMode mode)
    {
        if (x._kind != Kind.Finite || x._exp >= 0) return x;
        ApFloat r = RoundExact(x._negative, x.Mant, x._exp, false, MaxPrecision, mode, minExp: 0);
        return r.IsZero ? ZeroOf(x._negative, x.Precision) : new ApFloat(Kind.Finite, x._negative, r.Mant, r._exp, x.Precision);
    }

    /// <summary>
    /// The nearest integer, a tie going away from zero: |x| + 1/2, truncated. Adding the half is
    /// exact (2^-1 sits at or above x's last bit, as x isn't integral). Below 1/2 it's ±0.
    /// </summary>
    private static ApFloat RoundHalfAwayFromZero(ApFloat x)
    {
        if (x._kind != Kind.Finite || x._exp >= 0) return x;
        if (x.Top < -1) return ZeroOf(x._negative, x.Precision);
        uint[] plusHalf = Magnitude.Add(x.Mant, Magnitude.ShiftLeft([1], -1 - x._exp));
        ApFloat r = RoundExact(x._negative, plusHalf, x._exp, false, MaxPrecision, RoundingMode.TowardZero, minExp: 0);
        return new ApFloat(Kind.Finite, x._negative, r.Mant, r._exp, x.Precision);
    }

    // ------------------------------------------------------------------
    // The remainder
    // ------------------------------------------------------------------

    /// <summary>
    /// IEEE 754's remainder: x − n·y, where n is x/y rounded to the nearest integer, a tie
    /// going to the even one. It's always exact, and it fits the larger of the two precisions,
    /// which is what it's given. |result| ≤ |y|/2, and a zero result has x's sign. remainder(x, ±∞)
    /// is x; remainder(±∞, y) and remainder(x, 0) are NaN. Not C#'s %, which truncates.
    /// </summary>
    public static ApFloat Ieee754Remainder(ApFloat x, ApFloat y)
    {
        IeeeFlags ignored = IeeeFlags.None;
        return Ieee754Remainder(x, y, ref ignored);
    }

    /// <summary>The exact remainder, at the larger precision, raising Invalid for ∞ or a zero divisor.</summary>
    internal static ApFloat Ieee754Remainder(ApFloat x, ApFloat y, ref IeeeFlags flags)
    {
        int precision = Math.Max(x.Precision, y.Precision);
        if (x.IsNaN || y.IsNaN) return NaNOf(precision);
        if (x.IsInfinity || y.IsZero)
        {
            flags |= IeeeFlags.Invalid;
            return NaNOf(precision);
        }
        if (y.IsInfinity || x.IsZero) return x.WithPrecisionExactly(precision);

        // Both finite and non-zero. On a common grid 2^e, x = X·2^e and y = Y·2^e for integers
        // X, Y. Then X mod 2Y gives both the remainder mod Y and the parity of floor(X/Y), which
        // a tie needs. When x's exponent is far above y's, X is enormous (2^1000000 / 3), so it's
        // reduced as (mx mod 2Y) × (2^k mod 2Y) mod 2Y, with 2^k by square-and-multiply mod 2Y.
        long e = Math.Min(x._exp, y._exp);
        uint[] Y = Magnitude.ShiftLeft(y.Mant, y._exp - e);
        uint[] twoY = Magnitude.ShiftLeft(Y, 1);
        uint[] mx = Mod(x.Mant, twoY);
        uint[] xMod2Y = Mod(Magnitude.Multiply(mx, PowerOfTwoMod(x._exp - e, twoY)), twoY);

        bool quotientOdd = Magnitude.Compare(xMod2Y, Y) >= 0;
        uint[] rem = quotientOdd ? Magnitude.Subtract(xMod2Y, Y) : xMod2Y;     // X mod Y
        // Round X/Y to nearest: up when the remainder is over half of Y, and on a tie when the
        // quotient below is odd. Rounding up leaves rem − Y, which is negative: |Y − rem| the other side.
        int vsHalf = Magnitude.Compare(Magnitude.ShiftLeft(rem, 1), Y);
        bool up = vsHalf > 0 || (vsHalf == 0 && quotientOdd);
        uint[] magnitude = up ? Magnitude.Subtract(Y, rem) : rem;
        bool negative = x._negative ^ up;
        if (magnitude.Length == 0) return ZeroOf(x._negative, precision);    // exact: the sign is x's
        return Normalised(negative, magnitude, e, precision);
    }

    /// <summary>a mod m, for a magnitude m &gt; 0.</summary>
    private static uint[] Mod(uint[] a, uint[] m)
    {
        Magnitude.DivRem(a, m, out uint[] remainder);
        return remainder;
    }

    /// <summary>2^k mod m, by square-and-multiply: k may be in the billions, m stays small.</summary>
    private static uint[] PowerOfTwoMod(long k, uint[] m)
    {
        uint[] result = Mod([1], m), square = Mod([2], m);
        for (; k > 0; k >>= 1)
        {
            if ((k & 1) != 0) result = Mod(Magnitude.Multiply(result, square), m);
            if (k > 1) square = Mod(Magnitude.Multiply(square, square), m);
        }
        return result;
    }

    /// <summary>The same value with a different precision field, for a value known to fit it.</summary>
    private ApFloat WithPrecisionExactly(int precision) => new(_kind, _negative, _mant, _exp, precision);
}

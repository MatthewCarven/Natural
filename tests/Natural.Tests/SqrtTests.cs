using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// Session 6c: the integer square root (ApInt.Sqrt, digit by digit) and ApFloat.Sqrt, correctly
/// rounded. BigInteger Newton is the integer oracle. For the float, the hardware (Math.Sqrt,
/// MathF.Sqrt, Half.Sqrt) is the oracle bit for bit; FloatOracle.SqrtOf's stand-in rational,
/// rounded by long division, is the oracle in every mode, precision and format, flags included;
/// and squaring the rounding boundaries around a result checks it with no root taken at all.
/// </summary>
public class SqrtTests
{
    // ------------------------------------------------------------------
    // The integer square root
    // ------------------------------------------------------------------

    private static void CheckIntegerRoot(BigInteger n)
    {
        var (root, remainder) = ApInt.SqrtRem(Oracle.ToAp(n));
        BigInteger r = Oracle.ToBig(root), expected = IntegerSqrt(n);
        Assert.True(r == expected, $"isqrt({n:X}): Newton says {expected:X}, got {r:X}");
        Assert.True(r * r <= n && n < (r + 1) * (r + 1), $"isqrt({n:X}) = {r:X} is not the floor of the root");
        Assert.Equal(n - r * r, Oracle.ToBig(remainder));
        Assert.Equal(root, ApInt.Sqrt(Oracle.ToAp(n)));
    }

    [Fact]
    public void IntegerRootOfEverySmallNumber()
    {
        for (int n = 0; n <= 5000; n++) CheckIntegerRoot(n);
    }

    [Fact]
    public void IntegerRootMatchesNewton()
    {
        var rng = new Random(900);
        for (int i = 0; i < 3000; i++) CheckIntegerRoot(Oracle.RandomBig(rng, 24, allowNegative: false));
    }

    [Fact]
    public void IntegerRootAtSquaresAndPowersOfTwo()
    {
        // Squares and their neighbours are where a root is off by one, and all-ones limbs are
        // where the remainder's subtraction borrows the furthest.
        var rng = new Random(901);
        for (int i = 0; i < 1000; i++)
        {
            BigInteger k = Oracle.RandomBig(rng, 12, allowNegative: false);
            CheckIntegerRoot(k * k);
            CheckIntegerRoot(k * k + 2 * k);       // the largest number whose root is still k
            if (!k.IsZero) CheckIntegerRoot(k * k - 1);
        }
        for (int bits = 0; bits <= 300; bits++)
        {
            CheckIntegerRoot(BigInteger.One << bits);
            CheckIntegerRoot((BigInteger.One << bits) - 1);
            CheckIntegerRoot((BigInteger.One << bits) + 1);
        }
    }

    [Fact]
    public void IntegerRootOfALongNumber()
    {
        var rng = new Random(902);
        CheckIntegerRoot(Oracle.RandomBig(rng, 400, allowNegative: false) | (BigInteger.One << 12_799));
        BigInteger k = Oracle.RandomBig(rng, 200, allowNegative: false);
        CheckIntegerRoot(k * k);
    }

    [Fact]
    public void IntegerRootOfANegativeThrows()
    {
        Assert.Throws<ArithmeticException>(() => ApInt.Sqrt(-1));
        Assert.Throws<ArithmeticException>(() => ApInt.SqrtRem(-(ApInt.One << 100)));
        Assert.Equal(ApInt.Zero, ApInt.Sqrt(ApInt.Zero));
    }

    // ------------------------------------------------------------------
    // The hardware, bit for bit
    // ------------------------------------------------------------------

    private static bool SameDouble(double expected, double actual) =>
        double.IsNaN(expected) ? double.IsNaN(actual) : BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual);

    /// <summary>
    /// x of precision p whose root is within a hair of a value the precision holds, or of a
    /// midpoint between two: s² rounded, for s of p bits or p + 1 (odd). Those are the hard cases
    /// for rounding a root, and random patterns almost never make them.
    /// </summary>
    private static ApFloat HardSquare(Random rng, int p, long exponent)
    {
        BigInteger s = (BigInteger.One << p) | RandomWideField(rng, p);
        s = rng.Next(2) == 0 ? s | 1 : s >> 1;
        return new ApFloat(Oracle.ToAp(s * s), 2 * exponent, p, Modes[rng.Next(4)]);
    }

    private static double Square(double s) => s * s;

    /// <summary>Whether r · r is x exactly: IEEE's inexact, for a root.</summary>
    private static bool SquaresTo(ApFloat r, ApFloat x)
    {
        var (rn, rd) = ToRational(r);
        var (xn, xd) = ToRational(x);
        return rn * rn * xd == xn * rd * rd;
    }

    [Fact]
    public void SqrtIsTheHardwaresInBinary64()
    {
        var rng = new Random(910);
        for (int i = 0; i < 20_000; i++)
        {
            double d = rng.Next(4) switch
            {
                0 => (double)HardSquare(rng, 53, rng.Next(-580, 455)),
                1 => Square(Math.ScaleB(rng.Next(1, 1 << 26), rng.Next(-537, 486))),     // exact, subnormals included
                _ => BitConverter.Int64BitsToDouble((long)RandomIeeeBits(rng, 11, 53, -1022, -1000, 0, 1, 1023)),
            };
            double expected = Math.Sqrt(d);
            var ieee = new IeeeContext(IeeeFormat.Binary64);
            ApFloat r = ieee.Sqrt(d);
            string at = $"sqrt({d:R})";
            Assert.True(SameDouble(expected, (double)r), $"{at}: hardware {expected:R}, got {Describe(r)}");
            Assert.True(SameDouble(expected, (double)ApFloat.Sqrt(d)), $"{at} at 53 bits");    // never subnormal: no second rounding

            IeeeFlags flags = double.IsNaN(d) || d == 0 || d == double.PositiveInfinity ? IeeeFlags.None
                : d < 0 ? IeeeFlags.Invalid
                : SquaresTo(r, d) ? IeeeFlags.None : IeeeFlags.Inexact;
            Assert.True(flags == ieee.Flags, $"{at}: expected {flags}, got {ieee.Flags}");
        }
    }

    [Fact]
    public void SqrtIsTheHardwaresInBinary32()
    {
        var rng = new Random(911);
        for (int i = 0; i < 20_000; i++)
        {
            float f = rng.Next(3) switch
            {
                0 => (float)HardSquare(rng, 24, rng.Next(-95, 38)),
                _ => BitConverter.Int32BitsToSingle((int)RandomIeeeBits(rng, 8, 24, -126, -100, 0, 1, 127)),
            };
            float expected = MathF.Sqrt(f);
            float actual = (float)ApFloat.Sqrt(f, IeeeFormat.Binary32);
            Assert.True(float.IsNaN(expected) ? float.IsNaN(actual) : BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
                $"sqrt({f:R}): hardware {expected:R}, got {actual:R}");
        }
    }

    [Fact]
    public void SqrtOfEveryHalf()
    {
        // .NET's Half.Sqrt goes through float, and 24 bits ≥ 2 × 11 + 2, so its double rounding
        // is harmless: it's correctly rounded, and every one of the 65536 patterns is checked.
        for (int bits = 0; bits < 1 << 16; bits++)
        {
            Half h = BitConverter.Int16BitsToHalf((short)bits);
            Half expected = Half.Sqrt(h), actual = (Half)ApFloat.Sqrt(h, IeeeFormat.Binary16);
            Assert.True(Half.IsNaN(expected) ? Half.IsNaN(actual) : BitConverter.HalfToInt16Bits(expected) == BitConverter.HalfToInt16Bits(actual),
                $"sqrt({h}) [{bits:X4}]: .NET {expected}, got {actual}");
        }
    }

    // ------------------------------------------------------------------
    // Every mode, precision and format
    // ------------------------------------------------------------------

    /// <summary>The flags IEEE says √x raises, given the result: Invalid below zero, otherwise rounding's.</summary>
    private static IeeeFlags ExpectedFlags(ApFloat x, ApFloat r, IeeeFormat? format, RoundingMode mode)
    {
        if (x.IsNaN || x.IsZero) return IeeeFlags.None;
        if (x.IsNegative) return IeeeFlags.Invalid;
        if (x.IsInfinity) return IeeeFlags.None;
        var (num, den) = SqrtOf(x, (format?.Precision ?? r.Precision) + 8);
        return RoundingFlags(num, den, r, format, mode);
    }

    /// <summary>
    /// Whether r is √x rounded to p bits (no exponent floor), judged only by squaring: x must lie
    /// between the squares of the rounding boundaries around r (the neighbours for a directed
    /// mode, the midpoints for nearest), and on a midpoint, r must be the even one.
    /// </summary>
    private static bool RoundsToRoot(ApFloat x, ApFloat r, int p, RoundingMode mode)
    {
        // r = N × 2^E with N of exactly p bits.
        BigInteger m = Oracle.ToBig(r.Significand);
        int pad = p - (int)m.GetBitLength();
        BigInteger N = m << pad;
        long E = r.Exponent - pad;
        bool bottom = N == BigInteger.One << (p - 1);     // the binade below has a finer grid

        int VsSquare(BigInteger a, long f)                // x against (a × 2^f)²
        {
            BigInteger X = Oracle.ToBig(x.Significand);
            long ex = x.Exponent, low = Math.Min(ex, 2 * f);
            return (X << (int)(ex - low)).CompareTo(a * a << (int)(2 * f - low));
        }

        switch (mode)
        {
            case RoundingMode.TowardZero:
            case RoundingMode.TowardNegative:
                return VsSquare(N, E) >= 0 && VsSquare(N + 1, E) < 0;
            case RoundingMode.TowardPositive:
                bool aboveDown = bottom ? VsSquare(2 * N - 1, E - 1) > 0 : VsSquare(N - 1, E) > 0;
                return aboveDown && VsSquare(N, E) <= 0;
            default:
                int vsLow = bottom ? VsSquare(4 * N - 1, E - 2) : VsSquare(2 * N - 1, E - 1);
                int vsHigh = VsSquare(2 * N + 1, E - 1);
                if (vsLow < 0 || vsHigh > 0) return false;
                if (vsHigh == 0) return N.IsEven;                  // a tie with the value above
                if (vsLow == 0) return bottom || N.IsEven;         // a tie with the value below
                return true;
        }
    }

    /// <summary>A positive x: random, an exact square, an exact midpoint's square (a tie), or nearly one.</summary>
    private static ApFloat RandomRadicand(Random rng, int p)
    {
        switch (rng.Next(4))
        {
            case 0:
                ApFloat s = ApFloat.Abs(RandomFinite(rng, 2, 150)).WithPrecision(rng.Next(1, 100));
                return s.IsZero ? new ApFloat(5) : ApFloat.Multiply(s, s, ApFloat.MaxPrecision);
            case 1:
                // s of p + 1 bits, odd: √(s²) sits exactly between two p-bit values.
                BigInteger mid = (BigInteger.One << p) | RandomWideField(rng, p) | 1;
                return new ApFloat(Oracle.ToAp(mid * mid), 2 * rng.Next(-150, 150), 2 * p + 2);
            case 2:
                return HardSquare(rng, rng.Next(1, 2 * p + 4), rng.Next(-150, 150));
            default:
                ApFloat x = ApFloat.Abs(RandomFinite(rng, 4, 300)).WithPrecision(rng.Next(1, 200));
                return x.IsZero ? new ApFloat(3) : x;
        }
    }

    [Fact]
    public void SqrtEveryModeAndPrecisionMatchesTheOracle()
    {
        var rng = new Random(920);
        var formats = new[] { (11, 53), (8, 24), (5, 6), (3, 4) };
        for (int i = 0; i < 6000; i++)
        {
            RoundingMode mode = Modes[rng.Next(4)];
            if (rng.Next(2) == 0)
            {
                var (w, p) = formats[rng.Next(formats.Length)];
                var format = new IeeeFormat(w, p);
                ApFloat x = RandomRadicand(rng, p);
                string at = $"sqrt({Describe(x)}) {mode} in ({w}, {p})";
                var ieee = new IeeeContext(format, mode);
                ApFloat r = ieee.Sqrt(x);
                AssertSame(ApFloat.Sqrt(x, format, mode), r, at);
                var (num, den) = SqrtOf(x, p + 8);
                Assert.True(EncodeRational(num, den, false, w, p, mode) == r.ToIeeeBits(w, p), $"{at}: got {Describe(r)}");
                Assert.True(ExpectedFlags(x, r, format, mode) == ieee.Flags, $"{at}: expected {ExpectedFlags(x, r, format, mode)}, got {ieee.Flags}");
            }
            else
            {
                int p = rng.Next(1, 200);
                ApFloat x = RandomRadicand(rng, p);
                string at = $"sqrt({Describe(x)}) {mode} at {p}";
                var ieee = new IeeeContext(p, mode);
                ApFloat r = ieee.Sqrt(x);
                AssertSame(ApFloat.Sqrt(x, p, mode), r, at);
                var (num, den) = SqrtOf(x, p + 8);
                AssertRounded(Round(num, den, p, mode), r, p, at);
                Assert.True(RoundsToRoot(x, r, p, mode), $"{at}: {Describe(r)} fails the squared boundaries");
                Assert.True(ExpectedFlags(x, r, null, mode) == ieee.Flags, $"{at}: expected {ExpectedFlags(x, r, null, mode)}, got {ieee.Flags}");
            }
        }
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    public void TinyFormatsExhaustively(int w, int p)
    {
        // Every value of a 6-bit format, every mode. With two exponent bits the smallest normal is
        // 1, so the roots of the subnormals are tiny: Underflow lives in here too.
        var format = new IeeeFormat(w, p);
        for (ulong bits = 0; bits < 1UL << (w + p); bits++)
        foreach (RoundingMode mode in Modes)
        {
            ApFloat x = ApFloat.FromIeeeBits(bits, w, p);
            var ieee = new IeeeContext(format, mode);
            ApFloat r = ieee.Sqrt(x);
            string at = $"w={w} p={p}: sqrt({Describe(x)}) {mode}";
            BigInteger expected = x.IsNaN || (x.IsNegative && !x.IsZero) ? CanonicalNaN(w, p)
                : x.IsZero ? (x.IsNegative ? BigInteger.One << (w + p - 1) : 0)
                : x.IsInfinity ? Encode(x, w, p, mode)
                : SqrtOf(x, p + 8) is var (num, den) ? EncodeRational(num, den, false, w, p, mode) : 0;
            Assert.True(expected == r.ToIeeeBits(w, p), $"{at}: expected {Decode(expected, w, p)}, got {Describe(r)}");
            Assert.True(ExpectedFlags(x, r, format, mode) == ieee.Flags, $"{at}: expected {ExpectedFlags(x, r, format, mode)}, got {ieee.Flags}");
        }
    }

    // ------------------------------------------------------------------
    // Signs, specials, and far from one
    // ------------------------------------------------------------------

    [Fact]
    public void SqrtSpecialValues()
    {
        foreach (RoundingMode mode in Modes)
        {
            foreach (IeeeContext ieee in new[] { new IeeeContext(IeeeFormat.Binary64, mode), new IeeeContext(80, mode) })
            {
                AssertSame(ApFloat.NegativeZero, ieee.Sqrt(ApFloat.NegativeZero));    // √(−0) is −0, and valid
                AssertSame(ApFloat.Zero, ieee.Sqrt(ApFloat.Zero));
                AssertSame(ApFloat.PositiveInfinity, ieee.Sqrt(ApFloat.PositiveInfinity));
                Assert.True(ieee.Sqrt(ApFloat.NaN).IsNaN);
                AssertSame(new ApFloat(3), ieee.Sqrt(9));
                Assert.Equal(IeeeFlags.None, ieee.Flags);

                Assert.True(ieee.Sqrt(ApFloat.NegativeInfinity).IsNaN);
                Assert.Equal(IeeeFlags.Invalid, ieee.Flags);
                ieee.ClearFlags();
                Assert.True(ieee.Sqrt(-4).IsNaN);
                AssertSame(new ApFloat(2), ieee.Sqrt(4));
                Assert.Equal(IeeeFlags.Invalid, ieee.Flags);                        // sticky
                ieee.ClearFlags();
                Assert.True(ieee.Sqrt(-double.Epsilon).IsNaN);
                Assert.Equal(IeeeFlags.Invalid, ieee.Flags);
                ieee.ClearFlags();
                ieee.Sqrt(2);
                Assert.Equal(IeeeFlags.Inexact, ieee.Flags);
                Assert.Equal(ieee.Precision, ieee.Sqrt(2).Precision);
            }
        }
        Assert.Equal(53, ApFloat.Sqrt(2.0).Precision);                     // x's own precision
        Assert.Equal(ApFloat.DefaultPrecision, ApFloat.Sqrt(ApFloat.NaN).Precision);
    }

    [Fact]
    public void SqrtFarFromOne()
    {
        // √(m × 2^2k) is √m × 2^k, however big k is, and an odd exponent is √(2m) × 2^k.
        var rng = new Random(930);
        foreach (long k in new[] { 1_000_000_000_000L, -1_000_000_000_000L, 123_456_789L })
        {
            ApFloat m = ApFloat.Abs(RandomFinite(rng, 3, 0)).WithPrecision(100);
            if (m.IsZero) m = new ApFloat(7);
            ApFloat near = ApFloat.Sqrt(m, 64), far = ApFloat.Sqrt(new ApFloat(m.Significand, m.Exponent + 2 * k, 100), 64);
            Assert.Equal(near.Significand, far.Significand);
            Assert.Equal(near.Exponent + k, far.Exponent);
            ApFloat odd = ApFloat.Sqrt(new ApFloat(m.Significand, m.Exponent + 2 * k + 1, 100), 64);
            ApFloat twice = ApFloat.Sqrt(m * 2, 64);
            Assert.Equal(twice.Significand, odd.Significand);
            Assert.Equal(twice.Exponent + k, odd.Exponent);
        }

        // Into binary64 from outside its range: overflow, an exact subnormal, a tiny one, and zero.
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        Assert.Equal(double.PositiveInfinity, (double)ieee.Sqrt(new ApFloat(3, 2100)));
        Assert.Equal(IeeeFlags.Overflow | IeeeFlags.Inexact, ieee.Flags);
        ieee.ClearFlags();
        Assert.Equal(Math.ScaleB(1, -1050), (double)ieee.Sqrt(new ApFloat(1, -2100)));     // exact, so no Underflow
        Assert.Equal(IeeeFlags.None, ieee.Flags);
        var tiny = new ApFloat(3, -2100);
        var (num, den) = SqrtOf(tiny, 60);
        Assert.Equal(EncodeRational(num, den, false, 11, 53, RoundingMode.ToNearestEven), (BigInteger)ieee.Sqrt(tiny).ToIeeeBits(11, 53));
        Assert.Equal(IeeeFlags.Underflow | IeeeFlags.Inexact, ieee.Flags);
        ieee.ClearFlags();
        Assert.Equal(0.0, (double)ieee.Sqrt(new ApFloat(1, -3000)));
        Assert.Equal(IeeeFlags.Underflow | IeeeFlags.Inexact, ieee.Flags);
        ieee.ClearFlags();
        ieee.Mode = RoundingMode.TowardPositive;
        Assert.Equal(double.Epsilon, (double)ieee.Sqrt(new ApFloat(1, -3000)));
        ieee.Mode = RoundingMode.TowardZero;
        Assert.Equal(double.MaxValue, (double)ieee.Sqrt(new ApFloat(3, 2100)));
    }

    [Fact]
    public void SqrtOfAHighPrecisionValueAtALowPrecision()
    {
        // The radicand's extra bits are folded into the sticky bit, so √ of a 100,000-bit value
        // to 53 bits costs what a 53-bit root costs, and still rounds right: √(1 + 2^-99999) is
        // about 1 + 2^-100000, which is 1 to nearest and toward zero, and the next double up.
        var x = new ApFloat((ApInt.One << 99_999) + 1, -99_999, 100_000);
        Assert.Equal(1.0, (double)ApFloat.Sqrt(x, IeeeFormat.Binary64));
        Assert.Equal(1.0, (double)ApFloat.Sqrt(x, IeeeFormat.Binary64, RoundingMode.TowardZero));
        Assert.Equal(Math.BitIncrement(1.0), (double)ApFloat.Sqrt(x, IeeeFormat.Binary64, RoundingMode.TowardPositive));
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        ieee.Sqrt(x);
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);

        // And a long square is still exact: 3^20000 has 31,699 bits, its root 15,850.
        var square = new ApFloat(Oracle.ToAp(BigInteger.Pow(3, 20_000)), 0, 32_000);
        ieee = new IeeeContext(16_000);
        AssertSame(new ApFloat(Oracle.ToAp(BigInteger.Pow(3, 10_000)), 0, 16_000), ieee.Sqrt(square));
        Assert.Equal(IeeeFlags.None, ieee.Flags);
    }
}

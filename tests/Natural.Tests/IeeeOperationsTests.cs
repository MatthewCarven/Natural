using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// Session 6b's operations: fused multiply-add, rounding to an integral value, and the
/// remainder. The hardware (Math.FusedMultiplyAdd, Math.Floor and friends,
/// double.Ieee754Remainder) is the oracle bit for bit in binary64 and binary32; exact rationals
/// are the oracle in every mode and at every precision.
/// </summary>
public class IeeeOperationsTests
{
    private static ApFloat Bits64(ulong bits) => ApFloat.FromIeeeBits(bits, 11, 53);

    private static bool SameDouble(double expected, double actual) =>
        double.IsNaN(expected) ? double.IsNaN(actual) : BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual);

    private static bool SameSingle(float expected, float actual) =>
        float.IsNaN(expected) ? float.IsNaN(actual) : BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual);

    // ------------------------------------------------------------------
    // Fused multiply-add
    // ------------------------------------------------------------------

    /// <summary>An addend for a × b: unrelated, cancelling the product nearly or exactly, far below it, or a zero.</summary>
    private static ulong Addend(Random rng, double a, double b, int w, int p)
    {
        return rng.Next(5) switch
        {
            0 => RandomIeeeBits(rng, w, p),
            1 => (ulong)BitConverter.DoubleToInt64Bits(-(a * b)) ^ RandomField(rng, rng.Next(1, 8)),
            2 => (ulong)BitConverter.DoubleToInt64Bits(-(a * b)),
            3 => (ulong)BitConverter.DoubleToInt64Bits((a * b) * Math.ScaleB(1, -rng.Next(40, 120))),
            _ => rng.Next(2) == 0 ? 0UL : 1UL << 63,
        };
    }

    [Fact]
    public void FmaIsTheHardwaresInBinary64()
    {
        var rng = new Random(800);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            double a = BitConverter.Int64BitsToDouble((long)aBits), b = BitConverter.Int64BitsToDouble((long)bBits);
            double c = BitConverter.Int64BitsToDouble((long)Addend(rng, a, b, 11, 53));
            double expected = Math.FusedMultiplyAdd(a, b, c);
            double actual = (double)ApFloat.FusedMultiplyAdd(a, b, c, IeeeFormat.Binary64);
            Assert.True(SameDouble(expected, actual), $"fma({a:R}, {b:R}, {c:R}): hardware {expected:R}, got {actual:R}");
        }
    }

    [Fact]
    public void FmaIsTheHardwaresInBinary32()
    {
        var rng = new Random(801);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 8, 24);
            float a = BitConverter.Int32BitsToSingle((int)aBits), b = BitConverter.Int32BitsToSingle((int)bBits);
            float c = rng.Next(3) switch
            {
                0 => BitConverter.Int32BitsToSingle((int)RandomIeeeBits(rng, 8, 24)),
                1 => -(a * b),
                _ => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(-(a * b)) ^ (int)RandomField(rng, 6)),
            };
            float expected = MathF.FusedMultiplyAdd(a, b, c);
            float actual = (float)ApFloat.FusedMultiplyAdd(a, b, c, IeeeFormat.Binary32);
            Assert.True(SameSingle(expected, actual), $"fma({a:R}, {b:R}, {c:R}): hardware {expected:R}, got {actual:R}");
        }
    }

    [Fact]
    public void FmaRoundsOnce()
    {
        // (1 + 2^-30)² = 1 + 2^-29 + 2^-60, which a double holds as 1 + 2^-29. The fused form
        // hands back the 2^-60 that rounding the product threw away.
        double x = 1 + Math.ScaleB(1, -30), product = x * x;
        AssertSame(new ApFloat(ApInt.One, -60), ApFloat.FusedMultiplyAdd(x, x, -product, IeeeFormat.Binary64));
        Assert.Equal(0.0, x * x - product);
    }

    [Fact]
    public void FmaEveryModeAndPrecisionMatchesTheOracle()
    {
        var rng = new Random(802);
        var formats = new[] { (11, 53), (8, 24), (5, 6) };
        for (int i = 0; i < 6000; i++)
        {
            ApFloat a = RandomFinite(rng, 4, 300).WithPrecision(rng.Next(1, 120));
            ApFloat b = RandomFinite(rng, 4, 300).WithPrecision(rng.Next(1, 120));
            ApFloat c = rng.Next(3) == 0
                ? -ApFloat.Multiply(a, b, rng.Next(1, 200), Modes[rng.Next(4)])          // near cancellation
                : RandomFinite(rng, 4, 600).WithPrecision(rng.Next(1, 200));
            RoundingMode mode = Modes[rng.Next(4)];
            var (an, ad) = ToRational(a);
            var (bn, bd) = ToRational(b);
            var (cn, cd) = ToRational(c);
            BigInteger num = an * bn * cd + cn * ad * bd, den = ad * bd * cd;
            // IEEE §6.3: an exact zero is +0 (-0 rounding down), unless the product and c are zeros of one sign.
            bool productNegative = a.IsNegative ^ b.IsNegative;
            bool zeroSign = (a.IsZero || b.IsZero) && c.IsZero && productNegative == c.IsNegative
                ? c.IsNegative
                : mode == RoundingMode.TowardNegative;
            string at = $"fma({Describe(a)}, {Describe(b)}, {Describe(c)}) {mode}";

            if (rng.Next(2) == 0)
            {
                var (w, p) = formats[rng.Next(formats.Length)];
                var format = new IeeeFormat(w, p);
                var ieee = new IeeeContext(format, mode);
                ApFloat r = ieee.FusedMultiplyAdd(a, b, c);
                AssertSame(ApFloat.FusedMultiplyAdd(a, b, c, format, mode), r, at);
                Assert.True(EncodeRational(num, den, zeroSign, w, p, mode) == r.ToIeeeBits(w, p), $"{at} in ({w}, {p}): {Describe(r)}");
                Assert.Equal(RoundingFlags(num, den, r, format, mode), ieee.Flags);
            }
            else
            {
                int p = rng.Next(1, 200);
                var ieee = new IeeeContext(p, mode);
                ApFloat r = ieee.FusedMultiplyAdd(a, b, c);
                AssertSame(ApFloat.FusedMultiplyAdd(a, b, c, p, mode), r, at);
                AssertRounded(Round(num, den, p, mode) with { Negative = num.IsZero ? zeroSign : num.Sign < 0 }, r, p, at);
                Assert.Equal(RoundingFlags(num, den, r, null, mode), ieee.Flags);
            }
        }
    }

    [Fact]
    public void FmaSpecialValues()
    {
        double[] specials = [0.0, -0.0, 1.0, -1.0, 1.5, double.PositiveInfinity, double.NegativeInfinity, double.NaN,
                             double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon];
        foreach (double a in specials)
        foreach (double b in specials)
        foreach (double c in specials)
        foreach (RoundingMode mode in Modes)
        {
            var ieee = new IeeeContext(IeeeFormat.Binary64, mode);
            ApFloat r = ieee.FusedMultiplyAdd(a, b, c);
            if (mode == RoundingMode.ToNearestEven)
                Assert.True(SameDouble(Math.FusedMultiplyAdd(a, b, c), (double)r), $"fma({a}, {b}, {c}): got {(double)r}");

            // 0 × ∞ is invalid (but NaN anywhere in goes through quietly), and so is an infinite
            // product meeting the opposite infinity.
            bool anyNaN = double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c);
            bool zeroTimesInfinity = (a == 0 && double.IsInfinity(b)) || (double.IsInfinity(a) && b == 0);
            bool productInfinite = (double.IsInfinity(a) || double.IsInfinity(b)) && !zeroTimesInfinity;
            bool productNegative = double.IsNegative(a) ^ double.IsNegative(b);
            bool infinitiesCancel = productInfinite && double.IsInfinity(c) && productNegative != double.IsNegative(c);
            bool invalid = !anyNaN && (zeroTimesInfinity || infinitiesCancel);
            string at = $"fma({a}, {b}, {c}) {mode}";
            Assert.True(invalid == ieee.Flags.HasFlag(IeeeFlags.Invalid), $"{at}: flags {ieee.Flags}");
            Assert.True(anyNaN || invalid ? r.IsNaN : !r.IsNaN, $"{at}: got {Describe(r)}");
        }
    }

    // ------------------------------------------------------------------
    // Rounding to an integral value
    // ------------------------------------------------------------------

    private static readonly MidpointRounding[] Midpoints =
    [
        MidpointRounding.ToEven, MidpointRounding.AwayFromZero, MidpointRounding.ToZero,
        MidpointRounding.ToNegativeInfinity, MidpointRounding.ToPositiveInfinity,
    ];

    /// <summary>A double near an integer or a half: k + f, with k up to 2^56 and f a fraction that matters.</summary>
    private static double NearIntegers(Random rng)
    {
        double k = Math.Floor(Math.ScaleB(rng.NextDouble(), rng.Next(0, 57)));
        double f = rng.Next(6) switch
        {
            0 => 0,
            1 => 0.5,
            2 => rng.Next(2) == 0 ? 0.25 : 0.75,
            3 => Math.BitDecrement(0.5),
            4 => Math.BitIncrement(0.5),
            _ => rng.NextDouble(),
        };
        double x = k + f;
        return rng.Next(2) == 0 ? -x : x;
    }

    [Fact]
    public void RoundingMatchesTheHardware()
    {
        var rng = new Random(810);
        var values = new List<double> { 0.0, -0.0, 0.5, -0.5, 1.5, -1.5, 2.5, -2.5, 0.49999999999999994, -0.4,
                                        double.Epsilon, -double.Epsilon, double.MaxValue, double.PositiveInfinity, double.NaN };
        for (int i = 0; i < 6000; i++) values.Add(NearIntegers(rng));
        for (int i = 0; i < 3000; i++) values.Add(BitConverter.Int64BitsToDouble((long)RandomIeeeBits(rng, 11, 53, 0, 1, 51, 52, 53)));

        foreach (double d in values)
        {
            ApFloat x = d;
            Check(Math.Floor(d), ApFloat.Floor(x), "Floor");
            Check(Math.Ceiling(d), ApFloat.Ceiling(x), "Ceiling");
            Check(Math.Truncate(d), ApFloat.Truncate(x), "Truncate");
            Check(Math.Round(d), ApFloat.Round(x), "Round");
            foreach (MidpointRounding m in Midpoints) Check(Math.Round(d, m), ApFloat.Round(x, m), $"Round {m}");

            void Check(double expected, ApFloat actual, string what)
            {
                Assert.True(SameDouble(expected, (double)actual), $"{what}({d:R}): hardware {expected:R}, got {(double)actual:R}");
                Assert.Equal(53, actual.Precision);
            }
        }
    }

    [Fact]
    public void RoundingAtAnyPrecisionMatchesTheOracle()
    {
        var rng = new Random(811);
        for (int i = 0; i < 4000; i++)
        {
            ApFloat x = RandomFinite(rng, 4, 200).WithPrecision(rng.Next(1, 150), Modes[rng.Next(4)]);
            var (num, den) = ToRational(x);
            BigInteger floor = BigInteger.DivRem(num, den, out BigInteger rem);
            if (rem.Sign < 0) floor -= 1;
            BigInteger twiceFraction = 2 * (num - floor * den);                 // 2 × (x − floor), over den: 0 ≤ … < 2
            int vsHalf = twiceFraction.CompareTo(den);
            bool exact = num == floor * den;
            BigInteger ceiling = exact ? floor : floor + 1;
            BigInteger truncate = x.IsNegative ? ceiling : floor;
            BigInteger nearestEven = vsHalf < 0 || (vsHalf == 0 && floor.IsEven) ? floor : floor + 1;
            BigInteger awayFromZero = vsHalf < 0 || (vsHalf == 0 && x.IsNegative) ? floor : floor + 1;

            Check(floor, ApFloat.Floor(x), "Floor");
            Check(ceiling, ApFloat.Ceiling(x), "Ceiling");
            Check(truncate, ApFloat.Truncate(x), "Truncate");
            Check(nearestEven, ApFloat.Round(x), "Round");
            Check(awayFromZero, ApFloat.Round(x, MidpointRounding.AwayFromZero), "Round away");

            void Check(BigInteger expected, ApFloat actual, string what)
            {
                string at = $"{what}({Describe(x)} at {x.Precision})";
                var (an, ad) = ToRational(actual);
                Assert.True(ad == 1 && an == expected, $"{at}: expected {expected}, got {Describe(actual)}");
                if (expected.IsZero) Assert.True(actual.IsZero && actual.IsNegative == x.IsNegative, $"{at}: a zero keeps x's sign");
                Assert.Equal(x.Precision, actual.Precision);
                Assert.True(actual.Significand.BitLength <= actual.Precision, $"{at}: more bits than its precision");
            }
        }
    }

    [Fact]
    public void RoundingFarFromOne()
    {
        // Integral already, however big: returned as it is, with no work.
        var huge = new ApFloat(3, 1_000_000_000_000);
        AssertSame(huge, ApFloat.Floor(huge));
        AssertSame(huge, ApFloat.Round(huge, MidpointRounding.AwayFromZero));

        // A tiny value rounds to a signed zero, or to ±1 outward.
        var tiny = new ApFloat(3, -1_000_000_000_000);
        AssertSame(ApFloat.Zero, ApFloat.Floor(tiny));
        AssertSame(new ApFloat(1), ApFloat.Ceiling(tiny));
        AssertSame(ApFloat.NegativeZero, ApFloat.Round(-tiny));
        AssertSame(ApFloat.NegativeZero, ApFloat.Round(-tiny, MidpointRounding.AwayFromZero));
        AssertSame(new ApFloat(-1), ApFloat.Floor(-tiny));

        // NaN and the infinities are their own integral values.
        Assert.True(ApFloat.Floor(ApFloat.NaN).IsNaN);
        AssertSame(ApFloat.NegativeInfinity, ApFloat.Ceiling(ApFloat.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApFloat.Round(1.5, (MidpointRounding)9));
    }

    [Fact]
    public void RoundToIntegralInAContext()
    {
        var up = new IeeeContext(IeeeFormat.Binary64, RoundingMode.TowardPositive);
        AssertSame(new ApFloat(3), up.RoundToIntegral(2.5));
        Assert.Equal(IeeeFlags.Inexact, up.Flags);                    // IEEE's roundToIntegralExact

        var exact = new IeeeContext(IeeeFormat.Binary64);
        AssertSame(new ApFloat(3), exact.RoundToIntegral(3.0));
        Assert.True(exact.RoundToIntegral(double.NaN).IsNaN);
        AssertSame(ApFloat.NegativeInfinity, exact.RoundToIntegral(double.NegativeInfinity));
        AssertSame(ApFloat.NegativeZero, exact.RoundToIntegral(-0.0));
        Assert.Equal(IeeeFlags.None, exact.Flags);

        // An integer with more bits than the target holds: exact as an integer, inexact in binary64.
        var big = new ApFloat((ApInt.One << 60) + 1, 0, 256);
        AssertSame(new ApFloat(ApInt.One, 60), exact.RoundToIntegral(big));
        Assert.Equal(IeeeFlags.Inexact, exact.Flags);
    }

    // ------------------------------------------------------------------
    // The remainder
    // ------------------------------------------------------------------

    [Fact]
    public void RemainderIsTheHardwaresInBinary64()
    {
        var rng = new Random(820);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            double a = BitConverter.Int64BitsToDouble((long)aBits), b = BitConverter.Int64BitsToDouble((long)bBits);
            double expected = double.Ieee754Remainder(a, b);
            ApFloat r = ApFloat.Ieee754Remainder(a, b);
            Assert.True(SameDouble(expected, (double)r), $"remainder({a:R}, {b:R}): .NET {expected:R}, got {Describe(r)}");
        }
    }

    [Theory]
    [InlineData(5.0, 2.0, 1.0)]        // 2.5: n = 2, the even one
    [InlineData(7.0, 2.0, -1.0)]       // 3.5: n = 4
    [InlineData(-5.0, 2.0, -1.0)]
    [InlineData(-7.0, 2.0, 1.0)]
    [InlineData(4.0, 2.0, 0.0)]
    [InlineData(-4.0, 2.0, -0.0)]      // a zero has x's sign
    [InlineData(-0.0, 3.0, -0.0)]
    [InlineData(1.0, double.PositiveInfinity, 1.0)]
    [InlineData(10.0, 3.0, 1.0)]
    [InlineData(11.0, 3.0, -1.0)]
    public void RemainderKnownValues(double x, double y, double expected) =>
        Assert.True(SameDouble(expected, (double)ApFloat.Ieee754Remainder(x, y)), $"remainder({x}, {y})");

    [Fact]
    public void RemainderAtAnyPrecisionMatchesTheOracle()
    {
        var rng = new Random(821);
        for (int i = 0; i < 4000; i++)
        {
            ApFloat x = RandomFinite(rng, 4, 300).WithPrecision(rng.Next(1, 150));
            ApFloat y = RandomFinite(rng, 4, 300).WithPrecision(rng.Next(1, 150));
            if (y.IsZero) continue;
            var (xn, xd) = ToRational(x);
            var (yn, yd) = ToRational(y);
            // n = x / y to the nearest integer, ties to even; r = x - n·y, exactly.
            BigInteger qn = xn * yd, qd = xd * yn;
            if (qd.Sign < 0) (qn, qd) = (-qn, -qd);
            BigInteger floor = BigInteger.DivRem(qn, qd, out BigInteger rem);
            if (rem.Sign < 0) floor -= 1;
            int vsHalf = (2 * (qn - floor * qd)).CompareTo(qd);
            BigInteger n = vsHalf < 0 || (vsHalf == 0 && floor.IsEven) ? floor : floor + 1;
            BigInteger rn = xn * yd - n * yn * xd, rd = xd * yd;

            ApFloat r = ApFloat.Ieee754Remainder(x, y);
            string at = $"remainder({Describe(x)}, {Describe(y)})";
            var (an, ad) = ToRational(r);
            Assert.True(an * rd == rn * ad, $"{at}: got {Describe(r)}");
            if (rn.IsZero) Assert.True(r.IsZero && r.IsNegative == x.IsNegative, $"{at}: a zero has x's sign");
            Assert.Equal(Math.Max(x.Precision, y.Precision), r.Precision);
            Assert.True(r.Significand.BitLength <= r.Precision, $"{at}: more bits than its precision");
            Assert.True(BigInteger.Abs(2 * rn * yd) <= BigInteger.Abs(yn * rd), $"{at}: more than half of y");
        }
    }

    [Fact]
    public void RemainderAcrossHugeExponentGaps()
    {
        var rng = new Random(822);
        for (int i = 0; i < 40; i++)
        {
            BigInteger m = Oracle.RandomBig(rng, 3, allowNegative: false) | 1;
            BigInteger yOdd = Oracle.RandomBig(rng, 2, allowNegative: false) | 1;
            var y = new ApFloat(Oracle.ToAp(yOdd), 0, 64);

            // 2^100000 · m: small enough to build in BigInteger and divide outright.
            const int k = 100_000;
            var x = new ApFloat(Oracle.ToAp(m), k, 96);
            BigInteger X = m << k, rem = BigInteger.Remainder(X, yOdd);
            BigInteger expected = 2 * rem > yOdd || (2 * rem == yOdd && !((X / yOdd).IsEven)) ? rem - yOdd : rem;
            var (an, ad) = ToRational(ApFloat.Ieee754Remainder(x, y));
            Assert.True(ad == 1 && an == expected, $"remainder(m·2^{k}, {yOdd}): expected {expected}, got {an}/{ad}");

            // 2^(10^12) · m: far too big to build. Check what defines it: r ≡ X (mod y), |r| ≤ y/2.
            var far = new ApFloat(Oracle.ToAp(m), 1_000_000_000_000, 96);
            var (fn, fd) = ToRational(ApFloat.Ieee754Remainder(far, y));
            BigInteger xModY = m % yOdd * BigInteger.ModPow(2, 1_000_000_000_000, yOdd) % yOdd;
            Assert.True(fd == 1 && BigInteger.Abs(2 * fn) <= yOdd && ((fn - xModY) % yOdd).IsZero,
                $"remainder(m·2^(10^12), {yOdd}) = {fn}: not x mod y, or more than half of y");
        }
    }

    [Fact]
    public void RemainderInAContext()
    {
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        AssertSame(new ApFloat(-1), ieee.Ieee754Remainder(7.0, 2.0));
        Assert.Equal(IeeeFlags.None, ieee.Flags);                         // always exact
        Assert.True(ieee.Ieee754Remainder(double.PositiveInfinity, 2.0).IsNaN);
        Assert.Equal(IeeeFlags.Invalid, ieee.Flags);
        ieee.ClearFlags();
        Assert.True(ieee.Ieee754Remainder(1.0, -0.0).IsNaN);
        Assert.Equal(IeeeFlags.Invalid, ieee.Flags);
        ieee.ClearFlags();
        Assert.True(ieee.Ieee754Remainder(double.NaN, 0.0).IsNaN);           // NaN first: quiet
        Assert.Equal(IeeeFlags.None, ieee.Flags);

        // Exact, but wider than the target: inexact once it's in binary32.
        var narrow = new IeeeContext(IeeeFormat.Binary32);
        narrow.Ieee754Remainder(new ApFloat((ApInt.One << 40) + 1, 0, 64), new ApFloat(ApInt.One, 45, 64));
        Assert.Equal(IeeeFlags.Inexact, narrow.Flags);
    }
}

using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>IEEE 754's special cases (§6–7): NaN, infinities, signed zeros.</summary>
public class ApFloatSpecialTests
{
    private static readonly double[] Specials =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0,
        1, -1, 2, 0.5, -3, 0.1,
        double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon,
        BitConverter.Int64BitsToDouble(0x0010000000000000),    // the smallest normal
        -BitConverter.Int64BitsToDouble(0x000FFFFFFFFFFFFF),   // the largest subnormal
    ];

    [Fact]
    public void EveryPairMatchesDouble()
    {
        // Every combination through all four operators, bit for bit (NaN as NaN).
        foreach (double a in Specials)
            foreach (double b in Specials)
            {
                ApFloat x = a, y = b;
                AssertSameDouble(a + b, x + y, $"{a:R} + {b:R}");
                AssertSameDouble(a - b, x - y, $"{a:R} - {b:R}");
                AssertSameDouble(a * b, x * y, $"{a:R} * {b:R}");
                AssertSameDouble(a / b, x / y, $"{a:R} / {b:R}");
            }
    }

    [Fact]
    public void ComparisonsMatchDouble()
    {
        // NaN is unordered: every comparison with it is false except !=. And -0 == +0.
        foreach (double a in Specials)
            foreach (double b in Specials)
            {
                ApFloat x = a, y = b;
                string context = $"{a:R} vs {b:R}";
                Assert.True((a == b) == (x == y), context + " ==");
                Assert.True((a != b) == (x != y), context + " !=");
                Assert.True((a < b) == (x < y), context + " <");
                Assert.True((a > b) == (x > y), context + " >");
                Assert.True((a <= b) == (x <= y), context + " <=");
                Assert.True((a >= b) == (x >= y), context + " >=");
                Assert.True(Math.Sign(a.CompareTo(b)) == Math.Sign(x.CompareTo(y)), context + " CompareTo");
                Assert.True(a.Equals(b) == x.Equals(y), context + " Equals");
                if (x.Equals(y)) Assert.True(x.GetHashCode() == y.GetHashCode(), context + " GetHashCode");
            }
    }

    [Theory]
    [InlineData(RoundingMode.ToNearestEven)]
    [InlineData(RoundingMode.TowardZero)]
    [InlineData(RoundingMode.TowardPositive)]
    [InlineData(RoundingMode.TowardNegative)]
    public void SignedZeros(RoundingMode mode)
    {
        // An exact zero sum of opposite-signed operands is +0, except rounding toward -∞,
        // where it's -0. Zeros of the same sign keep it. Products and quotients XOR the signs.
        ApFloat pz = ApFloat.Zero, nz = ApFloat.NegativeZero, three = 3;
        bool down = mode == RoundingMode.TowardNegative;

        AssertZero(down, ApFloat.Subtract(three, three, 53, mode), "3 - 3");
        AssertZero(down, ApFloat.Add(three, -three, 53, mode), "3 + -3");
        AssertZero(down, ApFloat.Add(pz, nz, 53, mode), "+0 + -0");
        AssertZero(down, ApFloat.Add(nz, pz, 53, mode), "-0 + +0");
        AssertZero(down, ApFloat.Subtract(pz, pz, 53, mode), "+0 - +0");
        AssertZero(false, ApFloat.Add(pz, pz, 53, mode), "+0 + +0");
        AssertZero(true, ApFloat.Add(nz, nz, 53, mode), "-0 + -0");
        AssertZero(true, ApFloat.Subtract(nz, pz, 53, mode), "-0 - +0");
        AssertZero(false, ApFloat.Subtract(pz, nz, 53, mode), "+0 - -0");

        AssertZero(true, ApFloat.Multiply(nz, three, 53, mode), "-0 * 3");
        AssertZero(true, ApFloat.Multiply(pz, -three, 53, mode), "+0 * -3");
        AssertZero(false, ApFloat.Multiply(nz, -three, 53, mode), "-0 * -3");
        AssertZero(true, ApFloat.Divide(nz, three, 53, mode), "-0 / 3");
        AssertZero(false, ApFloat.Divide(nz, -three, 53, mode), "-0 / -3");
        AssertZero(true, ApFloat.Divide(three, ApFloat.NegativeInfinity, 53, mode), "3 / -inf");

        // x + 0 is x, rounded (and still x's sign, not the zero's).
        AssertSame(ApFloat.Divide(-1, 3, 20, mode), ApFloat.Add(ApFloat.Divide(-1, 3, 60, mode), pz, 20, mode));
        AssertSame(new ApFloat(-3), ApFloat.Add(nz, -three, 53, mode));
        AssertSame(new ApFloat(3), ApFloat.Add(nz, three, 53, mode));
    }

    [Fact]
    public void NaNAndInfinityRules()
    {
        ApFloat inf = ApFloat.PositiveInfinity, ninf = ApFloat.NegativeInfinity, zero = ApFloat.Zero, nan = ApFloat.NaN;

        Assert.True((inf - inf).IsNaN);
        Assert.True((inf + ninf).IsNaN);
        Assert.True((zero * inf).IsNaN);
        Assert.True((ninf * ApFloat.NegativeZero).IsNaN);
        Assert.True((zero / zero).IsNaN);
        Assert.True((inf / ninf).IsNaN);
        AssertSame(ninf, ninf - inf);
        AssertSame(inf, ninf * ninf);
        AssertSame(ninf, new ApFloat(-1) / zero);
        AssertSame(inf, new ApFloat(-1) / ApFloat.NegativeZero);

        // NaN has no sign and never equals anything, itself included (but Equals says it
        // does, as double.Equals does, so it can be found in a collection).
        ApFloat itself = nan;
        Assert.False((-nan).IsNegative);
        Assert.False(nan == itself);
        Assert.True(nan != itself);
        Assert.True(nan.Equals(itself));
        Assert.True(nan.CompareTo(ninf) < 0);

        // Precision rides along on the special values too.
        Assert.Equal(80, ApFloat.Add(inf, 1, 80).Precision);
        Assert.Equal(80, ApFloat.Divide(1, zero, 80).Precision);
        Assert.Equal(80, ApFloat.Multiply(nan, 1, 80).Precision);
    }

    private static void AssertZero(bool negative, ApFloat actual, string context) =>
        Assert.True(actual.IsZero && actual.IsNegative == negative,
            $"{context}: expected {(negative ? "-0" : "+0")}, got {Describe(actual)}");

    internal static void AssertSameDouble(double expected, ApFloat actual, string context)
    {
        double got = (double)actual;
        bool same = double.IsNaN(expected)
            ? double.IsNaN(got) && actual.IsNaN
            : BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(got);
        Assert.True(same, $"{context}: expected {expected:R} ({BitConverter.DoubleToInt64Bits(expected):X16}), " +
                          $"got {got:R} ({BitConverter.DoubleToInt64Bits(got):X16}) from {Describe(actual)}");
    }
}

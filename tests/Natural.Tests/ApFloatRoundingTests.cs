using System.Numerics;
using static Natural.Tests.FloatOracle;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

/// <summary>ApFloat's rounding against the rational oracle: random operands, precision 1..200, all four modes.</summary>
public class ApFloatRoundingTests
{
    private const int Rounds = 3000;

    [Fact]
    public void ConstructorRoundsCorrectly()
    {
        var rng = new Random(20);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger m = RandomBig(rng, 8);
            long e = rng.Next(-300, 301);
            int p = rng.Next(1, 201);
            RoundingMode mode = Modes[rng.Next(4)];
            var (num, den) = e >= 0 ? (m << (int)e, BigInteger.One) : (m, BigInteger.One << (int)-e);
            AssertRounded(Round(num, den, p, mode), new ApFloat(ToAp(m), e, p, mode), p,
                $"new ApFloat(0x{m:X}, {e}, {p}, {mode})");
        }
    }

    [Fact]
    public void WithPrecisionRoundsCorrectly()
    {
        var rng = new Random(21);
        for (int i = 0; i < Rounds; i++)
        {
            ApFloat x = RandomFinite(rng, 8, 300);
            int p = rng.Next(1, 201);
            RoundingMode mode = Modes[rng.Next(4)];
            var (num, den) = ToRational(x);
            Rounded expected = Round(num, den, p, mode) with { Negative = x.IsNegative };
            AssertRounded(expected, x.WithPrecision(p, mode), p, $"{Describe(x)}.WithPrecision({p}, {mode})");
        }
    }

    [Theory]
    [InlineData('+')]
    [InlineData('-')]
    [InlineData('*')]
    [InlineData('/')]
    public void ArithmeticRoundsCorrectly(char op)
    {
        var rng = new Random(30 + op);
        for (int i = 0; i < Rounds; i++)
        {
            ApFloat a = RandomFinite(rng, 5, 100), b = RandomFinite(rng, 5, 100);
            if (rng.Next(6) == 0)
            {
                // b close to a (or to -a): cancellation, and quotients near 1.
                BigInteger near = ToBig(a.Significand) + RandomBig(rng, 1);
                b = new ApFloat(ToAp(rng.Next(2) == 0 ? near : -near), a.Exponent, Exact);
            }
            if (op == '/' && b.IsZero) continue;   // x / 0 is in the special-case tests

            int p = rng.Next(1, 201);
            RoundingMode mode = Modes[rng.Next(4)];
            var (an, ad) = ToRational(a);
            var (bn, bd) = ToRational(b);
            var (num, den) = op switch
            {
                '+' => (an * bd + bn * ad, ad * bd),
                '-' => (an * bd - bn * ad, ad * bd),
                '*' => (an * bn, ad * bd),
                _ => (an * bd, ad * bn),
            };
            Rounded expected = Round(num, den, p, mode);
            if (expected.N.IsZero) expected = expected with { Negative = ZeroSign(op, a, b, mode) };

            ApFloat actual = op switch
            {
                '+' => ApFloat.Add(a, b, p, mode),
                '-' => ApFloat.Subtract(a, b, p, mode),
                '*' => ApFloat.Multiply(a, b, p, mode),
                _ => ApFloat.Divide(a, b, p, mode),
            };
            AssertRounded(expected, actual, p, $"{Describe(a)} {op} {Describe(b)} at {p} bits, {mode}");
        }
    }

    /// <summary>IEEE 754 §6.3: the sign of an exact zero result.</summary>
    private static bool ZeroSign(char op, ApFloat a, ApFloat b, RoundingMode mode)
    {
        if (op is '*' or '/') return a.IsNegative ^ b.IsNegative;
        bool bNegative = op == '-' ? !b.IsNegative : b.IsNegative;
        if (a.IsZero && b.IsZero && a.IsNegative == bNegative) return a.IsNegative;
        return mode == RoundingMode.TowardNegative;   // x - x, and +0 + -0
    }

    [Fact]
    public void GapShortcutMatchesExactAddition()
    {
        // Add swaps an operand wholly below both a's last bit and the rounding point for a
        // single sticky bit. Put b's leading bit just above and just below that threshold,
        // either sign, and check against the exact sum.
        var rng = new Random(40);
        for (int i = 0; i < Rounds; i++)
        {
            int p = rng.Next(1, 150);
            BigInteger am = rng.Next(4) switch
            {
                0 => BigInteger.One << rng.Next(0, 100),                // a power of two: a - tiny drops a binade
                1 => (BigInteger.One << rng.Next(1, 100)) - 1,          // all ones: a + tiny carries all the way
                _ => BigInteger.Abs(RandomBig(rng, 4)) + 1,
            };
            var a = new ApFloat(ToAp(rng.Next(2) == 0 ? am : -am), rng.Next(-50, 51), Exact);
            long topA = a.Exponent + (long)am.GetBitLength() - 1 - (long)BigInteger.TrailingZeroCount(am);
            long threshold = Math.Min(a.Exponent, topA - p - 1) - 1;

            BigInteger bm = BigInteger.Abs(RandomBig(rng, 2)) + 1;
            long topB = threshold + rng.Next(-4, 5);
            long eb = topB - ((long)bm.GetBitLength() - 1);
            var b = new ApFloat(ToAp(rng.Next(2) == 0 ? bm : -bm), eb, Exact);

            RoundingMode mode = Modes[rng.Next(4)];
            var (an, ad) = ToRational(a);
            var (bn, bd) = ToRational(b);
            Rounded expected = Round(an * bd + bn * ad, ad * bd, p, mode);
            if (expected.N.IsZero) expected = expected with { Negative = mode == RoundingMode.TowardNegative };

            string context = $"{Describe(a)} + {Describe(b)} at {p} bits, {mode} (b's top {topB - threshold:+0;-0} from the threshold)";
            AssertRounded(expected, ApFloat.Add(a, b, p, mode), p, context);
            AssertRounded(expected, ApFloat.Add(b, a, p, mode), p, context + ", swapped");
        }
    }

    [Fact]
    public void HugeGapsCostNothing()
    {
        // 1 ± 2^-1000000: exact addition would build a million-bit number.
        ApFloat one = 1, tiny = new ApFloat(1, -1_000_000);
        AssertSame(new ApFloat(1), ApFloat.Add(one, tiny, 53));
        AssertSame(new ApFloat(1), ApFloat.Add(one, tiny, 53, RoundingMode.TowardZero));
        AssertSame(new ApFloat((1L << 52) + 1, -52), ApFloat.Add(one, tiny, 53, RoundingMode.TowardPositive));
        AssertSame(new ApFloat((1L << 53) - 1, -53), ApFloat.Subtract(one, tiny, 53, RoundingMode.TowardNegative));
        AssertSame(new ApFloat(1), ApFloat.Subtract(one, tiny, 53));
        AssertSame(new ApFloat(-((1L << 53) - 1), -53), ApFloat.Subtract(tiny, one, 53, RoundingMode.TowardPositive));
        AssertSame(new ApFloat(-1), ApFloat.Subtract(tiny, one, 53, RoundingMode.TowardNegative));
        AssertSame(new ApFloat(1, -1_000_000), ApFloat.Add(tiny, new ApFloat(1, -3_000_000), 53));
    }

    [Fact]
    public void OperatorsRoundToTheWiderOperand()
    {
        var third = ApFloat.Divide(1, 3, 20);
        Assert.Equal(20, third.Precision);
        Assert.Equal(ApFloat.DefaultPrecision, (third + 1).Precision);           // integers convert at the default
        Assert.Equal(53, (third + 1.0).Precision);                               // doubles at 53
        Assert.Equal(24, ((ApFloat)1.0f / (ApFloat)3.0f).Precision);
        Assert.Equal(60, (third * ApFloat.Divide(1, 3, 60)).Precision);
        Assert.Equal(ApFloat.DefaultPrecision, default(ApFloat).Precision);
    }
}

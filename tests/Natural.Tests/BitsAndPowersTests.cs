using System.Numerics;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

/// <summary>
/// Session 8: ApInt's bitwise operators (two's complement, as BigInteger), Pow, ModPow and
/// GreatestCommonDivisor. BigInteger is the oracle, on limb-biased operands of both signs, and
/// long's own operators are a second one where the values fit.
/// </summary>
public class BitsAndPowersTests
{
    /// <summary>Longs where two's complement goes wrong: 0, ±1, the edges of each limb, and long's own ends.</summary>
    private static readonly long[] Edges =
    [
        0, 1, -1, 2, -2, 3, -3, 5, -6, 0xFF, -0x100,
        (1L << 31) - 1, 1L << 31, -(1L << 31), -(1L << 31) - 1,
        (1L << 32) - 1, 1L << 32, -(1L << 32), -(1L << 32) + 1, (1L << 32) + 1,
        (1L << 33) + 7, -(1L << 33), (1L << 62) - 1, -(1L << 62), long.MaxValue, long.MinValue, long.MinValue + 1,
        0x5555_5555_5555_5555, unchecked((long)0xAAAA_AAAA_AAAA_AAAA),
    ];

    /// <summary>A random integer of either sign, biased like RandomBig, and sometimes exactly ±2^(32k), whose |x| − 1 borrows through every limb.</summary>
    private static BigInteger RandomOperand(Random rng)
    {
        if (rng.Next(8) == 0)
        {
            BigInteger power = BigInteger.One << (32 * rng.Next(0, 5) + rng.Next(2) * rng.Next(32));
            return rng.Next(2) == 0 ? -power : power - rng.Next(2);
        }
        return RandomBig(rng, 6);
    }

    // ------------------------------------------------------------------
    // &, |, ^, ~
    // ------------------------------------------------------------------

    [Fact]
    public void BitwiseMatchesLongOnEveryPairOfEdges()
    {
        foreach (long a in Edges)
        {
            Assert.Equal(~a, (long)~(ApInt)a);
            foreach (long b in Edges)
            {
                Assert.True((a & b) == (long)((ApInt)a & b), $"{a} & {b}");
                Assert.True((a | b) == (long)((ApInt)a | b), $"{a} | {b}");
                Assert.True((a ^ b) == (long)((ApInt)a ^ b), $"{a} ^ {b}");
            }
        }
    }

    [Fact]
    public void BitwiseMatchesBigInteger()
    {
        var rng = new Random(1100);
        for (int i = 0; i < 6000; i++)
        {
            BigInteger a = RandomOperand(rng), b = RandomOperand(rng);
            ApInt x = ToAp(a), y = ToAp(b);
            Check(a & b, x & y, "&");
            Check(a | b, x | y, "|");
            Check(a ^ b, x ^ y, "^");
            Check(~a, ~x, "~");

            void Check(BigInteger expected, ApInt actual, string op) =>
                Assert.True(expected == ToBig(actual), $"{a:X} {op} {b:X}: expected {expected:X}, got {ToBig(actual):X}");
        }
    }

    [Fact]
    public void BitwiseIdentities()
    {
        var rng = new Random(1101);
        for (int i = 0; i < 2000; i++)
        {
            ApInt x = ToAp(RandomOperand(rng)), y = ToAp(RandomOperand(rng));
            Assert.Equal(ApInt.Zero, x ^ x);
            Assert.Equal(ApInt.MinusOne, x | ~x);
            Assert.Equal(ApInt.Zero, x & ~x);
            Assert.Equal(x, ~~x);
            Assert.Equal(x, (x & y) | (x & ~y));
            Assert.Equal(x + y, (x ^ y) + ((x & y) << 1));           // an adder, in two lines
            if (!x.IsZero)
            {
                // x & -x is x's lowest set bit, whatever x's sign.
                BigInteger low = ToBig(x & -x);
                Assert.True(low > 0 && (low & (low - 1)).IsZero && !((BigInteger.Abs(ToBig(x)) & low).IsZero), $"lowest bit of {ToBig(x):X}");
            }
        }
        Assert.Equal(ApInt.MinusOne, ~ApInt.Zero);
        Assert.Equal(ApInt.Zero, ~ApInt.MinusOne);
    }

    // ------------------------------------------------------------------
    // Pow, ModPow, GreatestCommonDivisor
    // ------------------------------------------------------------------

    [Fact]
    public void PowMatchesBigInteger()
    {
        var rng = new Random(1110);
        for (int i = 0; i < 1500; i++)
        {
            BigInteger value = RandomBig(rng, 3);
            int exponent = rng.Next(0, 40);
            Assert.True(BigInteger.Pow(value, exponent) == ToBig(ApInt.Pow(ToAp(value), exponent)), $"{value}^{exponent}");
        }
        Assert.Equal(ApInt.One, ApInt.Pow(ApInt.Zero, 0));
        Assert.Equal(ApInt.Zero, ApInt.Pow(ApInt.Zero, 5));
        Assert.Equal((ApInt)(-8), ApInt.Pow(-2, 3));
        Assert.Equal((ApInt)16, ApInt.Pow(-2, 4));
        Assert.Equal(ApInt.One << 1000, ApInt.Pow(2, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApInt.Pow(2, -1));
    }

    [Fact]
    public void ModPowMatchesBigInteger()
    {
        var rng = new Random(1111);
        for (int i = 0; i < 3000; i++)
        {
            BigInteger value = RandomOperand(rng), exponent = RandomBig(rng, rng.Next(0, 3), allowNegative: false);
            BigInteger modulus = rng.Next(6) switch
            {
                0 => rng.Next(2) == 0 ? 1 : -1,
                1 => rng.Next(2, 20) * (rng.Next(2) == 0 ? 1 : -1),
                _ => RandomOperand(rng),
            };
            if (modulus.IsZero) modulus = 7;
            BigInteger expected = BigInteger.ModPow(value, exponent, modulus);
            ApInt actual = ApInt.ModPow(ToAp(value), ToAp(exponent), ToAp(modulus));
            Assert.True(expected == ToBig(actual), $"ModPow({value}, {exponent}, {modulus}): expected {expected}, got {ToBig(actual)}");
        }
    }

    [Theory]
    [InlineData(-2, 3, 5, -3)]      // the base's sign, as % (Matthew's choice: BigInteger's)
    [InlineData(-3, 2, 5, 4)]
    [InlineData(-10, 3, 5, 0)]      // and never a negative zero
    [InlineData(2, 3, -5, 3)]       // the modulus's sign ignored
    [InlineData(5, 0, 1, 0)]
    [InlineData(5, 0, -3, 1)]
    [InlineData(0, 0, 7, 1)]
    public void ModPowKnownValues(long value, long exponent, long modulus, long expected)
    {
        ApInt r = ApInt.ModPow(value, exponent, modulus);
        Assert.Equal((ApInt)expected, r);
        Assert.False(r.IsZero && r.IsNegative);
    }

    [Fact]
    public void ModPowRefusesWhatBigIntegerRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApInt.ModPow(2, -1, 5));
        Assert.Throws<DivideByZeroException>(() => ApInt.ModPow(2, 3, 0));

        // A long exponent costs its bit length in steps, not its value: 2^(2^200) mod a prime.
        BigInteger p = BigInteger.Parse("170141183460469231731687303715884105727");   // 2^127 − 1
        BigInteger e = BigInteger.One << 200;
        Assert.Equal(BigInteger.ModPow(2, e, p), ToBig(ApInt.ModPow(2, ToAp(e), ToAp(p))));
    }

    [Fact]
    public void GreatestCommonDivisorMatchesBigInteger()
    {
        var rng = new Random(1120);
        for (int i = 0; i < 3000; i++)
        {
            BigInteger a = RandomOperand(rng), b = RandomOperand(rng);
            if (rng.Next(2) == 0)
            {
                // A shared factor, and a shared power of two, so the answer isn't almost always 1.
                BigInteger c = RandomBig(rng, 2, allowNegative: false) << rng.Next(0, 70);
                a *= c;
                b *= c;
            }
            BigInteger expected = BigInteger.GreatestCommonDivisor(a, b);
            ApInt actual = ApInt.GreatestCommonDivisor(ToAp(a), ToAp(b));
            Assert.True(expected == ToBig(actual), $"gcd({a}, {b}): expected {expected}, got {ToBig(actual)}");
            Assert.False(actual.IsNegative);
        }
        Assert.Equal(ApInt.Zero, ApInt.GreatestCommonDivisor(0, 0));
        Assert.Equal((ApInt)12, ApInt.GreatestCommonDivisor(-12, 0));
        Assert.Equal((ApInt)12, ApInt.GreatestCommonDivisor(0, -12));
        Assert.Equal((ApInt)(1L << 40), ApInt.GreatestCommonDivisor(-(ApInt.One << 70), ApInt.One << 40));
    }
}

using System.Numerics;
using System.Text;
using static Adpdt.Tests.Oracle;

namespace Adpdt.Tests;

public class DivisionTests
{
    private static void CheckAgainstOracle(BigInteger a, BigInteger b)
    {
        var (q, r) = ApInt.DivRem(ToAp(a), ToAp(b));
        BigInteger expectedQ = BigInteger.DivRem(a, b, out BigInteger expectedR);   // truncating, like C#
        Assert.Equal(expectedQ, ToBig(q));
        Assert.Equal(expectedR, ToBig(r));
    }

    [Fact]
    public void MatchesCSharpTruncationOnLongs()
    {
        // The spec is "whatever C# does": check against long's own / and %, not just BigInteger.
        long[] values = [-100, -7, -6, -2, -1, 1, 2, 6, 7, 100, int.MinValue, int.MaxValue, long.MaxValue, long.MinValue + 1];
        foreach (long a in values.Append(0))
            foreach (long b in values)
            {
                Assert.Equal(a / b, (long)((ApInt)a / b));
                Assert.Equal(a % b, (long)((ApInt)a % b));
            }
    }

    [Fact]
    public void TruncatesWhereShiftFloors()
    {
        Assert.Equal((ApInt)(-3), (ApInt)(-7) / 2);
        Assert.Equal((ApInt)(-1), (ApInt)(-7) % 2);
        Assert.Equal((ApInt)(-4), (ApInt)(-7) >> 1);   // shifts floor; division doesn't
        Assert.Equal((ApInt)(-3), (ApInt)7 / -2);
        Assert.Equal((ApInt)1, (ApInt)7 % -2);          // remainder follows the dividend
    }

    [Fact]
    public void LongMinValueOverMinusOne()
    {
        // Overflows (throws) for long; an arbitrary-precision integer just gets 2^63.
        Assert.Equal(ToAp(BigInteger.One << 63), (ApInt)long.MinValue / -1);
    }

    [Fact]
    public void DivideByZeroThrows()
    {
        Assert.Throws<DivideByZeroException>(() => (ApInt)1 / 0);
        Assert.Throws<DivideByZeroException>(() => ApInt.Zero % ApInt.Zero);
        Assert.Throws<DivideByZeroException>(() => ApInt.DivRem(ApInt.Parse("123456789012345678901234567890"), default));
    }

    [Fact]
    public void Edges()
    {
        BigInteger big = BigInteger.Pow(3, 200);
        CheckAgainstOracle(0, 7);
        CheckAgainstOracle(big, 1);
        CheckAgainstOracle(big, -1);
        CheckAgainstOracle(big, big);          // quotient 1, remainder 0
        CheckAgainstOracle(big - 1, big);      // divisor just larger: quotient 0
        CheckAgainstOracle(big + 1, big);      // remainder 1
        CheckAgainstOracle(-big, big + 5);
        CheckAgainstOracle(BigInteger.One << 500, BigInteger.One << 250);
        CheckAgainstOracle((BigInteger.One << 500) - 1, (BigInteger.One << 250) - 1);   // all ones / all ones
        CheckAgainstOracle(uint.MaxValue, uint.MaxValue);
        CheckAgainstOracle(ulong.MaxValue, uint.MaxValue);
    }

    [Fact]
    public void Random()
    {
        var rng = new Random(30);
        for (int i = 0; i < 3000; i++)
        {
            BigInteger a = RandomBig(rng, 14), b = RandomBig(rng, 7);
            if (b.IsZero) continue;
            CheckAgainstOracle(a, b);
        }
    }

    [Fact]
    public void RandomSmallDivisors()
    {
        // One-limb divisors against long dividends: the running remainder stays tiny
        // while the quotient is long.
        var rng = new Random(31);
        for (int i = 0; i < 1000; i++)
        {
            BigInteger a = RandomBig(rng, 40), b = RandomBig(rng, 1);
            if (b.IsZero) continue;
            CheckAgainstOracle(a, b);
        }
    }

    [Fact]
    public void DivisionUndoesMultiplication()
    {
        var rng = new Random(32);
        for (int i = 0; i < 1000; i++)
        {
            ApInt a = ToAp(RandomBig(rng, 8)), b = ToAp(RandomBig(rng, 8));
            if (b.IsZero) continue;
            var (q, r) = ApInt.DivRem(a * b, b);
            Assert.Equal(a, q);
            Assert.Equal(ApInt.Zero, r);

            // And the defining identity, with a remainder in play.
            var (q2, r2) = ApInt.DivRem(a, b);
            Assert.Equal(a, q2 * b + r2);
            Assert.True(ApInt.Abs(r2) < ApInt.Abs(b));
            Assert.True(r2.IsZero || r2.IsNegative == a.IsNegative);
        }
    }

    [Fact]
    public void RepeatedDivisionByTenAgreesWithDoubleDabble()
    {
        // Two independent routes to the decimal digits: the shift-and-add-3 formatter,
        // and peeling digits off the bottom with % 10 and / 10.
        var rng = new Random(33);
        for (int i = 0; i < 200; i++)
        {
            ApInt n = ApInt.Abs(ToAp(RandomBig(rng, 10)));
            var digits = new StringBuilder();
            ApInt rest = n;
            do
            {
                var (q, r) = ApInt.DivRem(rest, 10);
                digits.Insert(0, (char)('0' + (long)r));
                rest = q;
            } while (!rest.IsZero);
            Assert.Equal(n.ToString(), digits.ToString());
        }
    }
}

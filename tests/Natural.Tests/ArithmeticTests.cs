using System.Numerics;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

public class ArithmeticTests
{
    private const int Rounds = 3000;

    [Fact]
    public void DefaultIsZero()
    {
        ApInt z = default;
        Assert.True(z.IsZero);
        Assert.Equal(0, z.Sign);
        Assert.Equal(ApInt.Zero, z);
        Assert.Equal(ApInt.Zero, -z);   // no negative zero
        Assert.Equal(ApInt.Zero, ApInt.One - ApInt.One);
        Assert.Equal(ApInt.Zero, ApInt.MinusOne * 0);
        Assert.False((ApInt.MinusOne * 0).IsNegative);
    }

    [Fact]
    public void CarryAcrossLimbs()
    {
        Assert.Equal((ApInt)(1UL << 32), (ApInt)uint.MaxValue + 1);
        Assert.Equal(ApInt.Parse("18446744073709551616"), (ApInt)ulong.MaxValue + 1);  // 2^64
        Assert.Equal((ApInt)ulong.MaxValue, ApInt.Parse("18446744073709551616") - 1);
    }

    [Fact]
    public void SignCombinations()
    {
        foreach (long a in new long[] { -7, -1, 0, 1, 7, long.MinValue, long.MaxValue })
            foreach (long b in new long[] { -3, -1, 0, 1, 3 })
            {
                BigInteger A = a, B = b;
                Assert.Equal(A + B, ToBig((ApInt)a + b));
                Assert.Equal(A - B, ToBig((ApInt)a - b));
                Assert.Equal(A * B, ToBig((ApInt)a * b));
            }
    }

    [Fact]
    public void Add()
    {
        var rng = new Random(2);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger a = RandomBig(rng, 12), b = RandomBig(rng, 12);
            Assert.Equal(a + b, ToBig(ToAp(a) + ToAp(b)));
        }
    }

    [Fact]
    public void Subtract()
    {
        var rng = new Random(3);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger a = RandomBig(rng, 12), b = RandomBig(rng, 12);
            Assert.Equal(a - b, ToBig(ToAp(a) - ToAp(b)));
            Assert.Equal(ApInt.Zero, ToAp(a) - ToAp(a));
        }
    }

    [Fact]
    public void Multiply()
    {
        var rng = new Random(4);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger a = RandomBig(rng, 10), b = RandomBig(rng, 10);
            Assert.Equal(a * b, ToBig(ToAp(a) * ToAp(b)));
        }
    }

    [Fact]
    public void MultiplyLopsided()
    {
        // One huge operand times a small one, both ways round.
        var rng = new Random(5);
        for (int i = 0; i < 200; i++)
        {
            BigInteger a = RandomBig(rng, 200), b = RandomBig(rng, 2);
            Assert.Equal(a * b, ToBig(ToAp(a) * ToAp(b)));
            Assert.Equal(b * a, ToBig(ToAp(b) * ToAp(a)));
        }
    }

    [Fact]
    public void MultiplyAllOnes()
    {
        // (2^n - 1)^2 = 2^2n - 2^(n+1) + 1: every partial product carries the whole way.
        for (int n = 1; n <= 300; n += 7)
        {
            BigInteger ones = (BigInteger.One << n) - 1;
            Assert.Equal(ones * ones, ToBig(ToAp(ones) * ToAp(ones)));
        }
    }

    [Fact]
    public void Factorial100()
    {
        ApInt f = 1;
        for (int i = 2; i <= 100; i++) f *= i;
        Assert.Equal(
            "93326215443944152681699238856266700490715968264381621468592963895217599993229915608941463976156518286253697920827223758251185210916864000000000000000000000000",
            f.ToString());
    }

    [Fact]
    public void Shifts()
    {
        var rng = new Random(6);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger a = RandomBig(rng, 8);
            int n = rng.Next(0, 200);
            Assert.Equal(a << n, ToBig(ToAp(a) << n));
            Assert.Equal(a >> n, ToBig(ToAp(a) >> n));   // floor semantics for negatives
            Assert.Equal(a >> n, ToBig(ToAp(a) << -n));
        }
        Assert.Equal((ApInt)(-3), (ApInt)(-5) >> 1);
        Assert.Equal(ApInt.MinusOne, (ApInt)(-1) >> 1000);
        Assert.Equal(ApInt.Zero, (ApInt)5 << int.MinValue);   // must not recurse forever
    }

    [Fact]
    public void Compare()
    {
        var rng = new Random(7);
        for (int i = 0; i < Rounds; i++)
        {
            BigInteger a = RandomBig(rng, 4), b = rng.Next(4) == 0 ? a : RandomBig(rng, 4);
            ApInt x = ToAp(a), y = ToAp(b);
            Assert.Equal(a.CompareTo(b), Math.Sign(x.CompareTo(y)));
            Assert.Equal(a == b, x == y);
            Assert.Equal(a < b, x < y);
            Assert.Equal(a >= b, x >= y);
            if (x == y) Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }
    }

    [Fact]
    public void LongConversions()
    {
        foreach (long v in new[] { 0, 1, -1, int.MinValue, int.MaxValue, long.MinValue, long.MaxValue })
            Assert.Equal(v, (long)(ApInt)v);
        Assert.Equal(ulong.MaxValue, (ulong)(ApInt)ulong.MaxValue);
        Assert.Throws<OverflowException>(() => (long)((ApInt)long.MaxValue + 1));
        Assert.Throws<OverflowException>(() => (long)((ApInt)long.MinValue - 1));
        Assert.Throws<OverflowException>(() => (ulong)(ApInt)(-1));
    }

    [Fact]
    public void AlgebraicIdentities()
    {
        var rng = new Random(8);
        for (int i = 0; i < 500; i++)
        {
            ApInt a = ToAp(RandomBig(rng, 6)), b = ToAp(RandomBig(rng, 6)), c = ToAp(RandomBig(rng, 6));
            Assert.Equal(a * (b + c), a * b + a * c);
            Assert.Equal((a - b) + b, a);
            Assert.Equal(a * b, b * a);
        }
    }
}

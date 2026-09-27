using System.Numerics;
using static Adpdt.Tests.Oracle;

namespace Adpdt.Tests;

public class TextTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("-0", "0")]
    [InlineData("000123", "123")]
    [InlineData("+42", "42")]
    [InlineData("-42", "-42")]
    [InlineData("1_000_000", "1000000")]
    [InlineData("0xFF", "255")]
    [InlineData("-0x1_0000_0000", "-4294967296")]
    [InlineData("0b1011", "11")]
    [InlineData("  99  ", "99")]
    public void ParseKnown(string input, string expected) =>
        Assert.Equal(expected, ApInt.Parse(input).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("12a")]
    [InlineData("0x")]
    [InlineData("0xG")]
    [InlineData("0b102")]
    [InlineData("_1")]
    [InlineData("1_")]
    [InlineData("1__0")]
    [InlineData("--1")]
    public void ParseRejects(string input) => Assert.False(ApInt.TryParse(input, out _));

    [Fact]
    public void FormatHexAndBinary()
    {
        Assert.Equal("-FF", ((ApInt)(-255)).ToString("X"));
        Assert.Equal("deadbeefcafe", ApInt.Parse("0xDEADBEEFCAFE").ToString("x"));
        Assert.Equal("101", ((ApInt)5).ToString("B"));
        Assert.Equal("0", ApInt.Zero.ToString("X"));
    }

    [Fact]
    public void DecimalRoundTrip()
    {
        var rng = new Random(20);
        for (int i = 0; i < 2000; i++)
        {
            BigInteger b = RandomBig(rng, 20);
            string expected = b.ToString();
            Assert.Equal(expected, ToAp(b).ToString());       // double dabble
            Assert.Equal(b, ToBig(ApInt.Parse(expected)));    // times-ten-plus
        }
    }

    [Fact]
    public void DecimalAtPowersOfTen()
    {
        // 10^n and 10^n - 1 are where a digit-count or BCD-carry slip would show.
        for (int n = 0; n < 120; n++)
        {
            BigInteger p = BigInteger.Pow(10, n);
            Assert.Equal(p.ToString(), ToAp(p).ToString());
            Assert.Equal((p - 1).ToString(), ToAp(p - 1).ToString());
        }
    }

    [Fact]
    public void HexAndBinaryRoundTrip()
    {
        var rng = new Random(21);
        for (int i = 0; i < 1000; i++)
        {
            BigInteger b = RandomBig(rng, 10);
            ApInt a = ToAp(b);
            string sign = b.Sign < 0 ? "-" : "";
            Assert.Equal(b, ToBig(ApInt.Parse(sign + "0x" + ApInt.Abs(a).ToString("X"))));
            Assert.Equal(b, ToBig(ApInt.Parse(sign + "0b" + ApInt.Abs(a).ToString("B"))));
        }
    }

    [Fact]
    public void LargeDecimal()
    {
        // 2^10000 has 3011 digits.
        BigInteger big = BigInteger.Pow(2, 10000) - 12345;
        Assert.Equal(big.ToString(), ToAp(big).ToString());
        Assert.Equal(big, ToBig(ApInt.Parse(big.ToString())));
    }
}

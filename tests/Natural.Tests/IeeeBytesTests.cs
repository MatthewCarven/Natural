using System.Globalization;
using System.Numerics;
using static Natural.Tests.FloatOracle;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

/// <summary>IEEE binary{k} formats of any width, as bytes.</summary>
public class IeeeBytesTests
{
    // π to 400 fraction bits (Machin's formula in Python integers; the digits agree with the
    // published hexadecimal expansion).
    private static readonly ApFloat Pi = new(ToAp(BigInteger.Parse(
        "03243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89452821E638D01377BE5466CF34E90C6CC0AC",
        NumberStyles.HexNumber)), -400, Exact);

    // ------------------------------------------------------------------
    // The formats
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(16, 5, 11)]
    [InlineData(32, 8, 24)]
    [InlineData(64, 11, 53)]
    [InlineData(128, 15, 113)]
    [InlineData(160, 16, 144)]
    [InlineData(192, 17, 175)]
    [InlineData(256, 19, 237)]
    [InlineData(512, 23, 489)]
    [InlineData(1024, 27, 997)]
    [InlineData(480768, 62, 480706)]    // the widest whose exponent fits a long
    public void StandardFormats(int k, int w, int p)
    {
        IeeeFormat format = IeeeFormat.Binary(k);
        Assert.Equal(w, format.ExponentBits);
        Assert.Equal(p, format.Precision);
        Assert.Equal(k, format.Width);
        Assert.Equal($"binary{k}", format.ToString());
        Assert.Equal(new IeeeFormat(w, p), format);
    }

    [Fact]
    public void WidthFormulaMatchesFloatingPointLog()
    {
        // The library finds round(4·log2 k) from k^8's bit length; here, the plain formula.
        for (int k = 128; k <= 480768; k += 32)
            Assert.Equal((int)Math.Round(4 * Math.Log2(k)) - 13, IeeeFormat.Binary(k).ExponentBits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(48)]
    [InlineData(96)]
    [InlineData(100)]
    [InlineData(144)]
    [InlineData(-32)]
    [InlineData(480800)]               // exponent field of 63 bits
    [InlineData(int.MaxValue)]
    public void NoSuchStandardFormat(int k) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => IeeeFormat.Binary(k));

    [Fact]
    public void FormatParameters()
    {
        Assert.Equal(1023, IeeeFormat.Binary64.Bias);
        Assert.Equal(1023, IeeeFormat.Binary64.MaxExponent);
        Assert.Equal(-1022, IeeeFormat.Binary64.MinExponent);
        Assert.Equal(16383, IeeeFormat.Binary128.Bias);
        Assert.Equal((1L << 26) - 1, IeeeFormat.Binary(1024).Bias);
        Assert.Equal((1L << 61) - 1, IeeeFormat.Binary(480768).Bias);

        var bfloat16 = new IeeeFormat(8, 8);
        Assert.Equal(16, bfloat16.Width);
        Assert.Equal("IeeeFormat(exponent bits 8, precision 8)", bfloat16.ToString());

        Assert.Throws<ArgumentOutOfRangeException>(() => new IeeeFormat(1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IeeeFormat(63, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IeeeFormat(5, 1));
    }

    [Fact]
    public void BadArguments()
    {
        ApFloat one = 1;
        Assert.Throws<ArgumentException>(() => ApFloat.FromIeeeBytes(new byte[15], IeeeFormat.Binary128));
        Assert.Throws<ArgumentException>(() => ApFloat.FromIeeeBytes(new byte[17], IeeeFormat.Binary128));
        Assert.Throws<ArgumentException>(() => one.ToIeeeBytes(new IeeeFormat(3, 4)));      // 7 bits
        Assert.Throws<ArgumentException>(() => one.ToIeeeBytes(default));
        Assert.Throws<ArgumentException>(() => ApFloat.FromIeeeBytes(new byte[2], default));
    }

    // ------------------------------------------------------------------
    // binary16/32/64 against BitConverter, both byte orders
    // ------------------------------------------------------------------

    [Fact]
    public void HardwareFormatsMatchBitConverter()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            Half h = BitConverter.Int16BitsToHalf((short)i);
            CheckAgainst(BitConverter.GetBytes(h), h, IeeeFormat.Binary16, Half.IsNaN(h));
        }

        var rng = new Random(70);
        for (int i = 0; i < 50_000; i++)
        {
            float f = BitConverter.Int32BitsToSingle((int)RandomIeeeBits(rng, 8, 24));
            CheckAgainst(BitConverter.GetBytes(f), f, IeeeFormat.Binary32, float.IsNaN(f));
            double d = BitConverter.Int64BitsToDouble((long)RandomIeeeBits(rng, 11, 53));
            CheckAgainst(BitConverter.GetBytes(d), d, IeeeFormat.Binary64, double.IsNaN(d));
        }
    }

    private static byte[] Reversed(byte[] bytes)
    {
        byte[] copy = (byte[])bytes.Clone();
        Array.Reverse(copy);
        return copy;
    }

    private static void CheckAgainst(byte[] hardware, ApFloat value, IeeeFormat format, bool isNaN)
    {
        Assert.True(BitConverter.IsLittleEndian);
        byte[] reversed = Reversed(hardware);
        AssertSame(value, ApFloat.FromIeeeBytes(hardware, format));
        AssertSame(value, ApFloat.FromIeeeBytes(reversed, format, bigEndian: true));
        if (isNaN) return;   // .NET's NaNs aren't canonical; NaNsAreCanonical covers ours
        Assert.Equal(hardware, value.ToIeeeBytes(format));
        Assert.Equal(reversed, value.ToIeeeBytes(format, bigEndian: true));
    }

    [Fact]
    public void Bfloat16IsTheTopHalfOfAFloat()
    {
        // bfloat16 has float's exponent and 8 bits of precision, so rounding a float toward
        // zero into it just drops the float's low two bytes.
        var bfloat16 = new IeeeFormat(8, 8);
        var rng = new Random(71);
        for (int i = 0; i < 20_000; i++)
        {
            float f = BitConverter.Int32BitsToSingle((int)RandomIeeeBits(rng, 8, 24));
            if (float.IsNaN(f)) continue;
            byte[] bytes = BitConverter.GetBytes(f);
            Assert.Equal(bytes[2..], ((ApFloat)f).ToIeeeBytes(bfloat16, RoundingMode.TowardZero));
        }
    }

    // ------------------------------------------------------------------
    // Known vectors
    // ------------------------------------------------------------------

    public static TheoryData<string, string> Binary128Vectors => new()
    {
        // (value, big-endian hex), from the IEEE 754 quadruple-precision examples.
        { "1", "3FFF0000000000000000000000000000" },
        { "-2", "C0000000000000000000000000000000" },
        { "pi", "4000921FB54442D18469898CC51701B8" },
        { "1/3", "3FFD5555555555555555555555555555" },
        { "max", "7FFEFFFFFFFFFFFFFFFFFFFFFFFFFFFF" },
        { "min normal", "00010000000000000000000000000000" },
        { "min subnormal", "00000000000000000000000000000001" },
        { "-0", "80000000000000000000000000000000" },
        { "inf", "7FFF0000000000000000000000000000" },
        { "-inf", "FFFF0000000000000000000000000000" },
        { "NaN", "7FFF8000000000000000000000000000" },
    };

    public static TheoryData<string, string> Binary256Vectors => new()
    {
        // 1.0 from the IEEE 754 octuple-precision example; π and 1/3 from an independent
        // encoder in Python (integers and Fractions only); the rest by the layout.
        { "1", "3FFFF00000000000000000000000000000000000000000000000000000000000" },
        { "-2", "C000000000000000000000000000000000000000000000000000000000000000" },
        { "pi", "40000921FB54442D18469898CC51701B839A252049C1114CF98E804177D4C762" },
        { "1/3", "3FFFD55555555555555555555555555555555555555555555555555555555555" },
        { "max", "7FFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF" },
        { "min normal", "0000100000000000000000000000000000000000000000000000000000000000" },
        { "min subnormal", "0000000000000000000000000000000000000000000000000000000000000001" },
        { "inf", "7FFFF00000000000000000000000000000000000000000000000000000000000" },
        { "NaN", "7FFFF80000000000000000000000000000000000000000000000000000000000" },
    };

    [Theory]
    [MemberData(nameof(Binary128Vectors))]
    public void Binary128KnownVectors(string name, string hex) => CheckVector(IeeeFormat.Binary128, name, hex);

    [Theory]
    [MemberData(nameof(Binary256Vectors))]
    public void Binary256KnownVectors(string name, string hex) => CheckVector(IeeeFormat.Binary256, name, hex);

    private static void CheckVector(IeeeFormat format, string name, string hex)
    {
        int p = format.Precision;
        long emin = format.MinExponent, emax = format.MaxExponent;
        ApFloat value = name switch
        {
            "1" => 1,
            "-2" => -2,
            "pi" => Pi,
            "1/3" => ApFloat.Divide(1, 3, p + 64),
            "max" => new ApFloat(ToAp((BigInteger.One << p) - 1), emax - (p - 1)),
            "min normal" => new ApFloat(1, emin),
            "min subnormal" => new ApFloat(1, emin - (p - 1)),
            "-0" => ApFloat.NegativeZero,
            "inf" => ApFloat.PositiveInfinity,
            "-inf" => ApFloat.NegativeInfinity,
            _ => ApFloat.NaN,
        };
        byte[] expected = Convert.FromHexString(hex);
        Assert.Equal(hex, Convert.ToHexString(value.ToIeeeBytes(format, bigEndian: true)));
        Assert.Equal(Reversed(expected), value.ToIeeeBytes(format));

        // And back. π and 1/3 were rounded on the way in, so compare with the rounded value.
        ApFloat decoded = ApFloat.FromIeeeBytes(expected, format, bigEndian: true);
        Assert.Equal(p, decoded.Precision);
        AssertSame(value.WithPrecision(p), decoded, name);
    }

    [Fact]
    public void PiInEveryModeBracketsPi()
    {
        // Toward zero and toward +∞ are one ulp apart; nearest is one of them. (Pi is held
        // to 400 bits, so binary320, at 300, is as wide as this can go.)
        foreach (IeeeFormat format in new[] { IeeeFormat.Binary128, IeeeFormat.Binary256, IeeeFormat.Binary(320) })
        {
            BigInteger down = FromBytes(Pi.ToIeeeBytes(format, RoundingMode.TowardZero));
            BigInteger up = FromBytes(Pi.ToIeeeBytes(format, RoundingMode.TowardPositive));
            BigInteger nearest = FromBytes(Pi.ToIeeeBytes(format));
            Assert.Equal(down + 1, up);
            Assert.Equal(down, FromBytes(Pi.ToIeeeBytes(format, RoundingMode.TowardNegative)));
            Assert.True(nearest == down || nearest == up);
            Assert.True(ApFloat.FromIeeeBytes(ToBytes(down, format.Width / 8), format) < Pi);
        }
    }

    // ------------------------------------------------------------------
    // Any width: round trips and rounding, against the oracle
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(15, 113)]
    [InlineData(16, 144)]      // binary160
    [InlineData(19, 237)]      // binary256
    [InlineData(23, 489)]      // binary512
    [InlineData(27, 997)]      // binary1024
    [InlineData(8, 8)]         // bfloat16
    [InlineData(15, 65)]       // 80 bits, x87's range with an implicit leading bit
    [InlineData(4, 4)]         // 8 bits
    public void EveryPatternRoundTrips(int w, int p)
    {
        var format = new IeeeFormat(w, p);
        int length = format.Width / 8;
        var rng = new Random(w * 1000 + p);
        for (int i = 0; i < 3000; i++)
        {
            BigInteger pattern = RandomIeeePattern(rng, w, p);
            byte[] bytes = ToBytes(pattern, length);
            ApFloat x = ApFloat.FromIeeeBytes(bytes, format);
            Assert.Equal(p, x.Precision);
            Assert.Equal(Decode(pattern, w, p), Describe(x));

            // A representable value never rounds, in any mode; NaN comes back canonical.
            byte[] expected = x.IsNaN ? ToBytes(CanonicalNaN(w, p), length) : bytes;
            RoundingMode mode = Modes[rng.Next(4)];
            Assert.Equal(expected, x.ToIeeeBytes(format, mode));
            Assert.Equal(Reversed(expected), x.ToIeeeBytes(format, mode, bigEndian: true));
            AssertSame(x, ApFloat.FromIeeeBytes(Reversed(bytes), format, bigEndian: true));
        }
    }

    [Theory]
    [InlineData(15, 113)]
    [InlineData(19, 237)]
    [InlineData(27, 997)]
    [InlineData(15, 65)]
    [InlineData(4, 4)]
    public void EncoderRoundsCorrectly(int w, int p)
    {
        // Values with up to 64 bits more than the format holds, leading bit anywhere from
        // under the smallest subnormal to past the largest finite, crowded round both ends.
        var format = new IeeeFormat(w, p);
        int length = format.Width / 8;
        long emin = format.MinExponent, emax = format.MaxExponent;
        var rng = new Random(w * 1000 + p + 1);
        for (int i = 0; i < 2000; i++)
        {
            BigInteger m = RandomBig(rng, (p + 64) / 32);
            long top = rng.Next(3) switch
            {
                0 => emin - p + 1 + rng.Next(-4, p + 2),
                1 => emax + rng.Next(-3, 3),
                _ => emin - p - 3 + rng.NextInt64(emax - emin + p + 7),
            };
            long e = m.IsZero ? 0 : top - ((long)m.GetBitLength() - 1);
            ApFloat x = new(ToAp(m), e, Exact);
            RoundingMode mode = Modes[rng.Next(4)];
            byte[] expected = ToBytes(Encode(x, w, p, mode), length);
            byte[] actual = x.ToIeeeBytes(format, mode);
            Assert.True(expected.AsSpan().SequenceEqual(actual),
                $"{Describe(x)} to {format}, {mode}:\n  expected {Convert.ToHexString(Reversed(expected))}\n  actual   {Convert.ToHexString(Reversed(actual))}");
        }
    }

    [Fact]
    public void NaNsAreCanonical()
    {
        // Any NaN decodes as NaN (no sign); every NaN encodes as the one quiet NaN.
        var rng = new Random(72);
        foreach (IeeeFormat format in new[] { IeeeFormat.Binary16, IeeeFormat.Binary128, IeeeFormat.Binary256, IeeeFormat.Binary(1024) })
        {
            int w = format.ExponentBits, p = format.Precision, length = format.Width / 8;
            byte[] canonical = ToBytes(CanonicalNaN(w, p), length);
            for (int i = 0; i < 500; i++)
            {
                BigInteger payload = RandomWideField(rng, p - 1);
                if (payload.IsZero) payload = BigInteger.One << rng.Next(p - 1);   // zero would be infinity
                BigInteger nan = ((BigInteger)rng.Next(2) << (w + p - 1)) | (((BigInteger.One << w) - 1) << (p - 1)) | payload;
                ApFloat x = ApFloat.FromIeeeBytes(ToBytes(nan, length), format);
                Assert.True(x.IsNaN && !x.IsNegative);
                Assert.Equal(canonical, x.ToIeeeBytes(format));
            }
            Assert.Equal(canonical, ApFloat.NaN.ToIeeeBytes(format));
            Assert.Equal(canonical, (-ApFloat.NaN).ToIeeeBytes(format));
        }
    }

    [Fact]
    public void QuadPrecisionArithmetic()
    {
        // Decoded values keep the format's precision, so binary128 values compute at 113 bits.
        ApFloat one = ApFloat.FromIeeeBytes(Convert.FromHexString("3FFF0000000000000000000000000000"), IeeeFormat.Binary128, bigEndian: true);
        ApFloat three = ApFloat.FromIeeeBytes(Convert.FromHexString("40008000000000000000000000000000"), IeeeFormat.Binary128, bigEndian: true);
        ApFloat third = one / three;
        Assert.Equal(113, third.Precision);
        Assert.Equal("3FFD5555555555555555555555555555", Convert.ToHexString(third.ToIeeeBytes(IeeeFormat.Binary128, bigEndian: true)));
        Assert.Equal(1.0 / 3, (double)third);
    }
}

using System.Numerics;
using static Natural.Tests.FloatOracle;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

/// <summary>The IEEE formats up to 64 bits: Half, float and double, and the encoder behind them.</summary>
public class ApFloatIeeeTests
{
    // ------------------------------------------------------------------
    // Decoding and round trips
    // ------------------------------------------------------------------

    [Fact]
    public void HalfRoundTripsEveryBitPattern()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            Half h = BitConverter.Int16BitsToHalf((short)i);
            ApFloat x = h;
            Assert.Equal(11, x.Precision);
            Assert.Equal(Decode((ulong)i, 5, 11), Describe(x));
            AssertSame((ApFloat)(double)h, x, $"Half {i:X4}");         // widening to double is exact in hardware
            Half back = (Half)x;
            if (Half.IsNaN(h)) Assert.True(Half.IsNaN(back));
            else Assert.Equal((ushort)i, (ushort)BitConverter.HalfToInt16Bits(back));
        }
    }

    [Fact]
    public void FloatRoundTrips()
    {
        var rng = new Random(50);
        for (int i = 0; i < 100_000; i++)
        {
            uint bits = (uint)RandomIeeeBits(rng, 8, 24);
            float f = BitConverter.Int32BitsToSingle((int)bits);
            ApFloat x = f;
            Assert.Equal(24, x.Precision);
            Assert.Equal(Decode(bits, 8, 24), Describe(x));
            AssertSame((ApFloat)(double)f, x, $"float {bits:X8}");
            float back = (float)x;
            if (float.IsNaN(f)) Assert.True(float.IsNaN(back));
            else Assert.Equal(bits, (uint)BitConverter.SingleToInt32Bits(back));
        }
    }

    [Fact]
    public void DoubleRoundTrips()
    {
        var rng = new Random(51);
        for (int i = 0; i < 100_000; i++)
        {
            ulong bits = RandomIeeeBits(rng, 11, 53);
            double d = BitConverter.Int64BitsToDouble((long)bits);
            ApFloat x = d;
            Assert.Equal(53, x.Precision);
            Assert.Equal(Decode(bits, 11, 53), Describe(x));
            double back = (double)x;
            if (double.IsNaN(d)) Assert.True(double.IsNaN(back));
            else Assert.Equal(bits, (ulong)BitConverter.DoubleToInt64Bits(back));
        }
    }

    [Fact]
    public void TinyFormatsRoundTripEveryBitPattern()
    {
        // Every pattern of every format from 4 to 11 bits wide: decode, then encode back in
        // every mode (a representable value never rounds). NaNs come back canonical.
        for (int w = 2; w <= 5; w++)
            for (int p = 2; p <= 6; p++)
                for (ulong bits = 0; bits < 1UL << (w + p); bits++)
                {
                    ApFloat x = ApFloat.FromIeeeBits(bits, w, p);
                    Assert.Equal(p, x.Precision);
                    Assert.Equal(Decode(bits, w, p), Describe(x));
                    ulong expected = x.IsNaN ? (ulong)CanonicalNaN(w, p) : bits;
                    foreach (RoundingMode mode in Modes)
                        Assert.True(expected == x.ToIeeeBits(w, p, mode), $"w={w} p={p} {bits:X} {mode}");
                }
    }

    [Fact]
    public void NaNEncodesCanonically()
    {
        // .NET's own double.NaN has the sign bit set (0xFFF8...); ours never does.
        Assert.Equal(0x7FF8000000000000UL, (ulong)BitConverter.DoubleToInt64Bits((double)ApFloat.NaN));
        Assert.Equal(0x7FC00000U, (uint)BitConverter.SingleToInt32Bits((float)ApFloat.NaN));
        Assert.Equal((ushort)0x7E00, (ushort)BitConverter.HalfToInt16Bits((Half)ApFloat.NaN));
        foreach (ulong nan in new[] { 0x7FF0000000000001UL, 0xFFF8000000000000UL, 0x7FFFFFFFFFFFFFFFUL, 0xFFF0000000000001UL })
        {
            ApFloat x = BitConverter.Int64BitsToDouble((long)nan);
            Assert.True(x.IsNaN && !x.IsNegative);
            Assert.Equal(0x7FF8000000000000UL, (ulong)BitConverter.DoubleToInt64Bits((double)x));
        }
    }

    // ------------------------------------------------------------------
    // Encoding: rounding once, straight to the format
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(11, 53)]
    [InlineData(8, 24)]
    [InlineData(5, 11)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 60)]
    [InlineData(15, 49)]
    [InlineData(20, 3)]
    public void EncoderRoundsCorrectly(int w, int p)
    {
        // Values of up to 200 bits whose leading bit lands anywhere from under the smallest
        // subnormal to past the largest finite value, crowded round both ends, every mode.
        var rng = new Random(w * 100 + p);
        long bias = (1L << (w - 1)) - 1, emin = 1 - bias, emax = bias;
        for (int i = 0; i < 4000; i++)
        {
            BigInteger m = RandomBig(rng, 7);
            long top = rng.Next(3) switch
            {
                0 => emin - p + 1 + rng.Next(-4, p + 2),        // around the subnormals
                1 => emax + rng.Next(-3, 3),                     // around overflow
                _ => emin - p - 3 + rng.NextInt64(emax - emin + p + 7),
            };
            long e = m.IsZero ? 0 : top - ((long)m.GetBitLength() - 1);
            ApFloat x = rng.Next(40) switch
            {
                0 => ApFloat.NaN,
                1 => ApFloat.NegativeInfinity,
                2 => ApFloat.NegativeZero,
                _ => new ApFloat(ToAp(m), e, Exact),
            };
            RoundingMode mode = Modes[rng.Next(4)];
            ulong expected = (ulong)Encode(x, w, p, mode);
            ulong actual = x.ToIeeeBits(w, p, mode);
            Assert.True(expected == actual, $"{Describe(x)} to w={w} p={p}, {mode}: expected {expected:X}, got {actual:X}");

            if (w == 11 && p == 53)
                Assert.Equal(expected, (ulong)BitConverter.DoubleToInt64Bits(x.ToDouble(mode)));
            if (w == 8 && p == 24)
                Assert.Equal(expected, (ulong)(uint)BitConverter.SingleToInt32Bits(x.ToSingle(mode)));
            if (w == 5 && p == 11)
                Assert.Equal(expected, (ulong)(ushort)BitConverter.HalfToInt16Bits(x.ToHalf(mode)));
        }
    }

    [Fact]
    public void OverflowAndUnderflowByMode()
    {
        ApFloat big = new ApFloat(1, 5000), tiny = new ApFloat(1, -5000);
        double max = double.MaxValue, eps = double.Epsilon;

        Assert.Equal(double.PositiveInfinity, big.ToDouble());
        Assert.Equal(max, big.ToDouble(RoundingMode.TowardZero));
        Assert.Equal(max, big.ToDouble(RoundingMode.TowardNegative));
        Assert.Equal(double.PositiveInfinity, big.ToDouble(RoundingMode.TowardPositive));
        Assert.Equal(-max, (-big).ToDouble(RoundingMode.TowardPositive));
        Assert.Equal(double.NegativeInfinity, (-big).ToDouble(RoundingMode.TowardNegative));

        Assert.Equal(0x0000000000000000UL, (ulong)BitConverter.DoubleToInt64Bits(tiny.ToDouble()));
        Assert.Equal(0x8000000000000000UL, (ulong)BitConverter.DoubleToInt64Bits((-tiny).ToDouble()));   // -0
        Assert.Equal(eps, tiny.ToDouble(RoundingMode.TowardPositive));
        Assert.Equal(-eps, (-tiny).ToDouble(RoundingMode.TowardNegative));
        Assert.Equal(0x8000000000000000UL, (ulong)BitConverter.DoubleToInt64Bits((-tiny).ToDouble(RoundingMode.TowardPositive)));

        // Exponents far outside any format cost nothing.
        Assert.Equal(double.PositiveInfinity, new ApFloat(3, 1L << 60).ToDouble());
        Assert.Equal(eps, new ApFloat(3, -(1L << 60)).ToDouble(RoundingMode.TowardPositive));

        // The edges themselves: max + half an ulp is a tie, and max is odd, so it goes up.
        ApFloat halfUlpAboveMax = (ApFloat)max + new ApFloat(1, 970);
        Assert.Equal(double.PositiveInfinity, (double)halfUlpAboveMax);
        Assert.Equal(max, (double)(halfUlpAboveMax - new ApFloat(1, 900)));
        Assert.Equal(0.0, (double)new ApFloat(1, -1075));                               // half of epsilon: a tie, to even (0)
        Assert.Equal(eps, (double)new ApFloat(3, -1076));                               // 3/4 of epsilon
        Assert.Equal(2 * eps, (double)new ApFloat(3, -1075));                           // 1.5 epsilon: a tie, to even (2)
    }

    [Fact]
    public void NarrowingMatchesHardware()
    {
        // double to float and Half, and float to Half, crowding round each target's
        // subnormals and overflow.
        var rng = new Random(52);
        for (int i = 0; i < 100_000; i++)
        {
            ulong dBits = RandomIeeeBits(rng, 11, 53, -150, -126, 127, -25, -14, 15);
            double d = BitConverter.Int64BitsToDouble((long)dBits);
            ApFloat x = d;
            AssertSameBits((float)d, (float)x, $"(float) {dBits:X16}");
            AssertSameBits((Half)d, (Half)x, $"(Half) {dBits:X16}");

            uint fBits = (uint)RandomIeeeBits(rng, 8, 24, -25, -14, 15);
            float f = BitConverter.Int32BitsToSingle((int)fBits);
            AssertSameBits((Half)f, (Half)(ApFloat)f, $"(Half) {fBits:X8}");
        }
    }

    [Fact]
    public void DirectedModesBracketTheValue()
    {
        ApFloat third = ApFloat.Divide(1, 3, 200);
        double down = third.ToDouble(RoundingMode.TowardNegative), up = third.ToDouble(RoundingMode.TowardPositive);
        Assert.Equal(BitConverter.DoubleToInt64Bits(down) + 1, BitConverter.DoubleToInt64Bits(up));
        Assert.Equal(down, third.ToDouble(RoundingMode.TowardZero));
        Assert.Equal(1.0 / 3, third.ToDouble());
        Assert.True((ApFloat)down < third && third < (ApFloat)up);
    }

    // ------------------------------------------------------------------
    // Conversions: precision, and integers staying exact
    // ------------------------------------------------------------------

    [Fact]
    public void ConvertedValuesKeepTheirPrecision()
    {
        Assert.Equal(53, ((ApFloat)0.1).Precision);
        Assert.Equal(24, ((ApFloat)0.1f).Precision);
        Assert.Equal(11, ((ApFloat)(Half)0.1).Precision);
        Assert.Equal(53, ((ApFloat)0.0).Precision);
        Assert.Equal(53, ((ApFloat)double.NaN).Precision);

        // Two converted doubles compute at 53 bits: 0.1 + 0.2 is the famous 0.30000000000000004.
        ApFloat sum = (ApFloat)0.1 + (ApFloat)0.2;
        Assert.Equal(53, sum.Precision);
        Assert.Equal(0.1 + 0.2, (double)sum);
        Assert.NotEqual(0.3, (double)sum);

        // With a default-precision value the wider one wins, and the sum is nearly exact.
        ApFloat wide = (ApFloat)0.1 + new ApFloat(0) + (ApFloat)0.2;
        Assert.Equal(ApFloat.DefaultPrecision, wide.Precision);
        Assert.Equal(0.30000000000000004, (double)wide);    // 0.1 and 0.2 were doubles all along
    }

    [Fact]
    public void IntegersConvertExactly()
    {
        // These go through the integer conversions, not through float (see ApFloat.cs).
        ApFloat u = ulong.MaxValue;
        Assert.Equal(ulong.MaxValue, (ulong)u);
        ApFloat l = (1L << 62) + 1;
        Assert.Equal((1L << 62) + 1, (long)l);
        ApFloat ui = uint.MaxValue;
        Assert.Equal(uint.MaxValue, (ulong)ui);
        ApFloat b = (byte)200;
        Assert.Equal(200L, (long)b);
        Assert.Equal(-7L, (long)(ApFloat)(-7.9));                   // truncates toward zero
        Assert.Throws<OverflowException>(() => (long)(ApFloat)1e19);
        Assert.Throws<OverflowException>(() => (ulong)(ApFloat)(-1.0));
    }

    // ------------------------------------------------------------------
    // The hardware as oracle
    // ------------------------------------------------------------------

    [Fact]
    public void DoubleArithmeticIsBitIdentical()
    {
        var rng = new Random(60);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            double a = BitConverter.Int64BitsToDouble((long)aBits), b = BitConverter.Int64BitsToDouble((long)bBits);
            ApFloat x = a, y = b;
            string ab = $"{aBits:X16} {bBits:X16}";

            // Rounded once from the exact result, straight to double: always the hardware's answer.
            // (A sum of doubles fits in 2,200 bits and a product in 106. A quotient is never
            // exact, but unless it's a midpoint it stays at least 2^-106 of its size away from
            // every one, so first rounding it to 256 bits can't make or cross a midpoint.)
            AssertSameBits(a + b, (double)ApFloat.Add(x, y, 2200), ab + " +");
            AssertSameBits(a - b, (double)ApFloat.Subtract(x, y, 2200), ab + " -");
            AssertSameBits(a * b, (double)ApFloat.Multiply(x, y, 106), ab + " *");
            AssertSameBits(a / b, (double)ApFloat.Divide(x, y, 256), ab + " /");

            // The operators at 53 bits. Sums that land subnormal are exact, so + and - always
            // match; a product or quotient there would round twice (53 bits, then the subnormal)
            // where the hardware rounds once, so those are left to the checks above.
            AssertSameBits(a + b, (double)(x + y), ab + " + at 53");
            AssertSameBits(a - b, (double)(x - y), ab + " - at 53");
            ApFloat product = x * y, quotient = x / y;
            Assert.Equal(53, product.Precision);
            if (!LandsSubnormal(a * b, product)) AssertSameBits(a * b, (double)product, ab + " * at 53");
            if (!LandsSubnormal(a / b, quotient)) AssertSameBits(a / b, (double)quotient, ab + " / at 53");
        }
    }

    [Fact]
    public void FloatArithmeticIsBitIdentical()
    {
        var rng = new Random(61);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 8, 24);
            float a = BitConverter.Int32BitsToSingle((int)aBits), b = BitConverter.Int32BitsToSingle((int)bBits);
            ApFloat x = a, y = b;
            string ab = $"{aBits:X8} {bBits:X8}";
            float sum = a + b, difference = a - b, product = a * b, quotient = a / b;

            AssertSameBits(sum, (float)ApFloat.Add(x, y, 320), ab + " +");
            AssertSameBits(difference, (float)ApFloat.Subtract(x, y, 320), ab + " -");
            AssertSameBits(product, (float)ApFloat.Multiply(x, y, 48), ab + " *");
            AssertSameBits(quotient, (float)ApFloat.Divide(x, y, 128), ab + " /");

            AssertSameBits(sum, (float)(x + y), ab + " + at 24");
            AssertSameBits(difference, (float)(x - y), ab + " - at 24");
            ApFloat p = x * y, q = x / y;
            Assert.Equal(24, p.Precision);
            if (!LandsSubnormal(product, p)) AssertSameBits(product, (float)p, ab + " * at 24");
            if (!LandsSubnormal(quotient, q)) AssertSameBits(quotient, (float)q, ab + " / at 24");
        }
    }

    /// <summary>The hardware's result is subnormal, or underflowed to zero from a non-zero value.</summary>
    private static bool LandsSubnormal(double hardware, ApFloat unbounded) =>
        double.IsSubnormal(hardware) || (hardware == 0 && !unbounded.IsZero);

    private static bool LandsSubnormal(float hardware, ApFloat unbounded) =>
        float.IsSubnormal(hardware) || (hardware == 0 && !unbounded.IsZero);

    private static void AssertSameBits(double expected, double actual, string context) =>
        Assert.True(double.IsNaN(expected) ? double.IsNaN(actual) : BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{context}: expected {expected:R} ({BitConverter.DoubleToInt64Bits(expected):X16}), got {actual:R} ({BitConverter.DoubleToInt64Bits(actual):X16})");

    private static void AssertSameBits(float expected, float actual, string context) =>
        Assert.True(float.IsNaN(expected) ? float.IsNaN(actual) : BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
            $"{context}: expected {expected:R} ({BitConverter.SingleToInt32Bits(expected):X8}), got {actual:R} ({BitConverter.SingleToInt32Bits(actual):X8})");

    private static void AssertSameBits(Half expected, Half actual, string context) =>
        Assert.True(Half.IsNaN(expected) ? Half.IsNaN(actual) : BitConverter.HalfToInt16Bits(expected) == BitConverter.HalfToInt16Bits(actual),
            $"{context}: expected {expected} ({BitConverter.HalfToInt16Bits(expected):X4}), got {actual} ({BitConverter.HalfToInt16Bits(actual):X4})");
}

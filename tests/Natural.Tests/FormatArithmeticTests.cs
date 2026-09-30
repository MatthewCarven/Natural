using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// Arithmetic in an IEEE format: Add/Subtract/Multiply/Divide(a, b, IeeeFormat), rounded
/// once into the format, subnormals and overflow included.
/// </summary>
public class FormatArithmeticTests
{
    private static readonly IeeeFormat Binary64 = IeeeFormat.Binary64, Binary32 = IeeeFormat.Binary32;
    private static readonly char[] Ops = ['+', '-', '*', '/'];

    private static ApFloat Apply(char op, ApFloat a, ApFloat b, IeeeFormat format, RoundingMode mode = RoundingMode.ToNearestEven) => op switch
    {
        '+' => ApFloat.Add(a, b, format, mode),
        '-' => ApFloat.Subtract(a, b, format, mode),
        '*' => ApFloat.Multiply(a, b, format, mode),
        _ => ApFloat.Divide(a, b, format, mode),
    };

    private static double Hardware(char op, double a, double b) => op switch
    {
        '+' => a + b,
        '-' => a - b,
        '*' => a * b,
        _ => a / b,
    };

    private static float Hardware(char op, float a, float b) => op switch
    {
        '+' => a + b,
        '-' => a - b,
        '*' => a * b,
        _ => a / b,
    };

    // ------------------------------------------------------------------
    // The hardware as oracle, with nothing left out this time
    // ------------------------------------------------------------------

    [Fact]
    public void Binary64IsDoubleEverywhere()
    {
        // The same pairs as DoubleArithmeticIsBitIdentical, crowded round the subnormal and
        // overflow edges, but no result is excused: subnormal products and quotients included.
        var rng = new Random(100);
        for (int i = 0; i < 30_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            double a = BitConverter.Int64BitsToDouble((long)aBits), b = BitConverter.Int64BitsToDouble((long)bBits);
            foreach (char op in Ops)
            {
                ApFloat r = Apply(op, a, b, Binary64);
                double hardware = Hardware(op, a, b);
                Assert.Equal(53, r.Precision);
                if (!r.IsNaN) AssertSame((ApFloat)(double)r, r, $"{aBits:X16} {op} {bBits:X16} isn't a double");
                Assert.True(double.IsNaN(hardware) ? r.IsNaN : BitConverter.DoubleToInt64Bits(hardware) == BitConverter.DoubleToInt64Bits((double)r),
                    $"{aBits:X16} {op} {bBits:X16}: hardware {hardware:R}, got {(double)r:R} from {Describe(r)}");
            }
        }
    }

    [Fact]
    public void Binary32IsFloatEverywhere()
    {
        var rng = new Random(101);
        for (int i = 0; i < 30_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 8, 24);
            float a = BitConverter.Int32BitsToSingle((int)aBits), b = BitConverter.Int32BitsToSingle((int)bBits);
            foreach (char op in Ops)
            {
                ApFloat r = Apply(op, a, b, Binary32);
                float hardware = Hardware(op, a, b);
                Assert.True(float.IsNaN(hardware) ? r.IsNaN : BitConverter.SingleToInt32Bits(hardware) == BitConverter.SingleToInt32Bits((float)r),
                    $"{aBits:X8} {op} {bBits:X8}: hardware {hardware:R}, got {(float)r:R} from {Describe(r)}");
            }
        }
    }

    [Fact]
    public void TheProductThatUsedToRoundTwice()
    {
        // From the session 1 probe: at 53 bits with an unbounded exponent this product
        // rounds once to 53 bits and again to double's subnormal grid, and lands one place
        // off. In binary64 it rounds once, like the hardware.
        double a = 1.4941053324303592E-130, b = 5.423880961370769E-179;
        Assert.Equal(8.103849466851567E-309, a * b);
        Assert.Equal(8.10384946685157E-309, (double)((ApFloat)a * (ApFloat)b));
        Assert.Equal(8.103849466851567E-309, (double)ApFloat.Multiply(a, b, Binary64));
    }

    [Fact]
    public void SpecialValuesMatchDouble()
    {
        double[] specials =
        [
            double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 1, -1, 0.5, -3, 0.1,
            double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon,
            BitConverter.Int64BitsToDouble(0x0010000000000000), -BitConverter.Int64BitsToDouble(0x000FFFFFFFFFFFFF),
        ];
        foreach (double a in specials)
            foreach (double b in specials)
                foreach (char op in Ops)
                {
                    double hardware = Hardware(op, a, b);
                    double got = (double)Apply(op, a, b, Binary64);
                    Assert.True(double.IsNaN(hardware) ? double.IsNaN(got) : BitConverter.DoubleToInt64Bits(hardware) == BitConverter.DoubleToInt64Bits(got),
                        $"{a:R} {op} {b:R}: hardware {hardware:R}, got {got:R}");
                }
    }

    [Fact]
    public void RoundingUpPastTheTopOverflows()
    {
        // max + half an ulp is a tie, and max's significand is odd, so it rounds up to 2^1024:
        // overflow, even though no operand and no exact result is past max by a whole ulp.
        // (Checked on the ApFloat itself: converting to double would apply the overflow a
        // second time and hide a result that had wrongly stayed finite at 2^1024.)
        ApFloat max = double.MaxValue, halfUlp = new ApFloat(1, 970);
        AssertSame(ApFloat.PositiveInfinity, ApFloat.Add(max, halfUlp, Binary64));
        AssertSame(max, ApFloat.Add(max, halfUlp, Binary64, RoundingMode.TowardZero));
        AssertSame(max, ApFloat.Add(max, halfUlp - new ApFloat(1, 900), Binary64));
        AssertSame(ApFloat.PositiveInfinity, ApFloat.Add(max, new ApFloat(1, 900), Binary64, RoundingMode.TowardPositive));
        AssertSame(ApFloat.NegativeInfinity, ApFloat.Subtract(-max, halfUlp, Binary64));
        AssertSame(ApFloat.PositiveInfinity, ApFloat.Multiply(max, 1.0000000000000002, Binary64));
    }

    // ------------------------------------------------------------------
    // Every rounding mode, against the rational oracle
    // ------------------------------------------------------------------

    [Fact]
    public void EveryModeMatchesTheOracle()
    {
        // The hardware only rounds to nearest; the oracle does all four, overflow to the
        // largest finite value and underflow to ±0 or the smallest subnormal included.
        var rng = new Random(102);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            double a = BitConverter.Int64BitsToDouble((long)aBits), b = BitConverter.Int64BitsToDouble((long)bBits);
            if (!double.IsFinite(a) || !double.IsFinite(b)) continue;
            char op = Ops[rng.Next(4)];
            if (op == '/' && b == 0) continue;
            CheckAgainstOracle(op, a, b, 11, 53, Modes[rng.Next(4)], $"{aBits:X16} {op} {bBits:X16}");
        }
    }

    [Fact]
    public void OperandsFromOutsideTheFormat()
    {
        // Operands the format can't hold (up to 200 bits, exponents past both ends of
        // binary64, zeros included): the result still rounds once, straight into the format.
        var rng = new Random(104);
        for (int i = 0; i < 5000; i++)
        {
            ApFloat a = RandomFinite(rng, 6, 1200).WithPrecision(rng.Next(1, 201));
            ApFloat b = RandomFinite(rng, 6, 1200).WithPrecision(rng.Next(1, 201));
            char op = Ops[rng.Next(4)];
            if (op == '/' && b.IsZero) continue;
            CheckAgainstOracle(op, a, b, 11, 53, Modes[rng.Next(4)], $"{Describe(a)} {op} {Describe(b)}");
        }
        // 0 + x is x, rounded into the format too.
        AssertSame(ApFloat.Zero, ApFloat.Add(ApFloat.Zero, new ApFloat(1, -2000), Binary64));
        AssertSame(ApFloat.PositiveInfinity, ApFloat.Add(new ApFloat(1, 2000), ApFloat.NegativeZero, Binary64));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    public void TinyFormatsExhaustively(int w, int p)
    {
        // Every pair of finite values in a 6-bit format, every operation, every mode.
        var finite = new List<ulong>();
        for (ulong bits = 0; bits < 1UL << (w + p); bits++)
            if (ApFloat.FromIeeeBits(bits, w, p).IsFinite) finite.Add(bits);

        foreach (ulong x in finite)
            foreach (ulong y in finite)
            {
                ApFloat a = ApFloat.FromIeeeBits(x, w, p), b = ApFloat.FromIeeeBits(y, w, p);
                foreach (char op in Ops)
                {
                    if (op == '/' && b.IsZero) continue;
                    foreach (RoundingMode mode in Modes)
                        CheckAgainstOracle(op, a, b, w, p, mode, $"w={w} p={p}: {x:X} {op} {y:X}");
                }
            }
    }

    private static void CheckAgainstOracle(char op, ApFloat a, ApFloat b, int w, int p, RoundingMode mode, string context)
    {
        var (an, ad) = ToRational(a);
        var (bn, bd) = ToRational(b);
        var (num, den) = op switch
        {
            '+' => (an * bd + bn * ad, ad * bd),
            '-' => (an * bd - bn * ad, ad * bd),
            '*' => (an * bn, ad * bd),
            _ => (an * bd, ad * bn),
        };
        if (den.Sign < 0) (num, den) = (-num, -den);

        // IEEE §6.3: an exact zero sum is +0 unless both operands are zeros of the same
        // sign, or it's rounding toward -∞; a product or quotient takes the XOR of the signs.
        bool bNegative = op == '-' ? !b.IsNegative : b.IsNegative;
        bool zeroSign = op is '*' or '/'
            ? a.IsNegative ^ b.IsNegative
            : a.IsZero && b.IsZero && a.IsNegative == bNegative ? a.IsNegative : mode == RoundingMode.TowardNegative;

        var format = new IeeeFormat(w, p);
        ApFloat r = Apply(op, a, b, format, mode);
        BigInteger expected = EncodeRational(num, den, zeroSign, w, p, mode);
        BigInteger actual = r.ToIeeeBits(w, p);    // exact: r is in the format already
        Assert.True(expected == actual, $"{context} {mode}: expected {expected:X}, got {actual:X} ({Describe(r)})");
        Assert.Equal(p, r.Precision);
        // Reading the result through the encoder rounds it again, which could hide a result
        // that wasn't in the format. So check that directly: no rounding mode moves it.
        AssertSame(r, r.WithFormat(format, RoundingMode.TowardZero), $"{context} {mode}: not in the format");
        AssertSame(r, r.WithFormat(format, RoundingMode.TowardPositive), $"{context} {mode}: not in the format");
    }

    // ------------------------------------------------------------------
    // WithFormat, and results that are exactly the format's
    // ------------------------------------------------------------------

    [Fact]
    public void WithFormatIsWhatTheEncoderStores()
    {
        var rng = new Random(103);
        for (int i = 0; i < 5000; i++)
        {
            BigInteger m = Oracle.RandomBig(rng, 7);
            long top = rng.Next(3) switch
            {
                0 => -1074 + rng.Next(-5, 60),
                1 => 1023 + rng.Next(-3, 3),
                _ => rng.Next(-1100, 1100),
            };
            ApFloat x = new(Oracle.ToAp(m), m.IsZero ? 0 : top - ((long)m.GetBitLength() - 1), Exact);
            RoundingMode mode = Modes[rng.Next(4)];
            ApFloat inFormat = x.WithFormat(Binary64, mode);

            Assert.Equal(53, inFormat.Precision);
            AssertSame((ApFloat)x.ToDouble(mode), inFormat, $"{Describe(x)} {mode}");
            // Already in the format, so no mode moves it.
            foreach (RoundingMode again in Modes)
                AssertSame(inFormat, inFormat.WithFormat(Binary64, again));
        }
    }

    [Fact]
    public void WithFormatKeepsSpecialValues()
    {
        Assert.True(ApFloat.NaN.WithFormat(Binary64).IsNaN);
        AssertSame(ApFloat.NegativeInfinity, ApFloat.NegativeInfinity.WithFormat(Binary64));
        AssertSame(ApFloat.NegativeZero, ApFloat.NegativeZero.WithFormat(Binary64));
        Assert.Equal(113, ApFloat.NegativeZero.WithFormat(IeeeFormat.Binary128).Precision);
        Assert.Throws<ArgumentException>(() => ApFloat.Zero.WithFormat(default));
        Assert.Throws<ArgumentException>(() => ApFloat.Add(1, 1, default(IeeeFormat)));
    }

    [Fact]
    public void WideFormats()
    {
        // binary128: 1/3, the edges, and overflow by mode.
        IeeeFormat quad = IeeeFormat.Binary128;
        ApFloat third = ApFloat.Divide(1, 3, quad);
        Assert.Equal(113, third.Precision);
        Assert.Equal("3FFD5555555555555555555555555555", Convert.ToHexString(third.ToIeeeBytes(quad, bigEndian: true)));

        ApFloat max = ApFloat.FromIeeeBytes(Convert.FromHexString("7FFEFFFFFFFFFFFFFFFFFFFFFFFFFFFF"), quad, bigEndian: true);
        ApFloat tiny = ApFloat.FromIeeeBytes(Convert.FromHexString("00000000000000000000000000000001"), quad, bigEndian: true);
        Assert.True(ApFloat.Multiply(max, 2, quad).IsInfinity);
        AssertSame(max, ApFloat.Multiply(max, 2, quad, RoundingMode.TowardZero));
        AssertSame(-max, ApFloat.Multiply(-max, 2, quad, RoundingMode.TowardPositive));
        AssertSame(ApFloat.Zero, ApFloat.Divide(tiny, 2, quad));                              // a tie, to even (0)
        AssertSame(tiny, ApFloat.Divide(tiny, 2, quad, RoundingMode.TowardPositive));
        AssertSame(ApFloat.NegativeZero, ApFloat.Divide(-tiny, 3, quad));
        AssertSame(tiny * 2, ApFloat.Multiply(tiny, 1.5, quad));                             // 1.5 ulp: a tie, to even (2)

        // Wider still: 1 + tiny in binary1024 is 1, rounding up it's the next value above 1.
        IeeeFormat k1024 = IeeeFormat.Binary(1024);
        ApFloat one = 1;
        AssertSame(one, ApFloat.Add(one, new ApFloat(1, -2000), k1024));
        AssertSame(new ApFloat(ApInt.One + (ApInt.One << 996), -996, Exact), ApFloat.Add(one, new ApFloat(1, -2000), k1024, RoundingMode.TowardPositive));
    }
}

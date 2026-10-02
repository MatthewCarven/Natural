using System.Globalization;
using System.Numerics;
using static Natural.Tests.FloatOracle;
using static Natural.Tests.Oracle;

namespace Natural.Tests;

/// <summary>Decimal (and binary and hex) text, both ways.</summary>
public class ApFloatTextTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly CultureInfo German = new("de-DE");

    /// <summary>A random value rounded to a random precision (1..200) somewhere in ±2^400.</summary>
    private static ApFloat RandomValue(Random rng, out int precision)
    {
        precision = rng.Next(1, 201);
        ApFloat x = RandomFinite(rng, 7, 400);
        return x.WithPrecision(precision);
    }

    private static double RandomDouble(Random rng, bool finite = true)
    {
        while (true)
        {
            double d = BitConverter.Int64BitsToDouble((long)RandomIeeeBits(rng, 11, 53));
            if (!finite || double.IsFinite(d)) return d;
        }
    }

    // ------------------------------------------------------------------
    // The default: every digit the precision carries, always scientific
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2)]
    [InlineData(11, 5)]
    [InlineData(24, 9)]
    [InlineData(53, 17)]
    [InlineData(113, 36)]
    [InlineData(200, 62)]
    [InlineData(237, 73)]
    [InlineData(256, 79)]
    public void DecimalDigitsForKnownPrecisions(int precision, int digits) =>
        Assert.Equal(digits, ApFloat.DecimalDigitsFor(precision));

    [Fact]
    public void DecimalDigitsIsOneMoreThan2ToThePsDigits()
    {
        // The smallest N with 10^(N-1) > 2^p is 2^p's digit count plus one.
        for (int p = 1; p <= 3000; p++)
            Assert.Equal((BigInteger.One << p).ToString().Length + 1, ApFloat.DecimalDigitsFor(p));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApFloat.DecimalDigitsFor(0));
    }

    [Theory]
    [InlineData(0.1, "1.0000000000000001E-001")]
    [InlineData(1.0, "1.0000000000000000E+000")]
    [InlineData(1234.5, "1.2345000000000000E+003")]
    [InlineData(1e20, "1.0000000000000000E+020")]
    [InlineData(1e-7, "9.9999999999999995E-008")]
    [InlineData(-2.5, "-2.5000000000000000E+000")]
    [InlineData(0.0, "0.0000000000000000E+000")]
    [InlineData(-0.0, "-0.0000000000000000E+000")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    [InlineData(double.NaN, "NaN")]
    public void DefaultIsPrecisionWidthScientific(double d, string expected)
    {
        ApFloat x = d;
        Assert.Equal(expected, x.ToString(null, Inv));
        Assert.Equal(x.ToString(null, CultureInfo.CurrentCulture), x.ToString());
    }

    [Fact]
    public void DefaultWidthFollowsPrecision()
    {
        // Same precision, same width; more precision, visibly longer.
        Assert.Equal("3.3325E-001", ApFloat.Divide(1, 3, 11).ToString(null, Inv));    // 1/3 at 11 bits is 0.333251953125
        Assert.Equal("3.33333343E-001", ((ApFloat)(1f / 3)).ToString(null, Inv));
        Assert.Equal("3.3333333333333333333333333333333333333333333333333333333333344E-001",
            ApFloat.Divide(1, 3, 200).ToString(null, Inv));
        Assert.Equal(79, ApFloat.Divide(1, 3, 256).ToString(null, Inv).Split('E')[0].Replace(".", "").Length);

        var rng = new Random(80);
        for (int i = 0; i < 2000; i++)
        {
            string text = ((ApFloat)RandomDouble(rng)).ToString(null, Inv);
            Assert.Equal(text.StartsWith('-') ? 24 : 23, text.Length);
        }
    }

    [Fact]
    public void DefaultMatchesDotNetE16()
    {
        // .NET's "E16" is the same layout (three-digit exponent), exact since .NET Core 3.0.
        // It rounds a tie away from zero where IEEE (and ApFloat) goes to even, so ties are skipped.
        var rng = new Random(81);
        for (int i = 0; i < 20_000; i++)
        {
            double d = RandomDouble(rng);
            ApFloat x = d;
            if (Scientific(x, 16, RoundingMode.ToNearestEven).Tie) continue;
            Assert.Equal(d.ToString("E16", Inv), x.ToString(null, Inv));
        }
    }

    // ------------------------------------------------------------------
    // E and F: against the oracle, every mode; against .NET for doubles
    // ------------------------------------------------------------------

    [Fact]
    public void ScientificMatchesOracle()
    {
        var rng = new Random(82);
        for (int i = 0; i < 3000; i++)
        {
            ApFloat x = RandomValue(rng, out _);
            int n = rng.Next(0, 45);
            RoundingMode mode = Modes[rng.Next(4)];
            char e = rng.Next(2) == 0 ? 'E' : 'e';
            string expected = Scientific(x, n, mode, e).Text;
            Assert.True(expected == x.ToString($"{e}{n}", Inv, mode), $"{Describe(x)} {e}{n} {mode}: expected {expected}, got {x.ToString($"{e}{n}", Inv, mode)}");
        }
    }

    [Fact]
    public void FixedMatchesOracle()
    {
        var rng = new Random(83);
        for (int i = 0; i < 3000; i++)
        {
            ApFloat x = RandomFinite(rng, 4, 150).WithPrecision(rng.Next(1, 201));
            int n = rng.Next(0, 60);
            RoundingMode mode = Modes[rng.Next(4)];
            string expected = Fixed(x, n, mode).Text;
            Assert.True(expected == x.ToString($"F{n}", Inv, mode), $"{Describe(x)} F{n} {mode}: expected {expected}, got {x.ToString($"F{n}", Inv, mode)}");
        }
    }

    [Fact]
    public void ScientificAndFixedMatchDotNetForDoubles()
    {
        var rng = new Random(84);
        for (int i = 0; i < 20_000; i++)
        {
            double d = RandomDouble(rng);
            ApFloat x = d;
            int n = rng.Next(0, 30);
            if (!Scientific(x, n, RoundingMode.ToNearestEven).Tie)
                Assert.Equal(d.ToString($"E{n}", Inv), x.ToString($"E{n}", Inv));
            if (Math.Abs(d) < 1e30 && !Fixed(x, n, RoundingMode.ToNearestEven).Tie)
                Assert.Equal(d.ToString($"F{n}", Inv), x.ToString($"F{n}", Inv));
        }
    }

    [Theory]
    [InlineData(0.125, "F2", RoundingMode.ToNearestEven, "0.12")]      // a tie: to even
    [InlineData(0.375, "F2", RoundingMode.ToNearestEven, "0.38")]
    [InlineData(2.5, "F0", RoundingMode.ToNearestEven, "2")]
    [InlineData(3.5, "F0", RoundingMode.ToNearestEven, "4")]
    [InlineData(-2.5, "F0", RoundingMode.ToNearestEven, "-2")]
    [InlineData(0.125, "F2", RoundingMode.TowardPositive, "0.13")]
    [InlineData(0.125, "F2", RoundingMode.TowardZero, "0.12")]
    [InlineData(-0.125, "F2", RoundingMode.TowardNegative, "-0.13")]
    [InlineData(-0.125, "F2", RoundingMode.TowardPositive, "-0.12")]
    [InlineData(-0.001, "F2", RoundingMode.ToNearestEven, "-0.00")]    // the sign stays, as in .NET Core 3.0+
    [InlineData(9.995, "F2", RoundingMode.ToNearestEven, "9.99")]      // 9.995 is 9.99499999... as a double
    [InlineData(9.5, "E0", RoundingMode.ToNearestEven, "1E+001")]      // carries into a new power of ten
    [InlineData(123.456, "E2", RoundingMode.ToNearestEven, "1.23E+002")]
    [InlineData(123.456, "e2", RoundingMode.TowardPositive, "1.24e+002")]
    [InlineData(1e-300, "E3", RoundingMode.ToNearestEven, "1.000E-300")]
    [InlineData(0.0, "F3", RoundingMode.ToNearestEven, "0.000")]
    [InlineData(0.0, "E", RoundingMode.ToNearestEven, "0.000000E+000")]
    [InlineData(1.5, "F", RoundingMode.ToNearestEven, "1.50")]
    public void RoundingInText(double d, string format, RoundingMode mode, string expected) =>
        Assert.Equal(expected, ((ApFloat)d).ToString(format, Inv, mode));

    // ------------------------------------------------------------------
    // R and G: the shortest text that reads back
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0.1, "0.1")]
    [InlineData(1.0, "1")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(123456.0, "123456")]
    [InlineData(1e16, "10000000000000000")]
    [InlineData(1e17, "1E+017")]
    [InlineData(1e-5, "0.00001")]
    [InlineData(1e-6, "1E-006")]
    [InlineData(1e300, "1E+300")]
    [InlineData(0.30000000000000004, "0.30000000000000004")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(0.0, "0")]
    [InlineData(-0.0, "-0")]
    public void ShortestKnown(double d, string expected)
    {
        Assert.Equal(expected, ((ApFloat)d).ToString("R", Inv));
        Assert.Equal(expected, ((ApFloat)d).ToString("G", Inv));
    }

    [Fact]
    public void ShortestReadsBackAndIsShortest()
    {
        var rng = new Random(85);
        for (int i = 0; i < 1500; i++)
        {
            ApFloat x = RandomValue(rng, out int p);
            if (x.IsZero) continue;
            string text = x.ToString("R", Inv);
            AssertSame(x, ApFloat.Parse(text, p, RoundingMode.ToNearestEven, Inv), $"R \"{text}\" at {p} bits");

            // One digit fewer can't: neither neighbour at k - 1 digits reads back.
            int k = Canonical(text).Split('e')[0].Length;
            if (k == 1) continue;
            long exponent = DecimalExponent(x);
            BigInteger below = ScaledRound(x, k - 2 - exponent, RoundingMode.TowardZero).Q;
            foreach (BigInteger q in new[] { below, below + 1 })
            {
                string shorter = $"{(x.IsNegative ? "-" : "")}{q}E{exponent - (k - 2)}";
                Assert.False(ApFloat.Parse(shorter, p, RoundingMode.ToNearestEven, Inv) == x,
                    $"{text} at {p} bits, but {shorter} reads back too");
            }
        }
    }

    [Fact]
    public void ShortestMatchesDotNetR()
    {
        // In double's normal range the two agree digit for digit. (Among the subnormals they
        // shouldn't: at 53 bits ApFloat's exponent is unbounded, so 2^-1074 has neighbours
        // 2^-1126 away rather than double's 2^-1074, and needs all 17 digits.)
        var rng = new Random(86);
        for (int i = 0; i < 10_000; i++)
        {
            double d = RandomDouble(rng);
            if (d == 0 || double.IsSubnormal(d)) continue;
            string dotNet = d.ToString("R", Inv);
            if (double.Parse(dotNet, Inv) != d) continue;    // .NET's own slip; see below
            Assert.Equal(Canonical(dotNet), Canonical(((ApFloat)d).ToString("R", Inv)));
        }
        Assert.Equal("4.9406564584124654E-324", ((ApFloat)double.Epsilon).ToString("R", Inv));

        // Found here, 2026-09-30, on .NET 10.0.12: for 2^-25 and 2^-958, .NET's "R" gives 16
        // digits that read back as the double below. A power of two's lower neighbour is half
        // as far away, and the 16-digit text lands outside that half-gap. Python's repr gives
        // 17 digits, as ApFloat does.
        foreach (int e in new[] { -25, -958 })
        {
            double p = Math.ScaleB(1, e);
            string ours = ((ApFloat)p).ToString("R", Inv);
            Assert.Equal(p, double.Parse(ours, Inv));
            Assert.Equal(17, Canonical(ours).Split('e')[0].Length);
        }
        Assert.Equal("2.9802322387695312E-008", ((ApFloat)Math.ScaleB(1, -25)).ToString("R", Inv));
    }

    [Fact]
    public void GeneralMatchesDotNetDigits()
    {
        var rng = new Random(87);
        for (int i = 0; i < 10_000; i++)
        {
            double d = RandomDouble(rng);
            if (d == 0) continue;
            ApFloat x = d;
            int n = rng.Next(1, 18);
            if (Scientific(x, n - 1, RoundingMode.ToNearestEven).Tie) continue;
            Assert.Equal(Canonical(d.ToString($"G{n}", Inv)), Canonical(x.ToString($"G{n}", Inv)));
        }
        Assert.Equal("1.2346E+005", ((ApFloat)123456.0).ToString("G5", Inv));
        Assert.Equal("0.33333", ApFloat.Divide(1, 3, 53).ToString("G5", Inv));
        Assert.Equal("1.5", ((ApFloat)1.5).ToString("G5", Inv));
    }

    // ------------------------------------------------------------------
    // Custom patterns
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(3.14159, "00000.0000", "00003.1416")]
    [InlineData(-3.14159, "00000.0000", "-00003.1416")]
    [InlineData(1234567.891, "#,##0.00", "1,234,567.89")]
    [InlineData(12.0, "#,##0.00", "12.00")]
    [InlineData(0.5, "#.##", ".5")]
    [InlineData(2.0, "0.###", "2")]
    [InlineData(2.5, "0.0##", "2.5")]
    [InlineData(2.0, "0.0##", "2.0")]
    [InlineData(2.12345, "0.0##", "2.123")]
    [InlineData(0.0, "#.##", "0")]
    [InlineData(-0.001, "0.00", "-0.00")]
    [InlineData(42.0, "'x = '0", "x = 42")]
    [InlineData(42.0, "0' kg'", "42 kg")]
    [InlineData(42.0, "\\#0", "#42")]
    [InlineData(7.0, "000", "007")]
    [InlineData(1234.0, "0", "1234")]
    [InlineData(0.125, "0.00", "0.12")]                                   // a tie: to even
    public void CustomPatterns(double d, string pattern, string expected) =>
        Assert.Equal(expected, ((ApFloat)d).ToString(pattern, Inv));

    /// <summary>
    /// A pattern with no digit placeholders prints no digits, so its value must not be
    /// scaled to an integer at all. It used to be, and scaling a value with an enormous
    /// exponent overflows -- so a literal-only pattern threw instead of printing.
    /// </summary>
    [Theory]
    [InlineData("'hello'", "hello")]
    [InlineData("'x'", "x")]
    [InlineData("'hi '", "hi ")]
    [InlineData("''", "")]
    [InlineData("'a''b'", "ab")]
    public void CustomPatternWithoutPlaceholdersIgnoresTheValue(string pattern, string expected)
    {
        ApFloat huge = ApFloat.Parse("1e1000000000000000000");
        Assert.Equal(expected, huge.ToString(pattern, Inv));
        Assert.Equal("-" + expected, (-huge).ToString(pattern, Inv));   // the sign still applies
    }

    [Theory]
    [InlineData("0.00;(0.00)")]
    [InlineData("0.0%")]
    [InlineData("0.00E+00")]
    [InlineData("0.0.0")]
    [InlineData("0 0")]
    [InlineData("'open")]
    [InlineData("Q")]
    public void UnsupportedPatternsThrow(string pattern) =>
        Assert.Throws<FormatException>(() => ((ApFloat)1.5).ToString(pattern, Inv));

    [Fact]
    public void CustomZeroPaddingMatchesFixed()
    {
        var rng = new Random(88);
        for (int i = 0; i < 2000; i++)
        {
            ApFloat x = RandomFinite(rng, 3, 60).WithPrecision(rng.Next(1, 120));
            string f3 = x.ToString("F3", Inv);
            Assert.Equal(f3, x.ToString("0.000", Inv));
            string sign = f3.StartsWith('-') ? "-" : "";
            Assert.Equal(sign + f3.TrimStart('-').PadLeft(9, '0'), x.ToString("00000.000", Inv));
        }
    }

    // ------------------------------------------------------------------
    // Binary point and hex float
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(5.25, "B", "101.01")]
    [InlineData(5.25, "B4", "101.0100")]
    [InlineData(-0.75, "B", "-0.11")]
    [InlineData(0.1, "B8", "0.00011010")]
    [InlineData(0.0, "B", "0")]
    [InlineData(0.0, "B2", "0.00")]
    [InlineData(2.5, "B0", "10")]                     // 10.1 is a tie between 10 and 11: to even
    [InlineData(1024.0, "B", "10000000000")]
    public void BinaryPointView(double d, string format, string expected) =>
        Assert.Equal(expected, ((ApFloat)d).ToString(format, Inv));

    [Fact]
    public void BinaryPointRoundingModes()
    {
        Assert.Equal("11", ((ApFloat)2.5).ToString("B0", Inv, RoundingMode.TowardPositive));
        Assert.Equal("0.01", ((ApFloat)0.1).ToString("B2", Inv, RoundingMode.TowardPositive));
        Assert.Equal("0.00", ((ApFloat)0.1).ToString("B2", Inv));
    }

    [Theory]
    [InlineData(1.0, "a", "0x1p+0")]
    [InlineData(0.1, "a", "0x1.999999999999ap-4")]
    [InlineData(0.1, "A", "0X1.999999999999AP-4")]
    [InlineData(0.1, "x", "0x1.999999999999ap-4")]
    [InlineData(0.1, "a3", "0x1.99ap-4")]
    [InlineData(3.0, "a", "0x1.8p+1")]
    [InlineData(-2.0, "a", "-0x1p+1")]
    [InlineData(double.MaxValue, "a", "0x1.fffffffffffffp+1023")]
    [InlineData(double.Epsilon, "a", "0x1p-1074")]
    [InlineData(0.0, "a", "0x0p+0")]
    [InlineData(-0.0, "a", "-0x0p+0")]
    [InlineData(1.5, "a0", "0x1p+1")]                 // a tie at one bit: to even, and the carry makes 2
    [InlineData(1.0, "a2", "0x1.00p+0")]
    [InlineData(1.000244140625, "a3", "0x1.001p+0")]      // 1 + 2^-12: the last bit a3 keeps
    [InlineData(1.0003662109375, "a3", "0x1.002p+0")]     // 0x1.0018: a tie, up to even
    public void HexFloat(double d, string format, string expected) =>
        Assert.Equal(expected, ((ApFloat)d).ToString(format, Inv));

    [Fact]
    public void BinaryAndHexRoundTrip()
    {
        var rng = new Random(89);
        for (int i = 0; i < 2000; i++)
        {
            ApFloat x = RandomValue(rng, out int p);
            string b = x.ToString("B", Inv), a = x.ToString("a", Inv);
            string unsignedB = b.TrimStart('-');
            AssertSame(x, ApFloat.Parse((x.IsNegative ? "-0b" : "0b") + unsignedB, p), $"B {b}");
            AssertSame(x, ApFloat.Parse(a, p), $"a {a}");
            AssertSame(x, ApFloat.Parse(x.ToString("A", Inv), p), $"A {a}");
        }
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    [Fact]
    public void ParseRoundsCorrectly()
    {
        // Random digit strings (runs of 9s and 0s included), any exponent, precision and mode,
        // against the rational oracle.
        var rng = new Random(90);
        for (int i = 0; i < 3000; i++)
        {
            int length = rng.Next(1, 45);
            var digits = new char[length];
            int style = rng.Next(4);
            for (int j = 0; j < length; j++)
                digits[j] = style switch { 0 => '9', 1 => j == 0 ? '1' : '0', _ => (char)('0' + rng.Next(10)) };
            if (style < 2 && length > 3) digits[rng.Next(length)] = (char)('0' + rng.Next(10));
            int point = rng.Next(length + 1);
            long exponent = rng.Next(-400, 401);
            bool negative = rng.Next(2) == 0;
            string text = $"{(negative ? "-" : "")}{new string(digits, 0, point)}.{new string(digits, point, length - point)}e{exponent}";

            int p = rng.Next(1, 201);
            RoundingMode mode = Modes[rng.Next(4)];
            BigInteger num = BigInteger.Parse(new string(digits), Inv);
            long scale = exponent - (length - point);
            BigInteger den = BigInteger.One;
            if (scale >= 0) num *= BigInteger.Pow(10, (int)scale);
            else den = BigInteger.Pow(10, (int)-scale);
            Rounded expected = Round(negative ? -num : num, den, p, mode) with { Negative = negative };
            AssertRounded(expected, ApFloat.Parse(text, p, mode, Inv), p, $"Parse(\"{text}\", {p}, {mode})");
        }
    }

    [Fact]
    public void ParseMatchesDoubleParse()
    {
        var rng = new Random(91);
        for (int i = 0; i < 20_000; i++)
        {
            double d = RandomDouble(rng);
            if (double.IsSubnormal(d)) continue;     // at 53 bits ApFloat has no subnormals
            // Round-trip text, and the same text nudged to a nearby, longer decimal.
            string text = d.ToString(rng.Next(3) switch { 0 => "R", 1 => "E25", _ => "G" + rng.Next(1, 20) }, Inv);
            double hardware = double.Parse(text, Inv);
            if (double.IsSubnormal(hardware) || double.IsInfinity(hardware) || hardware == 0) continue;
            Assert.True(BitConverter.DoubleToInt64Bits(hardware) == BitConverter.DoubleToInt64Bits((double)ApFloat.Parse(text, 53, RoundingMode.ToNearestEven, Inv)),
                $"\"{text}\"");
        }
    }

    [Theory]
    [InlineData("9007199254740993", 53, "9007199254740992")]           // 2^53 + 1: a tie, to even
    [InlineData("9007199254740995", 53, "9007199254740996")]
    [InlineData("0.1", 53, "0.1")]
    [InlineData("-0", 53, "-0")]
    [InlineData("+.5", 53, "0.5")]
    [InlineData("1.", 53, "1")]
    [InlineData("1_000.25", 53, "1000.25")]
    [InlineData("0.000_001_5", 53, "1.5E-006")]
    [InlineData("  42  ", 53, "42")]
    [InlineData("1E400", 53, "1E+400")]                                  // no overflow: the exponent is unbounded
    [InlineData("0x1.8p+1", 53, "3")]
    [InlineData("0X1P-1074", 53, "4.9406564584124654E-324")]
    [InlineData("0x.8", 53, "0.5")]
    [InlineData("0x1.fffffffffffff8p+0", 53, "2")]                    // a tie at 53 bits, to even
    [InlineData("-0b101.01", 53, "-5.25")]
    [InlineData("0b1p-3", 53, "0.125")]
    public void ParseKnown(string text, int precision, string expectedR) =>
        Assert.Equal(expectedR, ApFloat.Parse(text, precision, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("-NaN")]
    public void ParseNaN(string text) => Assert.True(ApFloat.Parse(text, Inv).IsNaN);

    [Theory]
    [InlineData("Infinity", false)]
    [InlineData("-Infinity", true)]
    [InlineData("inf", false)]
    [InlineData("-∞", true)]
    [InlineData("+INF", false)]
    public void ParseInfinity(string text, bool negative)
    {
        ApFloat x = ApFloat.Parse(text, Inv);
        Assert.True(x.IsInfinity && x.IsNegative == negative);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("-")]
    [InlineData("e5")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1e1.5")]
    [InlineData("1.2.3")]
    [InlineData("--1")]
    [InlineData("1__0")]
    [InlineData("_1")]
    [InlineData("1_")]
    [InlineData("abc")]
    [InlineData("0x")]
    [InlineData("0x1p")]
    [InlineData("0xg")]
    [InlineData("0b2")]
    [InlineData("1e99999999999999999999")]
    [InlineData(null)]
    public void ParseRejects(string? text) =>
        Assert.False(ApFloat.TryParse(text, 53, RoundingMode.ToNearestEven, Inv, out _));

    [Theory]
    [InlineData("1e99999999999999999999")]          // the exponent itself is past a long
    [InlineData("0x1p99999999999999999999")]
    [InlineData("1e3000000000000000000")]           // fits a long, but the binary exponent wouldn't (3e18 · log2 10)
    [InlineData("-1e-3000000000000000000")]
    [InlineData("1e4000000000000000000")]
    public void ExponentsPastALongAreRefused(string text)
    {
        var error = Assert.Throws<OverflowException>(() => ApFloat.Parse(text, 53, RoundingMode.ToNearestEven, Inv));
        Assert.Contains("too large", error.Message);
        Assert.False(ApFloat.TryParse(text, 53, RoundingMode.ToNearestEven, Inv, out _));
    }

    [Fact]
    public void ExponentExtremes()
    {
        Assert.Equal("1.000E+5000", ApFloat.Parse("1e5000", 53, RoundingMode.ToNearestEven, Inv).ToString("E3", Inv));

        // No cap on decimal exponents (it went when the interval engine came in): where the old
        // cap was, and far past it. 0.001e100004 is 1 × 10^100001 once the point moves to the end.
        Assert.Equal("1E+100001", ApFloat.Parse("1e100001", 53, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));
        Assert.Equal("1E+100001", ApFloat.Parse("0.001e100004", 53, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));
        Assert.Equal("-1E-100001", ApFloat.Parse("-1e-100001", 53, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));
        Assert.Equal("2.5E+2000000000000000000", ApFloat.Parse("25e1999999999999999999", 53, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));

        // A zero needs no power of five, so its exponent doesn't matter.
        AssertSame(ApFloat.Zero, ApFloat.Parse("0e1000000000", 53, RoundingMode.ToNearestEven, Inv));
        AssertSame(ApFloat.NegativeZero, ApFloat.Parse("-0.000e-999999999", 53, RoundingMode.ToNearestEven, Inv));

        // Hex needs none either: any exponent that fits a long, instantly.
        Assert.Equal("0x1p+3321929", ApFloat.Parse("0x1p+3321929", 53, RoundingMode.ToNearestEven, Inv).ToString("a", Inv));

        // Malformed is still a format error, not an overflow.
        Assert.Throws<FormatException>(() => ApFloat.Parse("1e1.5", 53, RoundingMode.ToNearestEven, Inv));
        Assert.Throws<FormatException>(() => ApFloat.Parse("1e", 53, RoundingMode.ToNearestEven, Inv));
    }

    [Fact]
    public void ParseRoundsInEveryMode()
    {
        Assert.Equal("0.1", ApFloat.Parse("0.1", 53, RoundingMode.ToNearestEven, Inv).ToString("R", Inv));
        ApFloat down = ApFloat.Parse("0.1", 53, RoundingMode.TowardZero, Inv);
        ApFloat up = ApFloat.Parse("0.1", 53, RoundingMode.TowardPositive, Inv);
        Assert.True(down < up);
        Assert.Equal(BitConverter.DoubleToInt64Bits((double)down) + 1, BitConverter.DoubleToInt64Bits((double)up));
        Assert.Equal(53, up.Precision);
        Assert.Equal(ApFloat.DefaultPrecision, ApFloat.Parse("0.1", Inv).Precision);
    }

    // ------------------------------------------------------------------
    // .NET plumbing: culture, IFormattable, ISpanFormattable, IParsable
    // ------------------------------------------------------------------

    [Fact]
    public void CultureDecidesTheSeparator()
    {
        ApFloat x = 1234.5;
        Assert.Equal("1,2345000000000000E+003", x.ToString(null, German));
        Assert.Equal("1234,50", x.ToString("F2", German));
        Assert.Equal("1.234,50", x.ToString("#,##0.00", German));
        Assert.Equal("1234,5", x.ToString("R", German));
        Assert.Equal("10011010010.1", x.ToString("B", German));                 // binary and hex keep "."
        Assert.Equal(x, ApFloat.Parse("1234,5", German));
        Assert.False(ApFloat.TryParse("1234.5", German, out _));
        Assert.Equal(x, ApFloat.Parse("0x1.34ap+10", German));

        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            Assert.Equal("1,2345000000000000E+003", x.ToString());
            Assert.Equal(x, ApFloat.Parse("1234,5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void AlignsInInterpolation()
    {
        ApFloat pi = Math.PI, e = Math.E;
        Assert.Equal("[    3.1416][    2.7183]", string.Format(Inv, "[{0,10:F4}][{1,10:F4}]", pi, e));
        Assert.Equal(" 3.1415926535897931E+000", string.Format(Inv, "{0,24}", pi));
        Assert.Equal("3.1416", FormattableString.Invariant($"{pi:F4}"));
    }

    [Fact]
    public void TryFormatAndIParsable()
    {
        ApFloat x = 0.1;
        Span<char> small = stackalloc char[5], exact = stackalloc char[3];
        Assert.False(x.TryFormat(small, out int written, "E4", Inv));
        Assert.Equal(0, written);
        Assert.True(x.TryFormat(exact, out written, "R", Inv));
        Assert.Equal("0.1", exact[..written].ToString());

        Assert.Equal(x, ParseAny<ApFloat>("0.1").WithPrecision(53));
        static T ParseAny<T>(string s) where T : IParsable<T> => T.Parse(s, CultureInfo.InvariantCulture);
    }
}

using System.Globalization;
using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// Session 7: text parsed straight into an IeeeFormat, rounded once, with flags. The hardware's
/// parsers (double.Parse, float.Parse, Half.Parse) are the oracle bit for bit; the text's exact
/// rational, rounded by FloatOracle.EncodeRational, is the oracle in every mode and format, and
/// FloatOracle.RoundingFlags predicts the flags. The interval engine is forced on (and off) where
/// it has to agree on tininess and overflow as well as on the value.
/// </summary>
public class ParseFormatTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A decimal text's exact value, read with BigInteger: [sign] digits [. digits] [e [sign] digits].</summary>
    private static (BigInteger Num, BigInteger Den, bool Negative) Rational(string text)
    {
        bool negative = text.StartsWith('-');
        string body = text.TrimStart('-', '+');
        int e = body.IndexOfAny(['e', 'E']);
        long exponent = e < 0 ? 0 : long.Parse(body[(e + 1)..], Inv);
        string mantissa = e < 0 ? body : body[..e];
        int point = mantissa.IndexOf('.');
        string digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
        long scale = exponent - (point < 0 ? 0 : mantissa.Length - point - 1);
        BigInteger m = BigInteger.Parse(digits, Inv);
        if (negative) m = -m;
        return scale >= 0 ? (m * BigInteger.Pow(10, (int)scale), 1, negative) : (m, BigInteger.Pow(10, (int)-scale), negative);
    }

    /// <summary>
    /// The exact decimal of num / 2^k (num ≥ 0, a dyadic), written out: num × 5^k e-k. With
    /// <paramref name="extra"/> digits more and a <paramref name="nudge"/> of ±1 in the last, it's
    /// a hair above or below: 10^-extra of the last digit's worth.
    /// </summary>
    private static string DyadicText(BigInteger num, BigInteger den, int extra = 0, int nudge = 0)
    {
        int k = (int)den.GetBitLength() - 1;
        BigInteger digits = BigInteger.Abs(num * BigInteger.Pow(5, k) * BigInteger.Pow(10, extra) + nudge);   // a hair below zero is above it
        return $"{digits}e-{k + extra}";
    }

    /// <summary>Random decimal text, its digits biased like Oracle.RandomBig, the point anywhere, the value near 10^lead.</summary>
    private static string RandomText(Random rng, int lead)
    {
        BigInteger m = Oracle.RandomBig(rng, rng.Next(1, 4), allowNegative: false);
        if (m.IsZero) m = rng.Next(1, 10);
        string digits = m.ToString(Inv);
        int point = rng.Next(digits.Length + 1);
        long exponent = lead - (point - 1);
        string sign = rng.Next(2) == 0 ? "-" : "";
        return $"{sign}{digits[..point]}.{digits[point..]}e{exponent}".Replace(".e", "e");
    }

    /// <summary>The midpoint between a random pattern and the next one up, in full, or a hair either side of it.</summary>
    private static string MidpointText(Random rng, int w, int p)
    {
        BigInteger maxFinite = ((BigInteger.One << w) - 2) << (p - 1) | ((BigInteger.One << (p - 1)) - 1);
        BigInteger bits = RandomIeeePattern(rng, w, p) & ((BigInteger.One << (w + p - 1)) - 1);      // positive
        if (bits >= maxFinite) bits = maxFinite - 1;
        var (an, ad) = ToRational(FromPattern(bits, w, p));
        var (bn, bd) = ToRational(FromPattern(bits + 1, w, p));
        BigInteger d = BigInteger.Max(ad, bd), num = an * (d / ad) + bn * (d / bd), den = 2 * d;     // both are powers of two
        string text = DyadicText(num, den, rng.Next(0, 4) == 0 ? 0 : rng.Next(1, 30), rng.Next(-1, 2));
        return rng.Next(2) == 0 ? "-" + text : text;
    }

    /// <summary>A bit pattern's value, for formats of any width (wider than 64 bits, they're whole bytes).</summary>
    private static ApFloat FromPattern(BigInteger bits, int w, int p) =>
        w + p <= 64 ? ApFloat.FromIeeeBits((ulong)bits, w, p) : ApFloat.FromIeeeBytes(ToBytes(bits, (w + p) / 8), new IeeeFormat(w, p));

    /// <summary>Parses into the format through a context, and checks the bits and the flags against the exact rational.</summary>
    private static ApFloat CheckAgainstTheOracle(string text, IeeeFormat format, RoundingMode mode)
    {
        var (num, den, negative) = Rational(text);
        var ieee = new IeeeContext(format, mode);
        ApFloat r = ieee.Parse(text, Inv);
        string at = $"\"{text}\" into ({format.ExponentBits}, {format.Precision}) {mode}";
        BigInteger expected = EncodeRational(num, den, negative, format.ExponentBits, format.Precision, mode);
        BigInteger actual = format.Width <= 64 ? r.ToIeeeBits(format.ExponentBits, format.Precision) : FromBytes(r.ToIeeeBytes(format));
        Assert.True(expected == actual, $"{at}: expected {Decode(expected, format.ExponentBits, format.Precision)}, got {Describe(r)}");
        IeeeFlags flags = RoundingFlags(num, den, r, format, mode);
        Assert.True(flags == ieee.Flags, $"{at}: expected {flags}, got {ieee.Flags}");
        AssertSame(r, ApFloat.Parse(text, format, mode, Inv), at);
        return r;
    }

    // ------------------------------------------------------------------
    // The hardware's parsers
    // ------------------------------------------------------------------

    [Fact]
    public void ParsesAsDoubleParseDoes()
    {
        var rng = new Random(1000);
        var texts = new List<string>
        {
            "4.9406564584124654e-324", "2.4703282292062327e-324", "2.4703282292062328e-324", "2.47032822920623272e-324",
            "1.7976931348623157e308", "1.7976931348623158e308", "1.797693134862315807e308", "1.7976931348623159e308",
            "2.2250738585072011e-308", "2.2250738585072012e-308", "2.2250738585072014e-308", "1e308", "9.99e308", "1e309",
            "1e-324", "1e-400", "1e400", "0.1", "-0", "0", "123456789012345678901234567890", "-7.2e-310",
        };
        for (int i = 0; i < 3000; i++)
        {
            double d = BitConverter.Int64BitsToDouble((long)RandomIeeeBits(rng, 11, 53, -1074, -1022, -1000, 0, 1000, 1023));
            if (!double.IsFinite(d)) continue;
            texts.Add(d.ToString(rng.Next(4) switch { 0 => "R", 1 => "E16", 2 => "E25", _ => "G17" }, Inv));
        }
        for (int i = 0; i < 3000; i++) texts.Add(MidpointText(rng, 11, 53));
        for (int i = 0; i < 3000; i++) texts.Add(RandomText(rng, rng.Next(-330, 312)));

        foreach (string text in texts)
        {
            ApFloat r = CheckAgainstTheOracle(text, IeeeFormat.Binary64, RoundingMode.ToNearestEven);
            double hardware = double.Parse(text, Inv);
            Assert.True(BitConverter.DoubleToInt64Bits(hardware) == BitConverter.DoubleToInt64Bits((double)r),
                $"\"{text}\": double.Parse {hardware:R}, got {Describe(r)}");
        }
    }

    [Fact]
    public void ParsesAsFloatParseDoes()
    {
        var rng = new Random(1001);
        for (int i = 0; i < 9000; i++)
        {
            string text = (i % 3) switch
            {
                0 => MidpointText(rng, 8, 24),
                1 => RandomText(rng, rng.Next(-48, 41)),
                _ => BitConverter.Int32BitsToSingle((int)RandomIeeeBits(rng, 8, 24, -149, -126, 0, 127)) is var f && float.IsFinite(f)
                    ? f.ToString(rng.Next(2) == 0 ? "R" : "E12", Inv) : "1",
            };
            ApFloat r = CheckAgainstTheOracle(text, IeeeFormat.Binary32, RoundingMode.ToNearestEven);
            float hardware = float.Parse(text, Inv);
            Assert.True(BitConverter.SingleToInt32Bits(hardware) == BitConverter.SingleToInt32Bits((float)r),
                $"\"{text}\": float.Parse {hardware:R}, got {Describe(r)}");
        }
    }

    [Fact]
    public void ParsesAsHalfParseDoes()
    {
        // Half.Parse shares double's and float's parsing code, so it should be correctly rounded
        // too. The oracle is checked against it here, not assumed.
        var rng = new Random(1002);
        for (int i = 0; i < 6000; i++)
        {
            string text = i % 2 == 0 ? MidpointText(rng, 5, 11) : RandomText(rng, rng.Next(-9, 6));
            ApFloat r = CheckAgainstTheOracle(text, IeeeFormat.Binary16, RoundingMode.ToNearestEven);
            Half hardware = Half.Parse(text, Inv);
            Assert.True(BitConverter.HalfToInt16Bits(hardware) == BitConverter.HalfToInt16Bits((Half)r),
                $"\"{text}\": Half.Parse {hardware}, got {Describe(r)}");
        }
    }

    // ------------------------------------------------------------------
    // Every mode and format
    // ------------------------------------------------------------------

    [Fact]
    public void EveryModeAndFormatMatchesTheOracle()
    {
        var rng = new Random(1010);
        var formats = new[] { (11, 53), (8, 24), (5, 11), (15, 113), (5, 6), (3, 4), (2, 4), (4, 2) };
        for (int i = 0; i < 8000; i++)
        {
            var (w, p) = formats[rng.Next(formats.Length)];
            var format = new IeeeFormat(w, p);
            RoundingMode mode = Modes[rng.Next(4)];
            // Leading digits from a little under the smallest subnormal to a little over the largest finite.
            long low = (long)Math.Floor((format.MinExponent - p) * 0.30103) - 3, high = (long)Math.Ceiling((format.MaxExponent + 1) * 0.30103) + 2;
            // Half of them crowd the edges: the smallest subnormal, the smallest normal, and overflow.
            long[] edges = [low + 3, (long)Math.Floor(format.MinExponent * 0.30103), high - 2];
            long lead = rng.Next(2) == 0 ? rng.NextInt64(low, high + 1) : edges[rng.Next(edges.Length)] + rng.Next(-4, 5);
            // (binary128's midpoints run to thousands of digits, slow to parse: its short texts reach
            // past 10^±4096, where the engine settles them by default, near both edges.)
            string text = rng.Next(3) == 0 && w + p <= 64 ? MidpointText(rng, w, p) : RandomText(rng, (int)lead);
            CheckAgainstTheOracle(text, format, mode);
        }
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    public void EveryMidpointOfATinyFormat(int w, int p)
    {
        // Every value of a 6-bit format and the midpoints between neighbours, a hair either side
        // and exactly, in every mode: every rounding and flag edge the format has.
        var format = new IeeeFormat(w, p);
        for (ulong bits = 0; bits + 1 < (((1UL << w) - 1) << (p - 1)); bits++)
        {
            var (an, ad) = ToRational(ApFloat.FromIeeeBits(bits, w, p));
            var (bn, bd) = ToRational(ApFloat.FromIeeeBits(bits + 1, w, p));
            foreach (int nudge in new[] { -1, 0, 1 })
            foreach (RoundingMode mode in Modes)
            {
                BigInteger d = BigInteger.Max(ad, bd);
                CheckAgainstTheOracle(DyadicText(an * (d / ad) + bn * (d / bd), 2 * d, 3, nudge), format, mode);
                CheckAgainstTheOracle("-" + DyadicText(an, ad, 2, nudge), format, mode);
            }
        }
    }

    // ------------------------------------------------------------------
    // The interval engine, forced on
    // ------------------------------------------------------------------

    private static T Routed<T>(ApFloat.Route route, Func<T> f)
    {
        ApFloat.ForcedRoute = route;
        try { return f(); }
        finally { ApFloat.ForcedRoute = ApFloat.Route.Auto; }
    }

    [Fact]
    public void TheEngineAgreesOnTheRangeToo()
    {
        // Values where two neighbouring texts round to the same double but raise different flags:
        // 2^-1022 − 2^-1076 to nearest (below it, rounding to 53 bits with no floor leaves it tiny,
        // so Underflow; above, it isn't); 2^-1022 − 2^-1075 rounding up (the same, one place over);
        // and 2^1024 toward zero (the largest double either way, but past it is Overflow). A hair
        // either side, from 10^-20 of a digit (the first round settles it) to 10^-60 (its interval
        // straddles the edge, and only agreeing on the range sends it round again).
        var cases = new (BigInteger Num, BigInteger Den, RoundingMode Mode)[]
        {
            ((BigInteger.One << 54) - 1, BigInteger.One << 1076, RoundingMode.ToNearestEven),
            ((BigInteger.One << 53) - 1, BigInteger.One << 1075, RoundingMode.TowardPositive),
            (BigInteger.One << 1024, BigInteger.One, RoundingMode.TowardZero),
        };
        foreach (var (num, den, mode) in cases)
        foreach (int extra in new[] { 0, 20, 30, 40, 50, 60 })
        foreach (int nudge in extra == 0 ? new[] { 0 } : new[] { -1, 1 })
        foreach (string sign in new[] { "", "-" })
        {
            string text = sign + DyadicText(num, den, extra, nudge);
            RoundingMode m = sign == "" ? mode : mode switch
            {
                RoundingMode.TowardPositive => RoundingMode.TowardNegative,
                RoundingMode.TowardNegative => RoundingMode.TowardPositive,
                _ => mode,
            };
            ApFloat byOracle = CheckAgainstTheOracle(text, IeeeFormat.Binary64, m);
            foreach (ApFloat.Route route in new[] { ApFloat.Route.Interval, ApFloat.Route.Exact })
            {
                ApFloat r = Routed(route, () => CheckAgainstTheOracle(text, IeeeFormat.Binary64, m));
                AssertSame(byOracle, r, $"\"{text}\" {m} by {route}");
            }
        }
    }

    [Fact]
    public void EngineResultsKnowWhenTheyAreExact()
    {
        // Past the cache of powers of five the engine settles these, and its ends can't say if the
        // text was exact. The digits can: 10^4200 × 10^-4200 is exactly 1, and no flag is raised.
        string zeros = new('0', 4200);
        foreach (string text in new[] { $"1{zeros}e-4200", $"15{zeros}e-4201", $"-3{zeros}e-4200", $"1{zeros}1e-4201", $"0.{zeros}1e4201" })
        foreach (RoundingMode mode in Modes)
            CheckAgainstTheOracle(text, IeeeFormat.Binary64, mode);

        var ieee = new IeeeContext(IeeeFormat.Binary64);
        ieee.Parse($"1{zeros}e-4200");
        Assert.False(ApFloat.LastExact);                       // the engine settled it, not the exact route
        Assert.Equal(IeeeFlags.None, ieee.Flags);

        // 10^4096 = 5^4096 × 2^4096: exact in a format with 10,000 bits of precision, and settled by the engine.
        var wide = new IeeeFormat(20, 10_000);
        ieee = new IeeeContext(wide);
        Assert.Equal(new ApFloat(Oracle.ToAp(BigInteger.Pow(10, 4096)), 0, 10_000), ieee.Parse("1e4096"));
        Assert.Equal(IeeeFlags.None, ieee.Flags);
        Assert.Equal(IeeeFlags.Inexact, Routed(ApFloat.Route.Interval, () => { var c = new IeeeContext(wide); c.Parse("3e-4096"); return c.Flags; }));

        // Forced on below the cache, the engine meets exact values everywhere.
        var rng = new Random(1020);
        for (int i = 0; i < 300; i++)
        {
            string text = rng.Next(2) == 0 ? MidpointText(rng, 11, 53) : RandomText(rng, rng.Next(-330, 312));
            Routed(ApFloat.Route.Interval, () => CheckAgainstTheOracle(text, IeeeFormat.Binary64, Modes[rng.Next(4)]));
        }
    }

    // ------------------------------------------------------------------
    // Far out of range, and exponents past a long
    // ------------------------------------------------------------------

    [Fact]
    public void FarOutOfRangeSaturates()
    {
        var format = IeeeFormat.Binary64;
        BigInteger huge = BigInteger.One << 1030, tiny = BigInteger.One << 1080;    // stand-ins: past the top, under half the smallest subnormal
        foreach (RoundingMode mode in Modes)
        foreach (bool negative in new[] { false, true })
        {
            string sign = negative ? "-" : "";
            BigInteger overflow = EncodeRational(negative ? -huge : huge, 1, negative, 11, 53, mode);
            BigInteger underflow = EncodeRational(negative ? -1 : 1, tiny, negative, 11, 53, mode);
            foreach (string text in new[] { "1e99999999999999999999", "123.456e9223372036854775807", "1e1000000000000000000", "0x1p99999999999999999999", "0x1p4611686018427387904" })
                Check(sign + text, overflow, IeeeFlags.Overflow | IeeeFlags.Inexact);
            foreach (string text in new[] { "1e-99999999999999999999", "0.001e-9223372036854775808", "1e-1000000000000000000", "0b1p-99999999999999999999", "9e-400" })
                Check(sign + text, underflow, IeeeFlags.Underflow | IeeeFlags.Inexact);
            foreach (string text in new[] { "0e99999999999999999999", "0.000e-99999999999999999999", "0x0p99999999999999999999" })
                Check(sign + text, negative ? BigInteger.One << 63 : 0, IeeeFlags.None);

            void Check(string text, BigInteger expected, IeeeFlags flags)
            {
                var ieee = new IeeeContext(format, mode);
                ApFloat r = ieee.Parse(text, Inv);
                Assert.True(expected == r.ToIeeeBits(11, 53), $"\"{text}\" {mode}: expected {Decode(expected, 11, 53)}, got {Describe(r)}");
                Assert.True(flags == ieee.Flags, $"\"{text}\" {mode}: expected {flags}, got {ieee.Flags}");
                Assert.True(ApFloat.TryParse(text, format, mode, Inv, out ApFloat t) && t.ToIeeeBits(11, 53) == r.ToIeeeBits(11, 53));
            }
        }

        // With a precision the exponent is unbounded, so past a long it's still too large.
        Assert.Throws<OverflowException>(() => ApFloat.Parse("1e99999999999999999999", 53));
        Assert.Throws<OverflowException>(() => new IeeeContext(53).Parse("1e-99999999999999999999"));
        Assert.False(ApFloat.TryParse("0x1p99999999999999999999", 53, RoundingMode.ToNearestEven, Inv, out _));
    }

    // ------------------------------------------------------------------
    // Hex and binary text
    // ------------------------------------------------------------------

    [Fact]
    public void HexAndBinaryRoundOnceIntoTheFormat()
    {
        var rng = new Random(1030);
        var formats = new[] { (11, 53), (8, 24), (5, 6) };
        for (int i = 0; i < 4000; i++)
        {
            var (w, p) = formats[rng.Next(formats.Length)];
            var format = new IeeeFormat(w, p);
            RoundingMode mode = Modes[rng.Next(4)];
            BigInteger m = Oracle.RandomBig(rng, 3, allowNegative: false);
            long exponent = rng.NextInt64(format.MinExponent - p - 70, format.MaxExponent + 4);
            bool negative = rng.Next(2) == 0;
            string text = (negative ? "-" : "") + (rng.Next(3) == 0 ? $"0b{(m.IsZero ? "0" : m.ToString("B", Inv).TrimStart('0'))}p{exponent}" : $"0x{m:X}p{exponent}");
            BigInteger num = negative ? -m : m, den = 1;
            if (exponent >= 0) num <<= (int)exponent; else den <<= (int)-exponent;

            var ieee = new IeeeContext(format, mode);
            ApFloat r = ieee.Parse(text);
            string at = $"\"{text}\" into ({w}, {p}) {mode}";
            BigInteger expected = EncodeRational(num, den, negative, w, p, mode);
            Assert.True(expected == r.ToIeeeBits(w, p), $"{at}: expected {Decode(expected, w, p)}, got {Describe(r)}");
            Assert.True(RoundingFlags(num, den, r, format, mode) == ieee.Flags, $"{at}: expected {RoundingFlags(num, den, r, format, mode)}, got {ieee.Flags}");
        }
    }

    // ------------------------------------------------------------------
    // The context, and the job
    // ------------------------------------------------------------------

    [Fact]
    public void TheContextRaisesWhatTheTextDeserves()
    {
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        Assert.Equal(0.5, (double)ieee.Parse("0.5"));
        Assert.True(ieee.Parse("NaN").IsNaN);
        AssertSame(ApFloat.NegativeInfinity, ieee.Parse("-Infinity"));
        Assert.Equal(IeeeFlags.None, ieee.Flags);

        Assert.Equal(0.1, (double)ieee.Parse("0.1"));
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);
        Assert.Throws<FormatException>(() => ieee.Parse("1e400x"));
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);                     // a failed parse raises nothing
        ieee.Parse("0.5");
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);                     // sticky: an exact parse doesn't lower it
        ieee.Parse("1e-400");
        ieee.Parse("1e400");
        Assert.Equal(IeeeFlags.Inexact | IeeeFlags.Underflow | IeeeFlags.Overflow, ieee.Flags);

        // In the context's mode, and with a precision only Inexact can be raised.
        ieee = new IeeeContext(IeeeFormat.Binary64, RoundingMode.TowardNegative);
        Assert.Equal(Math.BitDecrement(0.1), (double)ieee.Parse("0.1"));     // the nearest double is above 0.1
        var bits = new IeeeContext(80);
        AssertSame(ApFloat.Parse("1e-400", 80), bits.Parse("1e-400"));
        Assert.Equal(IeeeFlags.Inexact, bits.Flags);
        bits.ClearFlags();
        bits.Parse("0.25");
        Assert.Equal(IeeeFlags.None, bits.Flags);
    }

    [Fact]
    public void AJobIntoAFormatGivesWhatParseGives()
    {
        var rng = new Random(1040);
        var texts = new List<string> { "1e-99999999999999999999", "0x1.8p-1074", "NaN", "0", "-0.0e5" };
        for (int i = 0; i < 40; i++) texts.Add(MidpointText(rng, 11, 53));
        for (int i = 0; i < 10; i++) texts.Add(RandomText(rng, rng.Next(-330, 312)));
        texts.Add("1" + new string('7', 4500) + "e-4400");                  // long, and past the cache: the engine, in steps
        foreach (string text in texts)
        foreach (RoundingMode mode in Modes)
        {
            var job = ApFloat.StartParse(text, IeeeFormat.Binary64, mode, Inv);
            for (int calls = 0; !job.Continue(1); calls++) Assert.True(calls < 100_000, $"\"{text}\": the job never finished");
            AssertSame(ApFloat.Parse(text, IeeeFormat.Binary64, mode, Inv), job.Result, $"\"{text}\" {mode}");
        }
        Assert.Throws<FormatException>(() => ApFloat.StartParse("1.2.3", IeeeFormat.Binary32));
        Assert.Throws<ArgumentNullException>(() => ApFloat.StartParse(null!, IeeeFormat.Binary32));
    }
}

using System.Globalization;
using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// The certified interval engine: X × 10^n for huge n, from rigorous bounds on 5^n. Checked
/// against the rational oracle, against the exact route, and against values computed
/// offline in Python for exponents neither of those can reach.
/// </summary>
public class CertifiedTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Runs f with one route forced, and always puts the default back.</summary>
    private static T Routed<T>(ApFloat.Route route, Func<T> f)
    {
        ApFloat.ForcedRoute = route;
        try { return f(); }
        finally { ApFloat.ForcedRoute = ApFloat.Route.Auto; }
    }

    // ------------------------------------------------------------------
    // The pieces
    // ------------------------------------------------------------------

    [Fact]
    public void FloorLog2Of5IsOneShortOfTheBitLength()
    {
        var rng = new Random(400);
        var ns = new List<long> { 0, 1, 2, 3, 4, 5, 100, 4095, 4096, 99_999, 100_000 };
        for (int i = 0; i < 200; i++) ns.Add(rng.Next(0, 20_000));
        foreach (long n in ns)
        {
            Assert.Equal((long)BigInteger.Pow(5, (int)n).GetBitLength(), ApFloat.FloorLog2Of5Times(n) + 1);
            // It floors for negatives too (n · log2 5 is never a whole number).
            if (n > 0) Assert.Equal(-ApFloat.FloorLog2Of5Times(n) - 1, ApFloat.FloorLog2Of5Times(-n));
        }
        Assert.Equal(23_219_280, ApFloat.FloorLog2Of5Times(10_000_000));     // Python: 5**10**7 has 23,219,281 bits
        Assert.Equal(2_321_928_094_887_362_347, ApFloat.FloorLog2Of5Times(1_000_000_000_000_000_000));   // log2 5 = 2.3219280948873623478...
    }

    [Fact]
    public void PowerOfFiveBoundsBracketAndAreTight()
    {
        foreach (long n in new long[] { 0, 1, 2, 3, 7, 64, 1000, 4095, 4096, 12_345, 30_001 })
        {
            BigInteger exact = BigInteger.Pow(5, (int)n);
            long exactBits = (long)exact.GetBitLength();
            // Squaring doubles a relative error, so a rounding in 5^(2^k) counts as many times
            // as that square goes into 5^n: about n in all, plus one per multiply.
            long weight = n + 2 * (64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)n));
            foreach (int w in new[] { 1, 2, 3, 8, 24, 53, 117, 300, 1000, 3000 })
            {
                var (lo, hi) = ApFloat.PowerOfFiveBounds(n, w);
                var (loNum, loDen) = ToRational(lo);
                var (hiNum, hiDen) = ToRational(hi);
                string at = $"5^{n} at {w} bits";
                Assert.True(loNum <= exact * loDen, at + ": lo is above it");
                Assert.True(exact * hiDen <= hiNum, at + ": hi is below it");
                Assert.True(lo.Significand.BitLength <= w && hi.Significand.BitLength <= w, at + ": too many bits");
                if (w >= exactBits)
                {
                    // Every partial product and square fits, so nothing is rounded.
                    Assert.True(loNum == exact * loDen && hiNum == exact * hiDen, at + ": should be exact");
                }
                else if (w >= 8 && weight < (1L << Math.Min(w - 4, 62)))
                {
                    // Each rounding is off by under 2^(1-w), relatively, so the bounds are within
                    // (1 ± 2^(1-w))^weight of 5^n: about weight · 2^(2-w) apart.
                    BigInteger width = hiNum * loDen - loNum * hiDen;
                    Assert.True((width << w) <= exact * hiDen * loDen * 5 * weight, at + ": too wide");
                    // And not absurdly tight either: at these sizes some rounding always happens.
                    Assert.True(width > 0, at + ": exact, but shouldn't be");
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Against the rational oracle
    // ------------------------------------------------------------------

    /// <summary>
    /// A random decimal as text, and its exact value num / den. The digits are biased to
    /// runs of 9s and to a 1 then 0s, the decimal version of the limb bias. The power of
    /// ten on the last digit has magnitude from <paramref name="minScale"/> to
    /// <paramref name="maxScale"/>, either sign.
    /// </summary>
    private static (string Text, BigInteger Num, BigInteger Den, bool Negative) RandomDecimal(Random rng, int minScale, int maxScale)
    {
        int length = rng.Next(1, 45);
        var digits = new char[length];
        int style = rng.Next(4);
        for (int j = 0; j < length; j++)
            digits[j] = style switch { 0 => '9', 1 => j == 0 ? '1' : '0', _ => (char)('0' + rng.Next(10)) };
        if (style < 2 && length > 3) digits[rng.Next(length)] = (char)('0' + rng.Next(10));
        int point = rng.Next(length + 1);
        long scale = rng.Next(minScale, maxScale + 1) * (rng.Next(2) == 0 ? 1L : -1L);
        long exponent = scale + (length - point);
        bool negative = rng.Next(2) == 0;
        string text = $"{(negative ? "-" : "")}{new string(digits, 0, point)}.{new string(digits, point, length - point)}e{exponent}";
        BigInteger num = BigInteger.Parse(new string(digits), Inv);
        BigInteger den = BigInteger.One;
        if (scale >= 0) num *= BigInteger.Pow(10, (int)scale);
        else den = BigInteger.Pow(10, (int)-scale);
        return (text, num, den, negative);
    }

    [Fact]
    public void ForcedIntervalsParseCorrectly()
    {
        // Every power through the interval engine, even below the cache, where it has to run
        // on until the bounds are exact for ties and exact values.
        var rng = new Random(401);
        for (int i = 0; i < 2000; i++)
        {
            var (text, num, den, negative) = RandomDecimal(rng, 0, 400);
            int p = rng.Next(1, 201);
            RoundingMode mode = Modes[rng.Next(4)];
            Rounded expected = Round(negative ? -num : num, den, p, mode) with { Negative = negative };
            ApFloat actual = Routed(ApFloat.Route.Interval, () => ApFloat.Parse(text, p, mode, Inv));
            AssertRounded(expected, actual, p, $"Parse(\"{text}\", {p}, {mode}) by intervals");
        }
    }

    [Fact]
    public void ParsePastTheCacheIsCertified()
    {
        // Past 5^4096 the default route is intervals: right, and settled without the exact route.
        var rng = new Random(402);
        int rounds = 0;
        for (int i = 0; i < 300; i++)
        {
            var (text, num, den, negative) = RandomDecimal(rng, 4096, 30_000);
            int p = rng.Next(1, 301);
            RoundingMode mode = Modes[rng.Next(4)];
            Rounded expected = Round(negative ? -num : num, den, p, mode) with { Negative = negative };
            ApFloat actual = ApFloat.Parse(text, p, mode, Inv);
            AssertRounded(expected, actual, p, $"Parse(\"{text}\", {p}, {mode})");
            if (num.IsZero) continue;
            Assert.False(ApFloat.LastExact, $"\"{text}\" fell back to the exact route");
            rounds += ApFloat.LastRounds;
        }
        Assert.True(rounds < 330, $"{rounds} rounds for about 300 parses: almost all should settle in one");
    }

    [Fact]
    public void ForcedIntervalsFormatCorrectly()
    {
        var rng = new Random(403);
        for (int i = 0; i < 1000; i++)
        {
            int p = rng.Next(1, 201);
            ApFloat x = RandomFinite(rng, 7, 400).WithPrecision(p);
            int n = rng.Next(0, 30);
            RoundingMode mode = Modes[rng.Next(4)];
            string format = rng.Next(2) == 0 ? "E" + n : "F" + n;
            string expected = format[0] == 'E' ? Scientific(x, n, mode).Text : Fixed(x, n, mode).Text;
            string actual = Routed(ApFloat.Route.Interval, () => x.ToString(format, Inv, mode));
            Assert.True(expected == actual, $"{Describe(x)}.ToString(\"{format}\", {mode}) by intervals\n  expected {expected}\n  actual   {actual}");
        }
    }

    [Fact]
    public void FormatPastTheCacheMatchesOracle()
    {
        // Binary exponents out to ±60,000, so decimal exponents out to about ±18,000. (The
        // oracle's BigInteger division is what costs here.)
        var rng = new Random(404);
        for (int i = 0; i < 200; i++)
        {
            int p = rng.Next(1, 201);
            ApFloat x = RandomFinite(rng, 7, 60_000).WithPrecision(p);
            if (x.IsZero) continue;
            int n = rng.Next(0, 40);
            RoundingMode mode = Modes[rng.Next(4)];
            string expected = Scientific(x, n, mode).Text;
            string actual = x.ToString("E" + n, Inv, mode);
            Assert.True(expected == actual, $"{Describe(x)}.ToString(\"E{n}\", {mode})\n  expected {expected}\n  actual   {actual}");

            Assert.Equal(Scientific(x, ApFloat.DecimalDigitsFor(p) - 1, RoundingMode.ToNearestEven).Text, x.ToString(null, Inv));
            AssertSame(x, ApFloat.Parse(x.ToString("R", Inv), p, RoundingMode.ToNearestEven, Inv), $"R of {Describe(x)}");
        }
    }

    [Fact]
    public void ExactAndIntervalRoutesAgree()
    {
        // The exact route is the slow one this engine replaces (past the cache it builds 5^n
        // afresh every time), so only a few, and not far past the cache.
        var rng = new Random(405);
        for (int i = 0; i < 6; i++)
        {
            var (text, _, _, _) = RandomDecimal(rng, 4096, 5500);
            int p = rng.Next(1, 301);
            RoundingMode mode = Modes[rng.Next(4)];
            ApFloat viaIntervals = ApFloat.Parse(text, p, mode, Inv);
            ApFloat viaExact = Routed(ApFloat.Route.Exact, () => ApFloat.Parse(text, p, mode, Inv));
            AssertSame(viaExact, viaIntervals, $"Parse(\"{text}\", {p}, {mode})");
            if (viaExact.IsZero) continue;
            Assert.Equal(Routed(ApFloat.Route.Exact, () => viaExact.ToString(null, Inv)), viaIntervals.ToString(null, Inv));
        }
    }

    // ------------------------------------------------------------------
    // Hard cases: decimals a hair from a rounding boundary
    // ------------------------------------------------------------------

    /// <summary>
    /// A rounding boundary at p bits, (odd) × 2^e, written out exactly in decimal: digits × 10^scale.
    /// For rounding to nearest the boundaries are the midpoints (odd has p + 1 bits); for the
    /// directed modes they're the representable values themselves (odd has p bits).
    /// </summary>
    private static (BigInteger Digits, long Scale) Boundary(Random rng, int p, long e, bool midpoint)
    {
        int bits = midpoint ? p + 1 : p;
        BigInteger low = BigInteger.Abs(Oracle.RandomBig(rng, 4)) & ((BigInteger.One << (bits - 1)) - 1);
        BigInteger odd = (BigInteger.One << (bits - 1)) | low | 1;
        // odd × 2^e is odd × 5^-e × 10^e when e < 0; when e >= 0 it's an integer already.
        return e >= 0 ? (odd << (int)e, 0) : (odd * BigInteger.Pow(5, (int)-e), e);
    }

    private static Rounded Expected(BigInteger digits, long scale, int p, RoundingMode mode) =>
        scale >= 0 ? Round(digits * BigInteger.Pow(10, (int)scale), 1, p, mode) : Round(digits, BigInteger.Pow(10, (int)-scale), p, mode);

    [Theory]
    [InlineData(24, 40)]
    [InlineData(53, 100)]
    [InlineData(113, 100)]
    [InlineData(53, 250)]
    public void NearBoundariesTakeMoreRounds(int p, int keep)
    {
        // Cut the exact digits after `keep` of them: just below the boundary. One more in the
        // last place: just above. Both are within 10^(1-keep) of it, relatively, so the first
        // round (p + 64 bits) can't settle them once keep·log2(10) is past about p + 57.
        var rng = new Random(406 + p + keep);
        foreach (long e in new long[] { -14_000, 14_000 + 4L * keep })        // scales past the cache either way
        {
            foreach (RoundingMode mode in Modes)
            {
                var (digits, scale) = Boundary(rng, p, e, midpoint: mode == RoundingMode.ToNearestEven);
                string all = digits.ToString(Inv);
                BigInteger below = BigInteger.Parse(all[..keep], Inv);
                long cutScale = scale + (all.Length - keep);
                Assert.True(Math.Abs(cutScale) >= 4096);
                foreach (BigInteger q in new[] { below, below + 1 })
                {
                    string text = $"{q}e{cutScale}";
                    ApFloat actual = ApFloat.Parse(text, p, mode, Inv);
                    AssertRounded(Expected(q, cutScale, p, mode), actual, p, $"Parse(\"{text}\", {p}, {mode})");
                    Assert.False(ApFloat.LastExact, $"\"{text}\" fell back to the exact route");
                    if (keep * 3.32 > p + 70)
                        Assert.True(ApFloat.LastRounds >= 2, $"\"{text}\" settled in {ApFloat.LastRounds} round(s)");
                }
            }
        }
    }

    [Theory]
    [InlineData(24)]
    [InlineData(53)]
    [InlineData(113)]
    public void AHairFromABoundaryWhereTheBoundsAreExact(int p)
    {
        // Forced through intervals with a small power of five (5^25 when parsing, 5^0 when
        // formatting "F0"), the bounds on it are exact in the first round, and only the outward
        // rounding of X × bound or X / bound keeps the two ends apart. Each value here is far
        // less than half an ulp of the working precision from a boundary, so rounding that
        // product or quotient to nearest instead would put an end on the boundary itself: a
        // midpoint whose tie goes the wrong way, or the representable value being approached.
        // (Past the cache the bounds on 5^n are thousands of ulps apart, which hides this.)
        var rng = new Random(408 + p);
        string tiny = new string('0', 24) + "1";          // after the point: 10^-25
        string nines = new string('9', 25);               // after the point: 1 - 10^-25
        for (int i = 0; i < 8; i++)
        {
            BigInteger m = (BigInteger.One << (p - 1)) | (BigInteger.Abs(Oracle.RandomBig(rng, 4)) & ((BigInteger.One << (p - 1)) - 1));
            BigInteger odd = m | BigInteger.One, even = m & ~BigInteger.One;
            (string Text, RoundingMode Mode)[] cases =
            [
                ($"{odd}.4{nines[1..]}", RoundingMode.ToNearestEven),       // below odd + 1/2, whose tie goes up
                ($"{even}.5{tiny[1..]}", RoundingMode.ToNearestEven),       // above even + 1/2, whose tie goes down
                ($"{m - 1}.{nines}", RoundingMode.TowardZero),              // just below m
                ($"{m}.{tiny}", RoundingMode.TowardPositive),               // just above m
            ];
            foreach (var (text, mode) in cases)
            {
                foreach (bool negative in new[] { false, true })
                {
                    // Mirrored for negatives: the same magnitudes, rounding the same way.
                    string signed = negative ? "-" + text : text;
                    RoundingMode signedMode = negative && mode == RoundingMode.TowardPositive ? RoundingMode.TowardNegative : mode;
                    BigInteger num = BigInteger.Parse(text.Replace(".", ""), Inv);
                    Rounded expected = Round(negative ? -num : num, BigInteger.Pow(10, 25), p, signedMode) with { Negative = negative };
                    ApFloat parsed = Routed(ApFloat.Route.Interval, () => ApFloat.Parse(signed, p, signedMode, Inv));
                    AssertRounded(expected, parsed, p, $"Parse(\"{signed}\", {p}, {signedMode}) by intervals");

                    // Nearly the same value in binary (far closer than 10^-25), printed to a whole
                    // number: formatting's side, where the bound is 5^0 and X × 1 is rounded.
                    ApFloat close = ApFloat.Parse(signed, 4 * p + 200, RoundingMode.ToNearestEven, Inv);
                    string whole = Routed(ApFloat.Route.Interval, () => close.ToString("F0", Inv, signedMode));
                    Assert.Equal(Fixed(close, 0, signedMode).Text, whole);
                }
            }
        }
    }

    [Theory]
    [InlineData(24)]
    [InlineData(53)]
    [InlineData(113)]
    public void TiesAndExactValuesFallBackToTheExactRoute(int p)
    {
        // Written out in full (about 2,900 digits, times 10^-4100), a boundary can't be settled
        // by any interval: the ends always straddle it. So these are the exact route's.
        var rng = new Random(407 + p);
        var (tie, tieScale) = Boundary(rng, p, -4100, midpoint: true);
        string text = $"{tie}e{tieScale}";
        AssertRounded(Expected(tie, tieScale, p, RoundingMode.ToNearestEven), ApFloat.Parse(text, p, RoundingMode.ToNearestEven, Inv), p, "a tie");
        Assert.True(ApFloat.LastExact, "a tie was settled by intervals");

        var (exact, exactScale) = Boundary(rng, p, -4100, midpoint: false);
        text = $"{exact}e{exactScale}";
        ApFloat chopped = ApFloat.Parse(text, p, RoundingMode.TowardZero, Inv);
        AssertRounded(Expected(exact, exactScale, p, RoundingMode.TowardZero), chopped, p, "an exact value, toward zero");
        Assert.True(ApFloat.LastExact, "an exact value toward zero was settled by intervals");

        // To nearest, though, an exact value is nowhere near a boundary, and both ends round to it.
        AssertSame(chopped, ApFloat.Parse(text, p, RoundingMode.ToNearestEven, Inv), "an exact value, to nearest");
        Assert.False(ApFloat.LastExact, "an exact value to nearest fell back");
    }

    // ------------------------------------------------------------------
    // Huge exponents, against Python
    // ------------------------------------------------------------------

    // Computed offline with exact Python integers (tests/reference/certified_reference.py, run
    // with N = 5000 and with the default N = 10^7, about 5 s): 5**N built in full, then one exact
    // division per value, so nothing is shared with the engine. The N = 5000 rows are within
    // the exact route's reach too, and go through it as well: that checks the script against
    // code written before this engine existed.

    [Theory]
    [InlineData("1e5000", 53, RoundingMode.ToNearestEven, "+0x31E2080103651p16560", true)]
    [InlineData("1e5000", 53, RoundingMode.TowardZero, "+0x31E2080103651p16560", true)]
    [InlineData("1e5000", 53, RoundingMode.TowardPositive, "+0x18F1040081B289p16557", true)]
    [InlineData("1e5000", 53, RoundingMode.TowardNegative, "+0x31E2080103651p16560", true)]
    [InlineData("1e5000", 256, RoundingMode.ToNearestEven, "+0x63C4100206CA21E78B23FBCA6C3B8C3330F12CE01E88A3C390B9C3205228D5D1p16355", true)]
    [InlineData("1e5000", 256, RoundingMode.TowardZero, "+0x63C4100206CA21E78B23FBCA6C3B8C3330F12CE01E88A3C390B9C3205228D5D1p16355", true)]
    [InlineData("1e5000", 256, RoundingMode.TowardPositive, "+0xC78820040D9443CF1647F794D877186661E259C03D11478721738640A451ABA3p16354", true)]
    [InlineData("1e5000", 256, RoundingMode.TowardNegative, "+0x63C4100206CA21E78B23FBCA6C3B8C3330F12CE01E88A3C390B9C3205228D5D1p16355", true)]
    [InlineData("1e-5000", 53, RoundingMode.ToNearestEven, "+0xA43978D593B69p-16661", true)]
    [InlineData("1e-5000", 53, RoundingMode.TowardZero, "+0x14872F1AB276D1p-16662", true)]
    [InlineData("1e-5000", 53, RoundingMode.TowardPositive, "+0xA43978D593B69p-16661", true)]
    [InlineData("1e-5000", 53, RoundingMode.TowardNegative, "+0x14872F1AB276D1p-16662", true)]
    [InlineData("1e-5000", 256, RoundingMode.ToNearestEven, "+0x521CBC6AC9DB479FA242F134A7CF94F77A0F5283B9A4877AD601E7FB6308CEF7p-16864", true)]
    [InlineData("1e-5000", 256, RoundingMode.TowardZero, "+0xA43978D593B68F3F4485E2694F9F29EEF41EA50773490EF5AC03CFF6C6119DEDp-16865", true)]
    [InlineData("1e-5000", 256, RoundingMode.TowardPositive, "+0x521CBC6AC9DB479FA242F134A7CF94F77A0F5283B9A4877AD601E7FB6308CEF7p-16864", true)]
    [InlineData("1e-5000", 256, RoundingMode.TowardNegative, "+0xA43978D593B68F3F4485E2694F9F29EEF41EA50773490EF5AC03CFF6C6119DEDp-16865", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 53, RoundingMode.ToNearestEven, "-0x1ECABC3B2F987p16561", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 53, RoundingMode.TowardZero, "-0x1ECABC3B2F986Fp16557", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 53, RoundingMode.TowardPositive, "-0x1ECABC3B2F986Fp16557", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 53, RoundingMode.TowardNegative, "-0x1ECABC3B2F987p16561", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 256, RoundingMode.ToNearestEven, "-0xF655E1D97CC37CE1DC0CEABF666840F03135787A07D330D70CE0FA12887BB4Bp16358", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 256, RoundingMode.TowardZero, "-0xF655E1D97CC37CE1DC0CEABF666840F03135787A07D330D70CE0FA12887BB4AFp16354", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 256, RoundingMode.TowardPositive, "-0xF655E1D97CC37CE1DC0CEABF666840F03135787A07D330D70CE0FA12887BB4AFp16354", true)]
    [InlineData("-1.2345678901234567890123456789e5000", 256, RoundingMode.TowardNegative, "-0xF655E1D97CC37CE1DC0CEABF666840F03135787A07D330D70CE0FA12887BB4Bp16358", true)]
    [InlineData("9.99999999999999999999e-5001", 53, RoundingMode.ToNearestEven, "+0xA43978D593B69p-16661", true)]
    [InlineData("9.99999999999999999999e-5001", 53, RoundingMode.TowardZero, "+0x14872F1AB276D1p-16662", true)]
    [InlineData("9.99999999999999999999e-5001", 53, RoundingMode.TowardPositive, "+0xA43978D593B69p-16661", true)]
    [InlineData("9.99999999999999999999e-5001", 53, RoundingMode.TowardNegative, "+0x14872F1AB276D1p-16662", true)]
    [InlineData("9.99999999999999999999e-5001", 256, RoundingMode.ToNearestEven, "+0x521CBC6AC9DB479FA0BF2D9627048585BCD99540B9BD28C45C58639992D35897p-16864", true)]
    [InlineData("9.99999999999999999999e-5001", 256, RoundingMode.TowardZero, "+0xA43978D593B68F3F417E5B2C4E090B0B79B32A81737A5188B8B0C73325A6B12Dp-16865", true)]
    [InlineData("9.99999999999999999999e-5001", 256, RoundingMode.TowardPositive, "+0x521CBC6AC9DB479FA0BF2D9627048585BCD99540B9BD28C45C58639992D35897p-16864", true)]
    [InlineData("9.99999999999999999999e-5001", 256, RoundingMode.TowardNegative, "+0xA43978D593B68F3F417E5B2C4E090B0B79B32A81737A5188B8B0C73325A6B12Dp-16865", true)]
    [InlineData("1e10000000", 53, RoundingMode.ToNearestEven, "+0x7B8B196B530CDp33219230", false)]
    [InlineData("1e10000000", 53, RoundingMode.TowardZero, "+0x1EE2C65AD4C333p33219228", false)]
    [InlineData("1e10000000", 53, RoundingMode.TowardPositive, "+0x7B8B196B530CDp33219230", false)]
    [InlineData("1e10000000", 53, RoundingMode.TowardNegative, "+0x1EE2C65AD4C333p33219228", false)]
    [InlineData("1e10000000", 256, RoundingMode.ToNearestEven, "+0xF71632D6A6199F6BBC60772EB5DD6AE34A5C2C603E59C6AAE980102C5301DF17p33219025", false)]
    [InlineData("1e10000000", 256, RoundingMode.TowardZero, "+0xF71632D6A6199F6BBC60772EB5DD6AE34A5C2C603E59C6AAE980102C5301DF17p33219025", false)]
    [InlineData("1e10000000", 256, RoundingMode.TowardPositive, "+0x1EE2C65AD4C333ED778C0EE5D6BBAD5C694B858C07CB38D55D3002058A603BE3p33219028", false)]
    [InlineData("1e10000000", 256, RoundingMode.TowardNegative, "+0xF71632D6A6199F6BBC60772EB5DD6AE34A5C2C603E59C6AAE980102C5301DF17p33219025", false)]
    [InlineData("1e-10000000", 53, RoundingMode.ToNearestEven, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("1e-10000000", 53, RoundingMode.TowardZero, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("1e-10000000", 53, RoundingMode.TowardPositive, "+0x849E0E980621p-33219328", false)]
    [InlineData("1e-10000000", 53, RoundingMode.TowardNegative, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("1e-10000000", 256, RoundingMode.ToNearestEven, "+0x849E0E980620FA32BC4AD76B12AE44BA8F715AEC8561BF3437A2593EEDFFC95Bp-33219536", false)]
    [InlineData("1e-10000000", 256, RoundingMode.TowardZero, "+0x849E0E980620FA32BC4AD76B12AE44BA8F715AEC8561BF3437A2593EEDFFC95Bp-33219536", false)]
    [InlineData("1e-10000000", 256, RoundingMode.TowardPositive, "+0x212783A601883E8CAF12B5DAC4AB912EA3DC56BB21586FCD0DE8964FBB7FF257p-33219534", false)]
    [InlineData("1e-10000000", 256, RoundingMode.TowardNegative, "+0x849E0E980620FA32BC4AD76B12AE44BA8F715AEC8561BF3437A2593EEDFFC95Bp-33219536", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 53, RoundingMode.ToNearestEven, "-0x9885CD1E74EF9p33219230", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 53, RoundingMode.TowardZero, "-0x1310B9A3CE9DF1p33219229", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 53, RoundingMode.TowardPositive, "-0x1310B9A3CE9DF1p33219229", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 53, RoundingMode.TowardNegative, "-0x9885CD1E74EF9p33219230", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 256, RoundingMode.ToNearestEven, "-0x9885CD1E74EF8D49018146F2A274B1E8040AED77EAC7CB60A95A37E87C74E82Dp33219026", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 256, RoundingMode.TowardZero, "-0x9885CD1E74EF8D49018146F2A274B1E8040AED77EAC7CB60A95A37E87C74E82Dp33219026", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 256, RoundingMode.TowardPositive, "-0x9885CD1E74EF8D49018146F2A274B1E8040AED77EAC7CB60A95A37E87C74E82Dp33219026", false)]
    [InlineData("-1.2345678901234567890123456789e10000000", 256, RoundingMode.TowardNegative, "-0x4C42E68F3A77C6A480C0A379513A58F4020576BBF563E5B054AD1BF43E3A7417p33219027", false)]
    [InlineData("9.99999999999999999999e-10000001", 53, RoundingMode.ToNearestEven, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("9.99999999999999999999e-10000001", 53, RoundingMode.TowardZero, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("9.99999999999999999999e-10000001", 53, RoundingMode.TowardPositive, "+0x849E0E980621p-33219328", false)]
    [InlineData("9.99999999999999999999e-10000001", 53, RoundingMode.TowardNegative, "+0x1093C1D300C41Fp-33219333", false)]
    [InlineData("9.99999999999999999999e-10000001", 256, RoundingMode.ToNearestEven, "+0x849E0E980620FA32B9D892CE945F308A1145780200F354F27AF039E80F7F1DCDp-33219536", false)]
    [InlineData("9.99999999999999999999e-10000001", 256, RoundingMode.TowardZero, "+0x849E0E980620FA32B9D892CE945F308A1145780200F354F27AF039E80F7F1DCDp-33219536", false)]
    [InlineData("9.99999999999999999999e-10000001", 256, RoundingMode.TowardPositive, "+0x424F074C03107D195CEC49674A2F984508A2BC010079AA793D781CF407BF8EE7p-33219535", false)]
    [InlineData("9.99999999999999999999e-10000001", 256, RoundingMode.TowardNegative, "+0x849E0E980620FA32B9D892CE945F308A1145780200F354F27AF039E80F7F1DCDp-33219536", false)]
    public void ParseAgainstPython(string text, int precision, RoundingMode mode, string expected, bool exactToo)
    {
        ApFloat x = ApFloat.ParseUncapped(text, precision, mode, Inv);
        AssertIs(expected, x, text);
        Assert.Equal(precision, x.Precision);
        Assert.False(ApFloat.LastExact);
        if (exactToo)
            AssertIs(expected, Routed(ApFloat.Route.Exact, () => ApFloat.Parse(text, precision, mode, Inv)), text + " (exact)");
    }

    /// <summary>
    /// x against Python's "±0x&lt;odd hex&gt;p&lt;exponent&gt;", by value: BigInteger's hex puts a
    /// 0 in front when the top digit is 8 or more, and Python's doesn't.
    /// </summary>
    private static void AssertIs(string expected, ApFloat x, string context)
    {
        int p = expected.IndexOf('p');
        BigInteger m = BigInteger.Parse("0" + expected[3..p], NumberStyles.HexNumber, Inv);
        long e = long.Parse(expected[(p + 1)..], Inv);
        Assert.True(x.IsFinite && x.IsNegative == (expected[0] == '-') && Oracle.ToBig(x.Significand) == m && x.Exponent == e,
            $"{context}\n  expected {expected}\n  actual   {Describe(x)}");
    }

    [Theory]
    [InlineData("0x1p+16609", 53, "E16", RoundingMode.ToNearestEven, "6.4150195326120695E+4999", true)]
    [InlineData("0x1p+16609", 256, "E78", RoundingMode.ToNearestEven, "6.415019532612069481527967666699475928748803281480064725703187577091465642920469E+4999", true)]
    [InlineData("0x1p+16609", 53, "E5", RoundingMode.ToNearestEven, "6.41502E+4999", true)]
    [InlineData("0x1p+16609", 53, "E5", RoundingMode.TowardZero, "6.41501E+4999", true)]
    [InlineData("0x1p+16609", 53, "E5", RoundingMode.TowardPositive, "6.41502E+4999", true)]
    [InlineData("0x1p+16609", 53, "E5", RoundingMode.TowardNegative, "6.41501E+4999", true)]
    [InlineData("0x1p-16609", 53, "E16", RoundingMode.ToNearestEven, "1.5588417072096111E-5000", true)]
    [InlineData("0x1p-16609", 256, "E78", RoundingMode.ToNearestEven, "1.558841707209611121801965753322285758071798828269212114086459311425349155095454E-5000", true)]
    [InlineData("0x1p-16609", 53, "E5", RoundingMode.ToNearestEven, "1.55884E-5000", true)]
    [InlineData("0x1p-16609", 53, "E5", RoundingMode.TowardZero, "1.55884E-5000", true)]
    [InlineData("0x1p-16609", 53, "E5", RoundingMode.TowardPositive, "1.55885E-5000", true)]
    [InlineData("0x1p-16609", 53, "E5", RoundingMode.TowardNegative, "1.55884E-5000", true)]
    [InlineData("1e5000 @53", 53, "E16", RoundingMode.ToNearestEven, "9.9999999999999993E+4999", true)]
    [InlineData("1e5000 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E+5000", true)]
    [InlineData("1e5000 @53", 53, "E5", RoundingMode.TowardZero, "9.99999E+4999", true)]
    [InlineData("1e5000 @53", 53, "E5", RoundingMode.TowardPositive, "1.00000E+5000", true)]
    [InlineData("1e5000 @53", 53, "E5", RoundingMode.TowardNegative, "9.99999E+4999", true)]
    [InlineData("1e-5000 @53", 53, "E16", RoundingMode.ToNearestEven, "1.0000000000000000E-5000", true)]
    [InlineData("1e-5000 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E-5000", true)]
    [InlineData("1e-5000 @53", 53, "E5", RoundingMode.TowardZero, "1.00000E-5000", true)]
    [InlineData("1e-5000 @53", 53, "E5", RoundingMode.TowardPositive, "1.00001E-5000", true)]
    [InlineData("1e-5000 @53", 53, "E5", RoundingMode.TowardNegative, "1.00000E-5000", true)]
    [InlineData("-1.2345678901234567890123456789e5000 @53", 53, "E16", RoundingMode.ToNearestEven, "-1.2345678901234568E+5000", true)]
    [InlineData("-1.2345678901234567890123456789e5000 @53", 53, "E5", RoundingMode.ToNearestEven, "-1.23457E+5000", true)]
    [InlineData("-1.2345678901234567890123456789e5000 @53", 53, "E5", RoundingMode.TowardZero, "-1.23456E+5000", true)]
    [InlineData("-1.2345678901234567890123456789e5000 @53", 53, "E5", RoundingMode.TowardPositive, "-1.23456E+5000", true)]
    [InlineData("-1.2345678901234567890123456789e5000 @53", 53, "E5", RoundingMode.TowardNegative, "-1.23457E+5000", true)]
    [InlineData("9.99999999999999999999e-5001 @53", 53, "E16", RoundingMode.ToNearestEven, "1.0000000000000000E-5000", true)]
    [InlineData("9.99999999999999999999e-5001 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E-5000", true)]
    [InlineData("9.99999999999999999999e-5001 @53", 53, "E5", RoundingMode.TowardZero, "1.00000E-5000", true)]
    [InlineData("9.99999999999999999999e-5001 @53", 53, "E5", RoundingMode.TowardPositive, "1.00001E-5000", true)]
    [InlineData("9.99999999999999999999e-5001 @53", 53, "E5", RoundingMode.TowardNegative, "1.00000E-5000", true)]
    [InlineData("0x1p+33219280", 53, "E16", RoundingMode.ToNearestEven, "5.1803675853273382E+9999999", false)]
    [InlineData("0x1p+33219280", 256, "E78", RoundingMode.ToNearestEven, "5.180367585327338181026226199753790380849390960317706976419316589996939188667819E+9999999", false)]
    [InlineData("0x1p+33219280", 53, "E5", RoundingMode.ToNearestEven, "5.18037E+9999999", false)]
    [InlineData("0x1p+33219280", 53, "E5", RoundingMode.TowardZero, "5.18036E+9999999", false)]
    [InlineData("0x1p+33219280", 53, "E5", RoundingMode.TowardPositive, "5.18037E+9999999", false)]
    [InlineData("0x1p+33219280", 53, "E5", RoundingMode.TowardNegative, "5.18036E+9999999", false)]
    [InlineData("0x1p-33219280", 53, "E16", RoundingMode.ToNearestEven, "1.9303649471368773E-10000000", false)]
    [InlineData("0x1p-33219280", 256, "E78", RoundingMode.ToNearestEven, "1.930364947136877324408520188419533041107897785424394887679263468954198940681498E-10000000", false)]
    [InlineData("0x1p-33219280", 53, "E5", RoundingMode.ToNearestEven, "1.93036E-10000000", false)]
    [InlineData("0x1p-33219280", 53, "E5", RoundingMode.TowardZero, "1.93036E-10000000", false)]
    [InlineData("0x1p-33219280", 53, "E5", RoundingMode.TowardPositive, "1.93037E-10000000", false)]
    [InlineData("0x1p-33219280", 53, "E5", RoundingMode.TowardNegative, "1.93036E-10000000", false)]
    [InlineData("1e10000000 @53", 53, "E16", RoundingMode.ToNearestEven, "1.0000000000000000E+10000000", false)]
    [InlineData("1e10000000 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E+10000000", false)]
    [InlineData("1e10000000 @53", 53, "E5", RoundingMode.TowardZero, "1.00000E+10000000", false)]
    [InlineData("1e10000000 @53", 53, "E5", RoundingMode.TowardPositive, "1.00001E+10000000", false)]
    [InlineData("1e10000000 @53", 53, "E5", RoundingMode.TowardNegative, "1.00000E+10000000", false)]
    [InlineData("1e-10000000 @53", 53, "E16", RoundingMode.ToNearestEven, "9.9999999999999994E-10000001", false)]
    [InlineData("1e-10000000 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E-10000000", false)]
    [InlineData("1e-10000000 @53", 53, "E5", RoundingMode.TowardZero, "9.99999E-10000001", false)]
    [InlineData("1e-10000000 @53", 53, "E5", RoundingMode.TowardPositive, "1.00000E-10000000", false)]
    [InlineData("1e-10000000 @53", 53, "E5", RoundingMode.TowardNegative, "9.99999E-10000001", false)]
    [InlineData("-1.2345678901234567890123456789e10000000 @53", 53, "E16", RoundingMode.ToNearestEven, "-1.2345678901234569E+10000000", false)]
    [InlineData("-1.2345678901234567890123456789e10000000 @53", 53, "E5", RoundingMode.ToNearestEven, "-1.23457E+10000000", false)]
    [InlineData("-1.2345678901234567890123456789e10000000 @53", 53, "E5", RoundingMode.TowardZero, "-1.23456E+10000000", false)]
    [InlineData("-1.2345678901234567890123456789e10000000 @53", 53, "E5", RoundingMode.TowardPositive, "-1.23456E+10000000", false)]
    [InlineData("-1.2345678901234567890123456789e10000000 @53", 53, "E5", RoundingMode.TowardNegative, "-1.23457E+10000000", false)]
    [InlineData("9.99999999999999999999e-10000001 @53", 53, "E16", RoundingMode.ToNearestEven, "9.9999999999999994E-10000001", false)]
    [InlineData("9.99999999999999999999e-10000001 @53", 53, "E5", RoundingMode.ToNearestEven, "1.00000E-10000000", false)]
    [InlineData("9.99999999999999999999e-10000001 @53", 53, "E5", RoundingMode.TowardZero, "9.99999E-10000001", false)]
    [InlineData("9.99999999999999999999e-10000001 @53", 53, "E5", RoundingMode.TowardPositive, "1.00000E-10000000", false)]
    [InlineData("9.99999999999999999999e-10000001 @53", 53, "E5", RoundingMode.TowardNegative, "9.99999E-10000001", false)]
    public void FormatAgainstPython(string value, int precision, string format, RoundingMode mode, string expected, bool exactToo)
    {
        // "0x..." is exact in hex; "<decimal> @53" is that decimal parsed at 53 bits.
        ApFloat x = value.EndsWith(" @53", StringComparison.Ordinal)
            ? ApFloat.ParseUncapped(value[..^4], 53, RoundingMode.ToNearestEven, Inv)
            : ApFloat.Parse(value, precision, RoundingMode.ToNearestEven, Inv);
        Assert.Equal(expected, x.ToString(format, Inv, mode));
        if (mode == RoundingMode.ToNearestEven && format == $"E{ApFloat.DecimalDigitsFor(precision) - 1}")
            Assert.Equal(expected, x.ToString(null, Inv));
        if (exactToo && mode == RoundingMode.ToNearestEven)     // the other modes too would be 3 s more
            Assert.Equal(expected, Routed(ApFloat.Route.Exact, () => x.ToString(format, Inv, mode)));
    }

    [Theory]
    [InlineData("1e10000000")]
    [InlineData("1e-10000000")]
    [InlineData("-7.25e123456789")]
    [InlineData("1e1000000000000000000")]
    [InlineData("-3e-1000000000000000000")]
    public void HugeExponentsRoundTrip(string text)
    {
        // "R" gives the shortest text that reads back, and a short input is its own shortest.
        ApFloat x = ApFloat.ParseUncapped(text, 53, RoundingMode.ToNearestEven, Inv);
        Assert.Equal(Canonical(text), Canonical(x.ToString("R", Inv)));
        AssertSame(x, ApFloat.ParseUncapped(x.ToString(null, Inv), 53, RoundingMode.ToNearestEven, Inv));

        // Rounded down and rounded up, they're neighbours, and the nearest is one of them. At 53
        // bits the step up from the smaller magnitude is 2^(its top bit - 52).
        ApFloat down = ApFloat.ParseUncapped(text, 53, RoundingMode.TowardNegative, Inv);
        ApFloat up = ApFloat.ParseUncapped(text, 53, RoundingMode.TowardPositive, Inv);
        Assert.True(down < up);
        Assert.True(x == down || x == up);
        ApFloat small = x.IsNegative ? up : down;
        ApFloat gap = ApFloat.Subtract(up, down, 53);
        Assert.Equal(1L, gap.Significand.BitLength);
        Assert.Equal(small.Exponent + small.Significand.BitLength - 53, gap.Exponent);
    }

    [Fact]
    public void TheDefaultParseKeepsItsCap()
    {
        Assert.Throws<OverflowException>(() => ApFloat.Parse("1e100001", 53, RoundingMode.ToNearestEven, Inv));
        Assert.Equal("1E+100001", ApFloat.ParseUncapped("1e100001", 53).ToString("R", Inv));
        // The uncapped parse still stops where the binary exponent would pass a long.
        Assert.Throws<OverflowException>(() => ApFloat.ParseUncapped("1e4000000000000000000", 53));
        Assert.Throws<OverflowException>(() => ApFloat.ParseUncapped("1e99999999999999999999", 53));
    }
}

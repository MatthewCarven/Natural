using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Natural.Jobs;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// ApFloat.StartFormat, ToString as a job. However it's stepped it must give what ToString
/// gives, character for character, keep to its budgets, and certify the digits it printed.
/// </summary>
public class FormatJobTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly int[] Precisions = [1, 2, 24, 53, 64, 113, 256, 1000];

    private static readonly string?[] Formats =
    [
        null, "E0", "E5", "e20", "F0", "F3", "F12", "G", "G7", "g3", "R",
        "B", "B6", "A", "a3", "X", "0.00", "#,##0.###", "'x = '0.0#", "'only text'",
    ];

    /// <summary>Formats whose printed digits come from the last scaling, so the certificate matches the test hooks.</summary>
    private static bool PrintsFromTheLastScaling(string? format) =>
        format is null || format[0] is 'E' or 'e' or 'F' or '0' or '#' or '\'' || (format[0] is 'G' or 'g' && format.Length > 1);

    /// <summary>
    /// Steps a job one unit at a time, checking every call, and returns its result. Progress must
    /// track the work: low after the first step (a scaling, with the digits still to come), and,
    /// when <paramref name="oneScaling"/> (the format scales once, so the plan is known up front),
    /// near a half when half the work is done.
    /// </summary>
    private static string StepByStep(Job<string, DecimalCertificate> job, bool oneScaling, out int calls)
    {
        calls = 0;
        double progress = 0;
        var seen = new List<(long Work, double Progress)>();
        while (true)
        {
            long steps = job.Steps;
            bool done = job.Continue(1);
            Assert.True(++calls < 100_000, "the job never finished");
            // A step is always at least one unit, so a budget of one takes exactly one step;
            // only the call that finishes takes none.
            Assert.Equal(done ? steps : steps + 1, job.Steps);
            Assert.True(job.Progress >= progress, $"Progress went from {progress} to {job.Progress}");
            if (done) break;
            progress = job.Progress;
            seen.Add((job.WorkDone, progress));
            Assert.True(progress <= 0.99);
            if (calls == 1) Assert.True(progress < 0.5, $"Progress {progress} after the first step, with the digits to come");
            Assert.Throws<InvalidOperationException>(() => job.Result);
        }
        Assert.True(progress >= 0.9, $"Progress was {progress} with all the work done");
        Assert.Equal(1, job.Progress);
        if (oneScaling)
        {
            double atHalf = seen.First(s => s.Work * 2 >= job.WorkDone).Progress;
            Assert.InRange(atHalf, 0.4, 0.7);
        }
        return job.Result;
    }

    /// <summary>A random non-zero value: a limb-biased significand of up to 40 decimal digits, at 10^±exponent.</summary>
    private static ApFloat RandomValue(Random rng, int p, int minExponent, int maxExponent)
    {
        BigInteger digits = Oracle.RandomBig(rng, 4, allowNegative: false) + 1;
        int exponent = rng.Next(minExponent, maxExponent + 1) * (rng.Next(2) == 0 ? 1 : -1);
        string text = $"{(rng.Next(2) == 0 ? "-" : "")}{digits}e{exponent}";
        return ApFloat.Parse(text, p, RoundingMode.ToNearestEven, Inv);
    }

    // ------------------------------------------------------------------
    // The same text as ToString
    // ------------------------------------------------------------------

    [Fact]
    public void AJobGivesWhatToStringGives()
    {
        var rng = new Random(600);
        var values = new List<ApFloat>
        {
            ApFloat.Zero, ApFloat.NegativeZero, ApFloat.NaN, ApFloat.PositiveInfinity, ApFloat.NegativeInfinity,
            ApFloat.Parse("1e5000", 53), ApFloat.Parse("-3.7e-6000", 113), ApFloat.Parse("9.999999e99999", 24),
        };
        for (int i = 0; i < 60; i++) values.Add(RandomValue(rng, Precisions[rng.Next(Precisions.Length)], 0, 400));
        for (int i = 0; i < 40; i++) values.Add(RandomFinite(rng, 3, 300).WithPrecision(Precisions[rng.Next(Precisions.Length)]));

        foreach (ApFloat x in values)
        foreach (string? format in Formats)
        {
            // F and custom patterns print every digit before the point: keep those to moderate sizes
            // (the pattern on 1e100000 alone took 2.3 s).
            bool everyDigit = format is "F0" or "F3" or "F12" or "0.00" or "#,##0.###" or "'x = '0.0#";
            if (everyDigit && x.IsFinite && !x.IsZero && Math.Abs(x.Exponent + x.Significand.BitLength) > 20_000) continue;
            RoundingMode mode = Modes[rng.Next(4)];
            string at = $"{Describe(x)} at {x.Precision} bits, \"{format}\", {mode}";

            var job = x.StartFormat(format, Inv, mode);
            string stepped = job.RunToEnd();
            Assert.Equal(x.ToString(format, Inv, mode), stepped);
            if (x.IsFinite && !x.IsZero && PrintsFromTheLastScaling(format) && format != "'only text'")
            {
                Assert.True(ApFloat.LastRounds == job.Certificate.Rounds && ApFloat.LastExact == job.Certificate.Exact,
                    $"{at}: certificate {job.Certificate.Rounds}/{job.Certificate.Exact}, hooks {ApFloat.LastRounds}/{ApFloat.LastExact}");
            }
        }
    }

    [Fact]
    public void SteppedOneUnitAtATimeTheTextIsTheSame()
    {
        var rng = new Random(601);
        var cases = new (ApFloat X, string Format)[]
        {
            (RandomValue(rng, 53, 0, 0) * ApFloat.Parse("1e3000", 53), "F0"),    // about 10,000 bits of double dabble
            (RandomValue(rng, 10_000, 0, 30), "E2500"),                          // 2,501 digits from a 10,000-bit value
            (RandomValue(rng, 3000, 0, 300), "R"),                               // many scalings and read-backs, then 905 digits
            (-ApFloat.Parse("3.3e2500", 4000), "#,##0.0###"),
        };
        foreach (var (x, format) in cases)
        {
            foreach (RoundingMode mode in new[] { RoundingMode.ToNearestEven, RoundingMode.TowardNegative })
            {
                string stepped = StepByStep(x.StartFormat(format, Inv, mode), oneScaling: format[0] != 'R', out int calls);
                Assert.True(calls > 20, $"\"{format}\": only {calls} calls");
                Assert.Equal(x.ToString(format, Inv, mode), stepped);
            }
        }
    }

    [Fact]
    public void BudgetsAreKeptToTheStep()
    {
        // Learn where the steps end by stepping one job a unit at a time, then run the same
        // format with random budgets: each call must stop at the first step end at or past it.
        var rng = new Random(602);
        ApFloat x = ApFloat.Parse("7.0710678118654752440e2999", 53);
        var ends = new List<long>();
        var learner = x.StartFormat("F2", Inv);
        while (!learner.Continue(1))
        {
            ends.Add(learner.WorkDone);
            Assert.True(ends.Count < 100_000, "the job never finished");
        }
        long total = learner.WorkDone;
        long biggest = ends.Zip(ends.Prepend(0), (end, start) => end - start).Max();

        var job = x.StartFormat("F2", Inv);
        for (int calls = 0; !job.IsComplete; calls++)
        {
            Assert.True(calls < 100_000, "the job never finished");
            long before = job.WorkDone;
            long budget = rng.Next(4) switch { 0 => 0, 1 => 1, 2 => rng.NextInt64(1, biggest + 1), _ => rng.NextInt64(1, 4 * biggest) };
            bool done = job.Continue(budget);
            long target = before + budget;
            int at = ends.FindIndex(end => end >= target);
            if (budget == 0) Assert.Equal(before, job.WorkDone);
            else if (at < 0) Assert.True(done && job.WorkDone == total, $"budget {budget} from {before}: should have finished");
            else Assert.True(!done && job.WorkDone == ends[at], $"budget {budget} from {before}: stopped at {job.WorkDone}, not {ends[at]}");
        }
        Assert.Equal(learner.Result, job.Result);
    }

    [Fact]
    public void ALongFormatAgainstBigInteger()
    {
        // F0 of 1e30000 at 53 bits: the value is m × 2^e exactly, an integer, so its digits are
        // BigInteger's. About 0.2 s of double dabble in Release, run in 10 ms slices.
        ApFloat x = ApFloat.Parse("1e30000", 53);
        var job = x.StartFormat("F0", Inv);
        int calls = 1;
        while (!job.Continue(TimeSpan.FromMilliseconds(10))) Assert.True(++calls < 100_000, "the job never finished");
        Assert.True(calls > 10, $"only {calls} calls");
        Assert.Equal((Oracle.ToBig(x.Significand) << (int)x.Exponent).ToString(Inv), job.Result);
        Assert.Equal(new DecimalCertificate(0, 0, true, null, null, 0), job.Certificate);   // t = 0: the exact route
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(1000)]
    [InlineData(20_000)]
    public void DoubleDabbleCountsItsLimbSteps(int bits)
    {
        // A bit costs a limb step per BCD word in use, for each of the two passes, plus one; a
        // step ends on the first bit that brings it to DigitStepUnits or more; reading the
        // digits out costs one per word. Worked out again here from BigInteger's digit counts:
        // the words in use are the value so far's digits over 8, at least one.
        var rng = new Random(603 + bits);
        BigInteger value = Oracle.RandomBig(rng, (bits + 31) / 32, allowNegative: false) & ((BigInteger.One << bits) - 1);
        value |= BigInteger.One << (bits - 1);
        var steps = new List<long>();
        var result = new StrongBox<string>();
        foreach (long units in ApInt.DecimalStringSteps(Oracle.ToAp(value).Limbs, result)) steps.Add(units);

        var expected = new List<long>();
        BigInteger soFar = BigInteger.Zero, nextPowerOfTen = BigInteger.One;
        long used = 1, pending = 0, digits = 0;
        for (int i = bits - 1; i >= 0; i--)
        {
            pending += 2 * used + 1;
            soFar = (soFar << 1) | ((value >> i) & 1);
            while (soFar >= nextPowerOfTen)
            {
                digits++;
                nextPowerOfTen *= 10;
            }
            used = Math.Max(1, (digits + 7) / 8);
            if (pending >= ApInt.DigitStepUnits)
            {
                expected.Add(pending);
                pending = 0;
            }
        }
        expected.Add(pending + used);

        Assert.Equal(expected, steps);
        Assert.Equal(value.ToString(Inv), result.Value);
        long total = steps.Sum();
        if (bits >= 10_000)
            Assert.InRange(ApInt.DecimalStringUnits(bits), total * 98 / 100, total * 102 / 100);   // the estimate behind Progress
    }

    // ------------------------------------------------------------------
    // The certificate
    // ------------------------------------------------------------------

    [Fact]
    public void TheCertificateBracketsTheValueScaled()
    {
        // E formats past the cache scale by 10^-(about the exponent), through the engine.
        var rng = new Random(604);
        foreach (string format in new[] { "E6", "E0", "G12", "R" })
        {
            for (int i = 0; i < 20; i++)
            {
                int p = Precisions[rng.Next(Precisions.Length)];
                ApFloat x = RandomValue(rng, p, 5000, 12_000);   // decimal exponents past the cache
                RoundingMode mode = Modes[rng.Next(4)];
                string at = $"{Describe(x)} at {p} bits, {format}, {mode}";

                var job = x.StartFormat(format, Inv, mode);
                job.RunToEnd();
                DecimalCertificate c = job.Certificate;
                Assert.True(c.Rounds >= 1 && c.Low is not null && c.High is not null, $"{at}: {c}");

                // Low ≤ x × 10^Scale ≤ High, in exact rationals.
                var (num, den) = ToRational(x);
                if (c.Scale >= 0) num *= BigInteger.Pow(10, (int)c.Scale);
                else den *= BigInteger.Pow(10, (int)-c.Scale);
                var (lowNum, lowDen) = ToRational(c.Low!.Value);
                var (highNum, highDen) = ToRational(c.High!.Value);
                Assert.True(lowNum * den <= num * lowDen, at + ": Low is above the scaled value");
                Assert.True(num * highDen <= highNum * den, at + ": High is below the scaled value");

                // And for E, which keeps every digit, the scale is the one the printed digits came
                // from: |x| × 10^Scale has as many digits before the point as were printed (one
                // fewer if 9.99... rounded up to 10.0...).
                if (format[0] == 'E')
                {
                    int printed = job.Result.Split('E')[0].Count(char.IsAsciiDigit);
                    BigInteger whole = BigInteger.Abs(num / den);
                    Assert.InRange(whole.ToString(Inv).Length, printed - 1, printed);
                }
            }
        }
    }

    [Theory]
    [InlineData("F4", 4)]
    [InlineData("0.00", 2)]
    [InlineData("F0", 0)]
    public void BelowTheCacheTheCertificateSaysExact(string format, long scale)
    {
        var job = ApFloat.Parse("-123.456789", 53).StartFormat(format, Inv);
        job.RunToEnd();
        Assert.Equal(new DecimalCertificate(0, 0, true, null, null, scale), job.Certificate);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("A")]
    [InlineData("x4")]
    [InlineData("'just this'")]
    public void FormatsWithoutDecimalDigitsFinishAtOnce(string format)
    {
        var job = ApFloat.Parse("-5.25", 53).StartFormat(format, Inv);
        Assert.True(job.Continue(1));
        Assert.Equal(0, job.WorkDone);
        Assert.Equal(ApFloat.Parse("-5.25", 53).ToString(format, Inv), job.Result);
        Assert.Equal(new DecimalCertificate(0, 0, true, null, null), job.Certificate);
    }

    [Fact]
    public void SpecialValuesFinishAtOnce()
    {
        foreach (ApFloat x in new[] { ApFloat.NaN, ApFloat.NegativeInfinity, ApFloat.NegativeZero })
        {
            var job = x.StartFormat("E3", Inv);
            Assert.True(job.Continue(1));
            Assert.Equal(x.ToString("E3", Inv), job.Result);
            Assert.Equal(new DecimalCertificate(0, 0, true, null, null), job.Certificate);
        }
        Assert.Equal("NaN", ApFloat.NaN.StartFormat("not a format", Inv).RunToEnd());   // as ToString: NaN first
    }

    // ------------------------------------------------------------------
    // Threads, hooks, errors
    // ------------------------------------------------------------------

    [Fact]
    public void AJobLeavesTheTestHooksAlone()
    {
        // Each job takes the other route from the ToString before it.
        ApFloat big = ApFloat.Parse("1e5000", 53), small = ApFloat.Parse("1.5", 53);
        _ = big.ToString("E3");                     // intervals
        small.StartFormat("E3").RunToEnd();         // the exact route, as a job
        Assert.True(ApFloat.LastRounds >= 1);
        Assert.False(ApFloat.LastExact);

        _ = small.ToString("E3");
        big.StartFormat("E3").RunToEnd();
        Assert.Equal(0, ApFloat.LastRounds);
        Assert.True(ApFloat.LastExact);
    }

    [Fact]
    public void TheRouteIsTheOneForcedWhenTheJobStarted()
    {
        try
        {
            ApFloat.ForcedRoute = ApFloat.Route.Interval;
            var job = ApFloat.Parse("1.5", 53).StartFormat("F3", Inv);
            ApFloat.ForcedRoute = ApFloat.Route.Auto;
            Assert.Equal("1.500", job.RunToEnd());
            Assert.True(job.Certificate.Rounds >= 1, "F3 forced through intervals");
        }
        finally
        {
            ApFloat.ForcedRoute = ApFloat.Route.Auto;
        }
    }

    [Fact]
    public async Task AJobCanBeContinuedFromOtherThreads()
    {
        ApFloat x = ApFloat.Parse("-2.5e4000", 53);
        var job = x.StartFormat("F1", Inv, RoundingMode.TowardZero);
        int calls = 0;
        while (!await Task.Run(() => job.Continue(50_000))) Assert.True(++calls < 100_000, "the job never finished");
        Assert.True(calls > 10, $"only {calls} calls");
        Assert.Equal(x.ToString("F1", Inv, RoundingMode.TowardZero), job.Result);
    }

    [Theory]
    [InlineData("Q")]
    [InlineData("Z5")]
    [InlineData("0.0.0")]
    [InlineData("0;(0)")]
    [InlineData("'unclosed")]
    public void BadFormatsThrowAtTheStart(string format) =>
        Assert.Throws<FormatException>(() => ApFloat.Parse("1.5", 53).StartFormat(format, Inv));

    [Fact]
    public void OutputTooLongForAStringThrowsAtTheStart()
    {
        ApFloat x = ApFloat.Parse("1e1000000000000000000", 53);
        Assert.Throws<OverflowException>(() => x.StartFormat("F0", Inv));
        Assert.Throws<OverflowException>(() => x.StartFormat("#,##0", Inv));
        Assert.Throws<OverflowException>(() => x.ToString("F2", Inv));
        Assert.Equal("1.000E+1000000000000000000", x.StartFormat("E3", Inv).RunToEnd());   // E is fine
    }
}

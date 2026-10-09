using System.Globalization;
using System.Numerics;
using Natural.Jobs;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// ApFloat.StartParse, a parse as a job. However it's stepped it must give what Parse gives,
/// bit for bit, keep to its budgets, and its certificate must bracket the exact value.
/// </summary>
public class ParseJobTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly int[] Precisions = [1, 2, 24, 53, 64, 113, 256, 1000];

    /// <summary>
    /// A decimal whose digits are a limb-biased integer of exactly <paramref name="limbs"/>
    /// limbs (runs of all-ones and all-zero limbs, where the digit loop's carries live), with
    /// the point anywhere and the last digit worth 10^±scale, scale from
    /// <paramref name="minScale"/> to <paramref name="maxScale"/>. Its exact value is num / den.
    /// </summary>
    private static (string Text, BigInteger Num, BigInteger Den) RandomDecimal(Random rng, int limbs, int minScale, int maxScale)
    {
        BigInteger m = Oracle.RandomBig(rng, limbs, allowNegative: false) | (BigInteger.One << (32 * limbs - 1));
        string digits = m.ToString(Inv);
        int point = rng.Next(digits.Length + 1);
        int scale = rng.Next(minScale, maxScale + 1) * (rng.Next(2) == 0 ? 1 : -1);
        long exponent = scale + (digits.Length - point);
        bool negative = rng.Next(2) == 0;
        string text = $"{(negative ? "-" : "")}{digits[..point]}.{digits[point..]}e{exponent}";
        BigInteger num = negative ? -m : m, den = BigInteger.One;
        if (scale >= 0) num *= BigInteger.Pow(10, scale);
        else den = BigInteger.Pow(10, -scale);
        return (text, num, den);
    }

    /// <summary>A long decimal, n random digits after a 1, and its exact value.</summary>
    private static (string Text, BigInteger Num, BigInteger Den) LongDecimal(Random rng, int n, int scale)
    {
        var digits = new char[n];
        digits[0] = '1';
        for (int i = 1; i < n; i++) digits[i] = (char)('0' + rng.Next(10));
        BigInteger m = BigInteger.Parse(digits, NumberStyles.None, Inv);
        return scale >= 0
            ? ($"{new string(digits)}e{scale}", m * BigInteger.Pow(10, scale), BigInteger.One)
            : ($"{new string(digits)}e{scale}", m, BigInteger.Pow(10, -scale));
    }

    /// <summary>Steps a job one unit at a time, checking every call, and returns its result.</summary>
    private static ApFloat StepByStep(Job<ApFloat, DecimalCertificate> job, out int calls)
    {
        calls = 0;
        double progress = 0;
        while (true)
        {
            long steps = job.Steps;
            bool done = job.Continue(1);
            Assert.True(++calls < 100_000, "the job never finished");
            // A step is always at least one unit, so a budget of one takes exactly one step;
            // only the call that finishes takes none (the work's tail runs then).
            Assert.Equal(done ? steps : steps + 1, job.Steps);
            Assert.True(job.Progress >= progress, $"Progress went from {progress} to {job.Progress}");
            if (done) break;
            progress = job.Progress;
            Assert.True(progress <= 0.99);
            Assert.Throws<InvalidOperationException>(() => job.Result);
            Assert.Throws<InvalidOperationException>(() => job.Certificate);
        }
        // With all the work done the estimate should be close: progress near the end, not stuck low.
        Assert.True(progress >= 0.9, $"Progress was {progress} with all the work done");
        Assert.Equal(1, job.Progress);
        return job.Result;
    }

    [Theory]
    [InlineData("7")]
    [InlineData("000_123_456_789")]
    [InlineData(null)]
    public void TheDigitLoopCountsItsLimbSteps(string? text)
    {
        // A digit costs a step per limb of what's been read so far, plus two, and a step ends
        // on the first digit that brings it to DigitStepUnits or more. Worked out again here
        // with BigInteger's limb counts.
        var rng = new Random(506);
        text ??= "0000" + string.Concat(Enumerable.Range(0, 6000).Select(i => i % 97 == 50 ? "_9" : ((char)('0' + rng.Next(10))).ToString()));
        var steps = new List<long>();
        var result = new System.Runtime.CompilerServices.StrongBox<uint[]>();
        foreach (long units in ApInt.DecimalDigitSteps(text, result)) steps.Add(units);

        BigInteger value = BigInteger.Zero;
        long pending = 0, digits = 0;
        var expected = new List<long>();
        foreach (char c in text)
        {
            if (c == '_') continue;
            digits++;
            pending += (long)(value.GetBitLength() + 31) / 32 + 2;
            value = value * 10 + (c - '0');
            if (pending >= ApInt.DigitStepUnits)
            {
                expected.Add(pending);
                pending = 0;
            }
        }
        if (pending > 0) expected.Add(pending);

        Assert.Equal(expected, steps);
        Assert.Equal(value, Oracle.ToBig(ApInt.FromLimbs(result.Value!, false)));
        long total = steps.Sum();
        if (digits >= 1000)
            Assert.InRange(ApInt.DecimalDigitUnits(digits), total * 98 / 100, total * 102 / 100);   // the estimate behind Progress
    }

    // ------------------------------------------------------------------
    // The same answer as Parse
    // ------------------------------------------------------------------

    [Fact]
    public void AJobGivesWhatParseGives()
    {
        var rng = new Random(500);
        for (int i = 0; i < 300; i++)
        {
            // Below the cache and past it: scales to 6,000 either way.
            var (text, _, _) = RandomDecimal(rng, rng.Next(1, 40), 0, 6000);
            int p = Precisions[rng.Next(Precisions.Length)];
            RoundingMode mode = Modes[rng.Next(4)];
            string at = $"\"{text}\" at {p} bits, {mode}";

            var job = ApFloat.StartParse(text, p, mode, Inv);
            ApFloat stepped = job.RunToEnd();
            ApFloat parsed = ApFloat.Parse(text, p, mode, Inv);
            AssertSame(parsed, stepped, at);
            Assert.Equal(parsed.Precision, stepped.Precision);

            // The certificate says what the test hooks say Parse did.
            Assert.Equal(ApFloat.LastRounds, job.Certificate.Rounds);
            Assert.Equal(ApFloat.LastExact, job.Certificate.Exact);
        }
    }

    [Fact]
    public void SteppedOneUnitAtATimeTheAnswerIsTheSame()
    {
        var rng = new Random(501);
        for (int i = 0; i < 8; i++)
        {
            // 300 to 700 limbs: about 3,000 to 7,000 digits, a few hundred steps each.
            var (text, num, den) = RandomDecimal(rng, rng.Next(300, 700), 4000, 9000);
            int p = Precisions[rng.Next(Precisions.Length)];
            RoundingMode mode = Modes[rng.Next(4)];
            string at = $"{text.Length}-character decimal at {p} bits, {mode}";

            ApFloat stepped = StepByStep(ApFloat.StartParse(text, p, mode, Inv), out int calls);
            Assert.True(calls > 100, $"{at}: only {calls} calls");
            AssertSame(ApFloat.Parse(text, p, mode, Inv), stepped, at);
            AssertRounded(Round(num, den, p, mode), stepped, p, at);
        }
    }

    [Fact]
    public void BudgetsAreKeptToTheStep()
    {
        // Step one job a unit at a time to learn where its steps end. Then run the same text
        // with random budgets: each call must stop at the first step end at or past its budget.
        var rng = new Random(502);
        for (int i = 0; i < 4; i++)
        {
            var (text, _, _) = RandomDecimal(rng, rng.Next(200, 500), 4096, 8000);
            var ends = new List<long>();
            var learner = ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv);
            while (!learner.Continue(1))
            {
                ends.Add(learner.WorkDone);
                Assert.True(ends.Count < 100_000, "the job never finished");
            }
            long total = learner.WorkDone;
            long biggest = ends.Zip(ends.Prepend(0), (end, start) => end - start).Max();

            var job = ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv);
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
            AssertSame(learner.Result, job.Result);
        }
    }

    [Fact]
    public void ALongDecimalAgainstTheOracle()
    {
        // 30,000 digits: about 0.75 s of digit loop in Release, run in 10 ms slices.
        var (text, num, den) = LongDecimal(new Random(503), 30_000, -35_000);
        var job = ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv);
        int calls = 1;
        while (!job.Continue(TimeSpan.FromMilliseconds(10))) Assert.True(++calls < 100_000, "the job never finished");
        Assert.True(calls > 10, $"only {calls} calls");
        AssertRounded(Round(num, den, 53, RoundingMode.ToNearestEven), job.Result, 53, "30,000 digits");
        Assert.False(job.Certificate.Exact);
    }

    // ------------------------------------------------------------------
    // The certificate
    // ------------------------------------------------------------------

    [Fact]
    public void TheCertificateBracketsTheExactValue()
    {
        var rng = new Random(504);
        int certified = 0;
        for (int i = 0; i < 150; i++)
        {
            var (text, num, den) = RandomDecimal(rng, rng.Next(1, 30), 4096, 20_000);   // past the cache: intervals
            int p = Precisions[rng.Next(Precisions.Length)];
            RoundingMode mode = Modes[rng.Next(4)];
            string at = $"\"{text[..Math.Min(40, text.Length)]}...\" at {p} bits, {mode}";

            var job = ApFloat.StartParse(text, p, mode, Inv);
            ApFloat result = job.RunToEnd();
            DecimalCertificate c = job.Certificate;
            Assert.True(c.Rounds >= 1, at);
            Assert.True(c.WorkingBits >= p + 64, at);
            ApFloat low = c.Low!.Value, high = c.High!.Value;
            Assert.Equal(c.WorkingBits, low.Precision);
            var (lowNum, lowDen) = ToRational(low);
            var (highNum, highDen) = ToRational(high);
            Assert.True(lowNum * den <= num * lowDen, at + ": Low is above the exact value");
            Assert.True(num * highDen <= highNum * den, at + ": High is below the exact value");
            if (!c.Exact)
            {
                // Both ends round to the result: that's what certified it.
                AssertSame(result, low.WithPrecision(p, mode), at + ": Low");
                AssertSame(result, high.WithPrecision(p, mode), at + ": High");
                certified++;
            }
        }
        Assert.True(certified > 140, $"only {certified} of 150 certified by intervals");
    }

    [Fact]
    public void ATieIsDecidedByTheExactRoute()
    {
        // 1 + 2^-53 is a tie at 53 bits: 1 + 5^53 / 10^53 exactly. With 5,000 zeros on the end
        // its decimal exponent is past the cache, and the interval ends straddle it at every width.
        string fraction = BigInteger.Pow(5, 53).ToString(Inv).PadLeft(53, '0');
        string tie = "1" + fraction + new string('0', 5000) + "e-5053";
        var job = ApFloat.StartParse(tie, 53, RoundingMode.ToNearestEven, Inv);
        AssertSame(ApFloat.Parse("1", 53, RoundingMode.ToNearestEven, Inv), job.RunToEnd(), "the tie goes to even");
        DecimalCertificate c = job.Certificate;
        Assert.True(c.Exact);
        Assert.True(c.Rounds >= 1);
        Assert.NotNull(c.Low);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(53)]
    [InlineData(113)]
    public void TheCertificateIsTheLastRounds(int p)
    {
        // A decimal a hair either side of a rounding boundary takes two or more rounds. The
        // certificate's ends must both come from the last one: an earlier round's ends still
        // bracket the value, but one of them rounds the other way, which is why it went on.
        var rng = new Random(507 + p);
        foreach (RoundingMode mode in Modes)
        {
            // The boundary (odd) × 2^-14000 written out exactly, then cut to 100 digits.
            int bits = mode == RoundingMode.ToNearestEven ? p + 1 : p;
            BigInteger low = BigInteger.Abs(Oracle.RandomBig(rng, 4)) & ((BigInteger.One << (bits - 1)) - 1);
            BigInteger odd = (BigInteger.One << (bits - 1)) | low | 1;
            string all = (odd * BigInteger.Pow(5, 14_000)).ToString(Inv);
            BigInteger below = BigInteger.Parse(all[..100], Inv);
            int scale = -14_000 + (all.Length - 100);
            foreach (BigInteger q in new[] { below, below + 1 })
            {
                string text = $"{q}e{scale}";
                var job = ApFloat.StartParse(text, p, mode, Inv);
                // The digits are one step and the first round another. The next round is about
                // four times the first, and progress should say so rather than near-done.
                Assert.False(job.Continue(1) || job.Continue(1));
                Assert.True(job.Progress < 0.5, $"{mode}: progress {job.Progress} with a bigger round to come");
                ApFloat result = job.RunToEnd();
                DecimalCertificate c = job.Certificate;
                Assert.True(c.Rounds >= 2 && !c.Exact, $"{text} at {p}, {mode}: {c.Rounds} round(s), exact {c.Exact}");
                AssertSame(result, c.Low!.Value.WithPrecision(p, mode), $"{mode}: Low");
                AssertSame(result, c.High!.Value.WithPrecision(p, mode), $"{mode}: High");
                Assert.Equal(c.WorkingBits, c.Low.Value.Precision);
                Assert.Equal(c.WorkingBits, c.High.Value.Precision);
            }
        }
    }

    [Theory]
    [InlineData("1.5")]                       // below the cache: the exact route from the start
    [InlineData("-123456789e-4095")]
    [InlineData("0.000e-99999")]              // zero: nothing to pin
    [InlineData("-0")]
    public void WithoutRoundsTheCertificateSaysExact(string text)
    {
        var job = ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv);
        AssertSame(ApFloat.Parse(text, 53, RoundingMode.ToNearestEven, Inv), job.RunToEnd(), text);
        Assert.Equal(new DecimalCertificate(0, 0, true, null, null), job.Certificate);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("-inf")]
    [InlineData("∞")]
    [InlineData("0x1.8p+1")]
    [InlineData("-0b101.01p-3")]
    public void TextWithNoDecimalArithmeticFinishesAtOnce(string text)
    {
        var job = ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv);
        Assert.True(job.Continue(1));
        Assert.Equal(0, job.WorkDone);
        Assert.Equal(0, job.Steps);
        AssertSame(ApFloat.Parse(text, 53, RoundingMode.ToNearestEven, Inv), job.Result, text);
        Assert.Equal(new DecimalCertificate(0, 0, true, null, null), job.Certificate);
    }

    // ------------------------------------------------------------------
    // Threads and the test hooks
    // ------------------------------------------------------------------

    [Fact]
    public void AJobLeavesTheTestHooksAlone()
    {
        // Each job takes the other route from the Parse before it, so a job that wrote the
        // hooks would show.
        ApFloat.Parse("1e5000", 53);                   // one interval round
        ApFloat.StartParse("1.5", 53).RunToEnd();      // the exact route, as a job
        Assert.Equal(1, ApFloat.LastRounds);
        Assert.False(ApFloat.LastExact);

        ApFloat.Parse("1.5", 53);
        ApFloat.StartParse("1e-6000", 53).RunToEnd();  // one round, as a job
        Assert.Equal(0, ApFloat.LastRounds);
        Assert.True(ApFloat.LastExact);
    }

    [Fact]
    public void TheRouteIsTheOneForcedWhenTheJobStarted()
    {
        try
        {
            ApFloat.ForcedRoute = ApFloat.Route.Interval;
            var intervals = ApFloat.StartParse("1.5e10", 53);
            ApFloat.ForcedRoute = ApFloat.Route.Exact;
            var exact = ApFloat.StartParse("1e5000", 53);
            ApFloat.ForcedRoute = ApFloat.Route.Auto;
            intervals.RunToEnd();
            exact.RunToEnd();
            Assert.True(intervals.Certificate.Rounds >= 1, "1.5e10 forced through intervals");
            Assert.Equal(0, exact.Certificate.Rounds);
            AssertSame(ApFloat.Parse("1e5000", 53), exact.Result);
        }
        finally
        {
            ApFloat.ForcedRoute = ApFloat.Route.Auto;
        }
    }

    [Fact]
    public async Task AJobCanBeContinuedFromOtherThreads()
    {
        var (text, num, den) = LongDecimal(new Random(505), 4000, -5000);
        var job = ApFloat.StartParse(text, 113, RoundingMode.TowardPositive, Inv);
        int calls = 0;
        while (!await Task.Run(() => job.Continue(20_000))) Assert.True(++calls < 100_000, "the job never finished");
        Assert.True(calls > 10, $"only {calls} calls");
        AssertRounded(Round(num, den, 113, RoundingMode.TowardPositive), job.Result, 113, "continued from the thread pool");
    }

    // ------------------------------------------------------------------
    // Errors
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1e")]
    [InlineData("1.2.3")]
    [InlineData("--1")]
    [InlineData("1__0")]
    [InlineData("_1")]
    [InlineData(".")]
    public void BadTextThrowsAtTheStart(string text) =>
        Assert.Throws<FormatException>(() => ApFloat.StartParse(text, 53, RoundingMode.ToNearestEven, Inv));

    [Fact]
    public void OtherBadArgumentsThrowAtTheStart()
    {
        Assert.Throws<ArgumentNullException>(() => ApFloat.StartParse(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApFloat.StartParse("1", 0));
        Assert.Throws<OverflowException>(() => ApFloat.StartParse("1e99999999999999999999"));   // not a long
    }

    [Fact]
    public void AValueTooLargeThrowsFromContinueAsParseWould()
    {
        // The exponent fits a long, but 10^(3 × 10^18)'s binary exponent doesn't.
        const string text = "1e3000000000000000000";
        string message = Assert.Throws<OverflowException>(() => ApFloat.Parse(text, 53)).Message;
        var job = ApFloat.StartParse(text, 53);
        Assert.Equal(message, Assert.Throws<OverflowException>(() => job.Continue(long.MaxValue)).Message);
        Assert.Equal(message, Assert.Throws<OverflowException>(() => job.Continue(1)).Message);
        Assert.Equal(message, Assert.Throws<OverflowException>(() => job.Result).Message);
    }
}

using System.Globalization;
using System.Runtime.CompilerServices;
using Natural.Jobs;

namespace Natural;

// Long text as a job (Natural.Jobs): the same work as Parse, a slice at a time. Parse itself
// runs these same steps to the end, so the two can't disagree.
public readonly partial struct ApFloat
{
    /// <summary>
    /// <see cref="Parse(string, int, RoundingMode, IFormatProvider?)"/> as a job: the same work and
    /// the same result, bit for bit, done a slice at a time. It's for long text, since a parse
    /// costs the square of the number of digits: 30,000 take most of a second, and a million
    /// would take about a quarter of an hour. Short text finishes in one or two steps.
    ///
    /// The syntax is checked here, straight away; the arithmetic waits for Continue. The work is
    /// counted in limb steps: the digit loop counts them as it goes, and a round of the interval
    /// engine (or the exact route) counts an estimate from its sizes, so the count is the same
    /// on any machine. A step is 4,096 units of the digit loop or a little more (it ends on a
    /// digit), or one whole round; Continue may go over its budget by one step. The
    /// certificate says how the result was pinned.
    /// </summary>
    /// <exception cref="FormatException">It isn't a number.</exception>
    /// <exception cref="OverflowException">An exponent past a long. One whose value's binary
    /// exponent wouldn't fit a long throws from Continue instead, as it would from Parse.</exception>
    public static Job<ApFloat, DecimalCertificate> StartParse(string s, int precision = DefaultPrecision,
                                                              RoundingMode mode = RoundingMode.ToNearestEven,
                                                              IFormatProvider? provider = null) =>
        StartParse(s ?? throw new ArgumentNullException(nameof(s)), Target.Of(precision), mode, provider);

    /// <summary>
    /// <see cref="Parse(string, IeeeFormat, RoundingMode, IFormatProvider?)"/> as a job: rounded
    /// once into the format, the same result bit for bit, a slice at a time. Text a format
    /// settles from the digits alone (zero, or far out of range) needs no steps.
    /// </summary>
    /// <exception cref="FormatException">It isn't a number.</exception>
    public static Job<ApFloat, DecimalCertificate> StartParse(string s, IeeeFormat format,
                                                              RoundingMode mode = RoundingMode.ToNearestEven,
                                                              IFormatProvider? provider = null) =>
        StartParse(s ?? throw new ArgumentNullException(nameof(s)), Target.Of(format), mode, provider);

    private static Job<ApFloat, DecimalCertificate> StartParse(string s, Target target, RoundingMode mode, IFormatProvider? provider)
    {
        IeeeFlags ignored = IeeeFlags.None;
        ParseOutcome outcome = PrepareParse(s, target, mode, provider, out ApFloat result, out DecimalParse? work, ref ignored);
        if (outcome == ParseOutcome.TooLarge) throw TooLarge(s);
        if (outcome != ParseOutcome.Parsed) throw NotANumber(s);
        return work is null
            ? Job.Start<ApFloat, DecimalCertificate>(context => Already(context, result))
            : Job.Start<ApFloat, DecimalCertificate>(context => ParseSteps(context, work, s));
    }

    /// <summary>
    /// <see cref="ToString(string?, IFormatProvider?, RoundingMode)"/> as a job: the same text,
    /// done a slice at a time. It's for long output, since printing costs the square of the
    /// number of digits: F0 of 1e30000 takes a fifth of a second, of 1e100000 2.5 s, and of
    /// 1e200000 10 s. E, G and R on a huge exponent take milliseconds, and so do short formats.
    ///
    /// The format is checked here, straight away, and so is the length: F (or a custom pattern)
    /// on a value with more digits before the point than a string can hold throws at once. The
    /// work is counted in limb steps, as in <see cref="StartParse"/>: double dabble counts them
    /// as it goes (4,096 a step or a little more, ending on a bit), and a scaling's interval
    /// round or exact route counts an estimate. The certificate says how the printed digits were
    /// pinned (see <see cref="DecimalCertificate.Scale"/>); B, A and X print no decimal digits.
    /// </summary>
    /// <exception cref="FormatException">An unknown format or a malformed custom pattern.</exception>
    /// <exception cref="OverflowException">More digits than a string can hold.</exception>
    public Job<string, DecimalCertificate> StartFormat(string? format = null, IFormatProvider? provider = null,
                                                       RoundingMode mode = RoundingMode.ToNearestEven)
    {
        var work = new DecimalFormat(this, format, NumberFormatInfo.GetInstance(provider), mode, ForcedRoute);
        return Job.Start<string, DecimalCertificate>(context => FormatSteps(context, work));
    }

    private static IEnumerable<long> FormatSteps(JobContext<string, DecimalCertificate> context, DecimalFormat work)
    {
        context.ExpectedWork = work.ExpectedWork;
        foreach (long units in work.Steps())
        {
            context.ExpectedWork = work.ExpectedWork;
            yield return units;
        }
        context.Result = work.Text!;
        context.Certificate = work.Certificate();
    }

    /// <summary>A job for text that needed no decimal arithmetic: NaN, infinity, hex, binary.</summary>
    private static IEnumerable<long> Already(JobContext<ApFloat, DecimalCertificate> context, ApFloat result)
    {
        context.Result = result;
        context.Certificate = new DecimalCertificate(0, 0, true, null, null);
        yield break;
    }

    private static IEnumerable<long> ParseSteps(JobContext<ApFloat, DecimalCertificate> context, DecimalParse work, string s)
    {
        using IEnumerator<long> steps = work.Steps().GetEnumerator();
        context.ExpectedWork = work.ExpectedWork;
        while (true)
        {
            bool more;
            try
            {
                more = steps.MoveNext();
            }
            catch (OverflowException e)
            {
                throw TooLarge(s, e);   // the message Parse gives
            }
            if (!more) break;
            context.ExpectedWork = work.ExpectedWork;
            yield return steps.Current;
        }
        context.Result = work.Result;
        context.Certificate = work.Certificate();
    }

    /// <summary>
    /// The arithmetic of one decimal parse, as steps: the digit loop, then digits × 10^scale
    /// pinned by the interval engine (or the exact route), then rounded once. Parse runs it to
    /// the end; a job runs it a slice at a time.
    /// </summary>
    private sealed class DecimalParse
    {
        private readonly bool _negative;
        private readonly string _digits;
        private readonly long _scale;
        private readonly Target _target;
        private readonly RoundingMode _mode;
        private readonly Route _route;
        private readonly Pinning _pinning = new();
        private bool _pinned;   // whether the pinning steps have started

        /// <param name="digits">Decimal digits and underscores, at least one digit.</param>
        /// <param name="route">The test hook, read by the caller: the steps may run on another thread.</param>
        public DecimalParse(bool negative, string digits, long scale, Target target, RoundingMode mode, Route route)
        {
            (_negative, _digits, _scale, _target, _mode, _route) = (negative, digits, scale, target, mode, route);
            long count = digits.Length - digits.AsSpan().Count('_');
            long bitsAtMost = count * 3322 / 1000 + 1;   // log2(10) is 3.3219...
            ExpectedWork = ApInt.DecimalDigitUnits(count) + FirstStepUnits(bitsAtMost, scale, (long)target.Precision + GuardBits, route);
        }

        public ApFloat Result { get; private set; }

        /// <summary>The flags rounding the text raised: Inexact, and in a format Overflow or Underflow.</summary>
        public IeeeFlags Flags { get; private set; }

        public long WorkDone { get; private set; }

        /// <summary>An estimate of the whole: what's done, plus what the next stage looks like it will take.</summary>
        public long ExpectedWork { get; private set; }

        public IEnumerable<long> Steps()
        {
            var q = new StrongBox<uint[]>();
            foreach (long units in ApInt.DecimalDigitSteps(_digits, q))
            {
                WorkDone += units;
                yield return units;
            }

            uint[] digits = q.Value!;
            if (digits.Length == 0)
            {
                Result = ZeroOf(_negative, _target.Precision);
                ExpectedWork = WorkDone;
                yield break;
            }

            _pinned = true;
            ExpectedWork = WorkDone + FirstStepUnits(Magnitude.BitLength(digits), _scale, (long)_target.Precision + GuardBits, _route);
            foreach (long units in FromDecimalSteps(_negative, digits, _scale, _target, _mode, _route, _pinning))
            {
                WorkDone += units;
                ExpectedWork = WorkDone + _pinning.NextUnits;
                yield return units;
            }
            Result = _pinning.Result;
            Flags = _pinning.Exact ? _pinning.Flags : SettledFlags(Result, digits, _scale, _pinning.Range);
        }

        /// <summary>All the steps, on this thread, leaving the rounds and the route in the test hooks as Parse always has.</summary>
        public ApFloat RunToEnd()
        {
            try
            {
                foreach (long _ in Steps()) { }
            }
            finally
            {
                if (_pinned)
                {
                    LastRounds = _pinning.Rounds;
                    LastExact = _pinning.Exact;
                }
            }
            return Result;
        }

        public DecimalCertificate Certificate()
        {
            ApFloat? low = _pinning.Low, high = _pinning.High;
            if (_negative && low is { } l && high is { } h) (low, high) = (-h, -l);
            return new DecimalCertificate(_pinning.Rounds, _pinning.WorkingBits, !_pinned || _pinning.Exact, low, high);
        }
    }
}

/// <summary>
/// How a decimal conversion was pinned: by the certified interval engine, which brackets the
/// exact value and accepts when both ends round the same way, or by the exact route, which
/// builds the exact rational and rounds it once. For a parse, the exact value is the text's;
/// for a format, it's the value × 10^Scale, the integer whose digits were printed.
/// </summary>
/// <param name="Rounds">Interval rounds taken. 0 when the exact route ran from the start: always
/// below 10^±4096, and when there was no decimal arithmetic (zero, NaN, infinity, hex, binary).</param>
/// <param name="WorkingBits">The last round's working precision, W; 0 with no rounds.</param>
/// <param name="Exact">True when the exact route decided the result. It does for a tie, and for an
/// exact value in a directed mode, because the interval ends straddle the boundary they sit on.</param>
/// <param name="Low">The last round's lower end: Low ≤ the exact value ≤ High. Null with no rounds.</param>
/// <param name="High">The last round's upper end. When the rounds settled it, Low and High round to
/// the same result, which is what certifies it.</param>
/// <param name="Scale">For a format, the power of ten the value was scaled by to make the printed
/// digits: 4 for "F4" (so Low ≤ value × 10^4 ≤ High). 0 for a parse.</param>
public sealed record DecimalCertificate(int Rounds, long WorkingBits, bool Exact, ApFloat? Low, ApFloat? High, long Scale = 0);

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
                                                              IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        ParseOutcome outcome = PrepareParse(s, precision, mode, provider, out ApFloat result, out DecimalParse? work);
        if (outcome == ParseOutcome.TooLarge) throw TooLarge(s);
        if (outcome != ParseOutcome.Parsed) throw NotANumber(s);
        return work is null
            ? Job.Start<ApFloat, DecimalCertificate>(context => Already(context, result))
            : Job.Start<ApFloat, DecimalCertificate>(context => ParseSteps(context, work, s));
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
        private readonly int _precision;
        private readonly RoundingMode _mode;
        private readonly Route _route;
        private readonly Pinning _pinning = new();
        private bool _pinned;   // whether the pinning steps have started

        /// <param name="digits">Decimal digits and underscores, at least one digit.</param>
        /// <param name="route">The test hook, read by the caller: the steps may run on another thread.</param>
        public DecimalParse(bool negative, string digits, long scale, int precision, RoundingMode mode, Route route)
        {
            (_negative, _digits, _scale, _precision, _mode, _route) = (negative, digits, scale, precision, mode, route);
            long count = digits.Length - digits.AsSpan().Count('_');
            long bitsAtMost = count * 3322 / 1000 + 1;   // log2(10) is 3.3219...
            ExpectedWork = ApInt.DecimalDigitUnits(count) + FirstStepUnits(bitsAtMost, scale, (long)precision + GuardBits, route);
        }

        public ApFloat Result { get; private set; }

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
                Result = ZeroOf(_negative, _precision);
                ExpectedWork = WorkDone;
                yield break;
            }

            _pinned = true;
            ExpectedWork = WorkDone + FirstStepUnits(Magnitude.BitLength(digits), _scale, (long)_precision + GuardBits, _route);
            foreach (long units in FromDecimalSteps(_negative, digits, _scale, _precision, _mode, _route, _pinning))
            {
                WorkDone += units;
                ExpectedWork = WorkDone + _pinning.NextUnits;
                yield return units;
            }
            Result = _pinning.Result;
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
/// How decimal text was pinned to its binary value: by the certified interval engine, which
/// brackets the exact value and accepts when both ends round the same way, or by the exact
/// route, which builds the exact rational and rounds it once.
/// </summary>
/// <param name="Rounds">Interval rounds taken. 0 when the exact route ran from the start: always
/// below 10^±4096, and for text that needed no decimal arithmetic (zero, NaN, infinity, hex, binary).</param>
/// <param name="WorkingBits">The last round's working precision, W; 0 with no rounds.</param>
/// <param name="Exact">True when the exact route decided the result. It does for a tie, and for an
/// exact value in a directed mode, because the interval ends straddle the boundary they sit on.</param>
/// <param name="Low">The last round's lower end: Low ≤ the exact value ≤ High. Null with no rounds.</param>
/// <param name="High">The last round's upper end. When the rounds settled it, Low and High round to
/// the same result, which is what certifies it.</param>
public sealed record DecimalCertificate(int Rounds, long WorkingBits, bool Exact, ApFloat? Low, ApFloat? High);

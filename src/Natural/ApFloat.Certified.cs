namespace Natural;

// The certified interval engine (Ziv's strategy), for X × 10^n when n is huge.
//
// Exact decimal conversion builds 5^|n| in full, and that costs the square of n. A correctly
// rounded answer almost never needs all of it. So bound it instead: lo <= 5^n <= hi, by
// square-and-multiply at W bits, every product rounded down for lo and up for hi. Directed
// rounding makes the bounds rigorous with no error analysis. X goes through the same way
// (times the bounds, or divided by them when n < 0), and then both ends are rounded to the
// target. Rounding is monotone, so if the two ends round to the same value, every value
// between them does too, the exact one included: the result is certified. If they don't,
// the exact value is close to a rounding boundary: double W and go again. Past a point
// another round costs more than the exact route, which is always right, so it takes over
// there. So this always ends, and always correctly.
//
// A tie, or an exact value in a directed mode, never certifies (the ends straddle the
// boundary it sits on), so those always reach the exact route. But with a huge n they need
// long input: a decimal q × 10^-n is only a tie or exact if q is a multiple of 5^n.
public readonly partial struct ApFloat
{
    internal enum Route { Auto, Exact, Interval }

    /// <summary>
    /// For tests: force the exact route, or intervals even below the cache (they then run
    /// until the bounds are exact). Thread-static, so parallel tests can't see each other's.
    /// </summary>
    [ThreadStatic] internal static Route ForcedRoute;

    /// <summary>For tests: how many interval rounds the last result on this thread took.</summary>
    [ThreadStatic] internal static int LastRounds;

    /// <summary>For tests: whether the last result on this thread came from the exact route.</summary>
    [ThreadStatic] internal static bool LastExact;

    /// <summary>
    /// Working bits beyond what the result needs, in the first round (and beyond n's bit
    /// length, which the bounds lose). An exact value 2^-57 of an ulp or more from a rounding
    /// boundary settles in one round.
    /// </summary>
    private const int GuardBits = 64;

    /// <summary>
    /// A guard against a loop that never ends. W doubles every round, so the exact route
    /// takes over long before this (by 2^30 bits at the latest).
    /// </summary>
    private const int MaxRounds = 64;

    // log2(5) × 2^128, rounded down: 128 bits of it (checked in Python against 5^n's bit length).
    private static readonly ApInt Log2Of5 = ApInt.Parse("0x25269E12F346E2BF924AFDBFD36BF6D33");

    /// <summary>floor(n · log2 5), for any n. For n &gt;= 0, 5^n is one bit longer than this.</summary>
    internal static long FloorLog2Of5Times(long n) => (long)(((ApInt)n * Log2Of5) >> 128);

    /// <summary>
    /// round(X × 10^n), where X = mag × 2^exp is positive and exact. <paramref name="round"/>
    /// rounds a positive mag × 2^exp to the target (any rounding is monotone, which is all
    /// this needs), and <paramref name="exact"/> gives the same result the exact way.
    /// The first round works with <paramref name="startBits"/> bits, plus n's bit length.
    /// The steps below, run to the end; the test hooks get the rounds and the route.
    /// </summary>
    private static ApFloat TimesPowerOfTen(uint[] mag, long exp, long n, long startBits,
                                           Func<uint[], long, ApFloat> round, Func<ApFloat> exact)
    {
        var pinning = new Pinning();
        return RunToEnd(TimesPowerOfTenSteps(mag, exp, n, startBits, round, exact, ForcedRoute, pinning), pinning);
    }

    /// <summary>Runs pinning steps to the end, on this thread, and leaves the rounds and the route in the test hooks.</summary>
    private static ApFloat RunToEnd(IEnumerable<long> steps, Pinning pinning)
    {
        try
        {
            foreach (long _ in steps) { }
        }
        finally
        {
            LastRounds = pinning.Rounds;
            LastExact = pinning.Exact;
        }
        return pinning.Result;
    }

    /// <summary>
    /// What pinning X × 10^n took, filled in as the steps go: the result, the rounds, and the
    /// last round's interval, for the test hooks and for a job's certificate.
    /// </summary>
    private sealed class Pinning
    {
        public ApFloat Result;
        public int Rounds;
        public long WorkingBits;
        public bool Exact;
        /// <summary>When the rounds settled it: where the value falls against a format's range (RangeOf), agreed by both ends.</summary>
        public IeeeFlags Range;
        /// <summary>When the exact route settled it: the flags its rounding raised.</summary>
        public IeeeFlags Flags;
        /// <summary>The last round's ends: Low &lt;= X × 10^n &lt;= High, at WorkingBits.</summary>
        public ApFloat? Low, High;
        /// <summary>An estimate of the next step's limb steps, for progress: 0 once done.</summary>
        public long NextUnits;
    }

    /// <summary>
    /// <see cref="TimesPowerOfTen"/> as steps a job can pause between: one per interval round,
    /// and one for the exact route if it runs. Each yields an estimate of its limb steps (see
    /// <see cref="RoundUnits"/>), the same on any machine. <paramref name="route"/> is read by
    /// the caller, since a job's steps may run on another thread. <see cref="FirstStepUnits"/>
    /// estimates the first step before it runs. With <paramref name="range"/> (a format's
    /// RangeOf), the ends must agree on it too before a round settles, and it goes in
    /// pinning.Range: the value alone doesn't decide Underflow or Overflow.
    /// </summary>
    private static IEnumerable<long> TimesPowerOfTenSteps(uint[] mag, long exp, long n, long startBits,
                                                          Func<uint[], long, ApFloat> round, Func<ApFloat> exact,
                                                          Route route, Pinning pinning,
                                                          Func<uint[], long, IeeeFlags>? range = null)
    {
        long power = Math.Abs(n), xBits = Magnitude.BitLength(mag);
        if (ExactFromTheStart(n, route))
        {
            pinning.Exact = true;
            pinning.Result = exact();
            pinning.NextUnits = 0;
            yield return ExactUnits(xBits, n, startBits);
            yield break;
        }

        long exactBits = FloorLog2Of5Times(power) + 1;
        ApFloat x = Normalised(false, mag, exp, MaxPrecision);

        // Squaring doubles a relative error, so the bounds on 5^n end up about n · 2^(2-W)
        // apart, relatively, not log(n) times: n's bit length goes on top of the guard bits.
        long nBits = NBits(power);
        for (long w = startBits + nBits; ; w *= 2)
        {
            if (ExactFinishes(w, exactBits, route))
            {
                pinning.Exact = true;
                pinning.Result = exact();
                pinning.NextUnits = 0;
                yield return ExactUnits(xBits, n, startBits);
                yield break;
            }
            if (++pinning.Rounds > MaxRounds) throw new InvalidOperationException("The interval engine didn't converge.");

            int bits = (int)w;
            var (lo5, hi5) = PowerOfFiveBounds(power, bits);
            ApFloat lo = n >= 0
                ? Multiply(x, lo5, bits, RoundingMode.TowardNegative)
                : Divide(x, hi5, bits, RoundingMode.TowardNegative);
            ApFloat hi = n >= 0
                ? Multiply(x, hi5, bits, RoundingMode.TowardPositive)
                : Divide(x, lo5, bits, RoundingMode.TowardPositive);

            // × 2^n: the other half of 10^n.
            long loExp = checked(lo._exp + n), hiExp = checked(hi._exp + n);
            pinning.WorkingBits = w;
            pinning.Low = new ApFloat(Kind.Finite, false, lo.Mant, loExp, bits);
            pinning.High = new ApFloat(Kind.Finite, false, hi.Mant, hiExp, bits);
            ApFloat a = round(lo.Mant, loExp);
            ApFloat b = round(hi.Mant, hiExp);
            IeeeFlags rangeA = range?.Invoke(lo.Mant, loExp) ?? IeeeFlags.None;
            IeeeFlags rangeB = range?.Invoke(hi.Mant, hiExp) ?? IeeeFlags.None;
            long units = RoundUnits(xBits, w, nBits);
            if (SameValue(a, b) && rangeA == rangeB)
            {
                pinning.Result = a;
                pinning.Range = rangeA;
                pinning.NextUnits = 0;
                yield return units;
                yield break;
            }
            pinning.NextUnits = ExactFinishes(2 * w, exactBits, route) ? ExactUnits(xBits, n, startBits) : RoundUnits(xBits, 2 * w, nBits);
            yield return units;
        }
    }

    /// <summary>
    /// Whether a round at w bits would cost more than the exact route. A round costs about
    /// 4·log2(n)·W² bit steps and the exact route about L², where L is 5^n's bit length. So
    /// once W passes L/8 (it never needs to pass L: the bounds are exact there), the exact
    /// route is the cheaper way to finish.
    /// </summary>
    private static bool ExactFinishes(long w, long exactBits, Route route) =>
        w > MaxPrecision || (route == Route.Auto && w * 8 >= exactBits);

    private static long NBits(long power) => 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)power);

    /// <summary>The estimate of <see cref="TimesPowerOfTenSteps"/>'s first step, before it runs.</summary>
    /// <summary>
    /// Whether × 10^n goes straight to the exact route: below the cache (it's cheap there), or
    /// forced. Then <see cref="TimesPowerOfTenSteps"/> is one step, exact() and its
    /// <see cref="ExactUnits"/>, which a caller may take itself and skip the iterator.
    /// </summary>
    private static bool ExactFromTheStart(long n, Route route) =>
        route == Route.Exact || (route == Route.Auto && Math.Abs(n) < CachedFives);

    private static long FirstStepUnits(long xBits, long n, long startBits, Route route)
    {
        long power = Math.Abs(n);
        if (ExactFromTheStart(n, route)) return ExactUnits(xBits, n, startBits);
        // Past the cache the steps compute 5^n's bit length anyway, and the parse costs far more.
        long w = startBits + NBits(power);
        return ExactFinishes(w, FloorLog2Of5Times(power) + 1, route) ? ExactUnits(xBits, n, startBits) : RoundUnits(xBits, w, NBits(power));
    }

    /// <summary>
    /// Limb steps for one round at w bits, estimated from the sizes: the bounds on 5^n take
    /// about three w-bit shift-and-add products per bit of n, each about w²/64, and X times
    /// (or over) the two bounds about xBits·w/16.
    /// </summary>
    private static long RoundUnits(long xBits, long w, long nBits) =>
        Units((Int128)3 * nBits * w * w / 64 + (Int128)xBits * w / 16);

    /// <summary>
    /// Limb steps for the exact route, estimated from the sizes: 5^|n| when it's past the
    /// cache (its last squaring dominates: about L²/144 in all, for L its bit length), then
    /// X × 5^n (xBits·L/64), or the restoring division by 5^|n|: a step per quotient bit, and
    /// it keeps all of them, at least startBits (about quotient bits · L/64).
    /// L is |n|·log2(5) to seven digits: an estimate needn't pay for the exact bit length,
    /// and on a short parse that would cost more than the parse.
    /// </summary>
    private static long ExactUnits(long xBits, long n, long startBits)
    {
        long power = Math.Abs(n);
        Int128 fiveBits = (Int128)power * 2_321_929 / 1_000_000 + 1;
        Int128 build = power < CachedFives ? 0 : fiveBits * fiveBits / 144;
        Int128 use = n >= 0 ? xBits * fiveBits / 64 : Int128.Max(xBits - fiveBits, startBits + 2) * fiveBits / 64;
        return Units(build + use + 1);
    }

    /// <summary>An estimate as a long, held well clear of overflow when it's added up.</summary>
    private static long Units(Int128 estimate) => (long)Int128.Min(estimate, long.MaxValue / 4);

    /// <summary>
    /// lo &lt;= 5^n &lt;= hi, each at most <paramref name="workingBits"/> bits long: square and
    /// multiply, with every product rounded down for lo and up for hi.
    /// </summary>
    internal static (ApFloat Lo, ApFloat Hi) PowerOfFiveBounds(long n, int workingBits)
    {
        ApFloat lo = new(Kind.Finite, false, [1], 0, workingBits), hi = lo;
        ApFloat squareLo = RoundExact(false, [5], 0, false, workingBits, RoundingMode.TowardNegative);
        ApFloat squareHi = RoundExact(false, [5], 0, false, workingBits, RoundingMode.TowardPositive);
        for (; n > 0; n >>= 1)
        {
            if ((n & 1) != 0)
            {
                lo = Multiply(lo, squareLo, workingBits, RoundingMode.TowardNegative);
                hi = Multiply(hi, squareHi, workingBits, RoundingMode.TowardPositive);
            }
            if (n > 1)
            {
                squareLo = Multiply(squareLo, squareLo, workingBits, RoundingMode.TowardNegative);
                squareHi = Multiply(squareHi, squareHi, workingBits, RoundingMode.TowardPositive);
            }
        }
        return (lo, hi);
    }

    /// <summary>The same value exactly: kind, sign, exponent and significand.</summary>
    private static bool SameValue(ApFloat a, ApFloat b) =>
        a._kind == b._kind && a._negative == b._negative
        && (a._kind != Kind.Finite || (a._exp == b._exp && Magnitude.Compare(a.Mant, b.Mant) == 0));
}

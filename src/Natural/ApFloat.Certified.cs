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
    /// </summary>
    private static ApFloat TimesPowerOfTen(uint[] mag, long exp, long n, long startBits,
                                           Func<uint[], long, ApFloat> round, Func<ApFloat> exact)
    {
        LastRounds = 0;
        LastExact = false;
        Route route = ForcedRoute;
        long power = Math.Abs(n);
        if (route == Route.Exact || (route == Route.Auto && power < CachedFives)) return Exact();

        // A round costs about 4·log2(n)·W² bit steps and the exact route about L², where L is
        // 5^n's bit length. So once W passes L/8 (it never needs to pass L: the bounds are
        // exact there), the exact route is the cheaper way to finish.
        long exactBits = FloorLog2Of5Times(power) + 1;
        ApFloat x = Normalised(false, mag, exp, MaxPrecision);

        // Squaring doubles a relative error, so the bounds on 5^n end up about n · 2^(2-W)
        // apart, relatively, not log(n) times: n's bit length goes on top of the guard bits.
        long nBits = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)power);
        for (long w = startBits + nBits; ; w *= 2)
        {
            if (w > MaxPrecision || (route == Route.Auto && w * 8 >= exactBits)) return Exact();
            if (++LastRounds > MaxRounds) throw new InvalidOperationException("The interval engine didn't converge.");

            int bits = (int)w;
            var (lo5, hi5) = PowerOfFiveBounds(power, bits);
            ApFloat lo = n >= 0
                ? Multiply(x, lo5, bits, RoundingMode.TowardNegative)
                : Divide(x, hi5, bits, RoundingMode.TowardNegative);
            ApFloat hi = n >= 0
                ? Multiply(x, hi5, bits, RoundingMode.TowardPositive)
                : Divide(x, lo5, bits, RoundingMode.TowardPositive);

            // × 2^n: the other half of 10^n.
            ApFloat a = round(lo.Mant, checked(lo._exp + n));
            ApFloat b = round(hi.Mant, checked(hi._exp + n));
            if (SameValue(a, b)) return a;
        }

        ApFloat Exact()
        {
            LastExact = true;
            return exact();
        }
    }

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

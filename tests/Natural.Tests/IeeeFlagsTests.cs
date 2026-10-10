using System.Numerics;
using static Natural.Tests.FloatOracle;

namespace Natural.Tests;

/// <summary>
/// IeeeContext and its five flags, against IEEE 754 worked out independently: the exact
/// rational value, the oracle's rounding with no floor on the exponent (for overflow, and for
/// tininess, which this library judges after rounding), and the operand kinds for the
/// invalid operations and division by zero.
/// </summary>
public class IeeeFlagsTests
{
    private static readonly char[] Ops = ['+', '-', '*', '/'];

    private static ApFloat Apply(IeeeContext context, char op, ApFloat a, ApFloat b) => op switch
    {
        '+' => context.Add(a, b),
        '-' => context.Subtract(a, b),
        '*' => context.Multiply(a, b),
        _ => context.Divide(a, b),
    };

    private static ApFloat Static(char op, ApFloat a, ApFloat b, IeeeFormat format, RoundingMode mode) => op switch
    {
        '+' => ApFloat.Add(a, b, format, mode),
        '-' => ApFloat.Subtract(a, b, format, mode),
        '*' => ApFloat.Multiply(a, b, format, mode),
        _ => ApFloat.Divide(a, b, format, mode),
    };

    private static ApFloat Static(char op, ApFloat a, ApFloat b, int precision, RoundingMode mode) => op switch
    {
        '+' => ApFloat.Add(a, b, precision, mode),
        '-' => ApFloat.Subtract(a, b, precision, mode),
        '*' => ApFloat.Multiply(a, b, precision, mode),
        _ => ApFloat.Divide(a, b, precision, mode),
    };

    /// <summary>
    /// The flags IEEE 754 says a op b raises, given the result it rounded to. A null format is a
    /// precision with an unbounded exponent, which can't overflow or underflow.
    /// </summary>
    private static IeeeFlags Expected(char op, ApFloat a, ApFloat b, ApFloat result, IeeeFormat? format, RoundingMode mode)
    {
        if (a.IsNaN || b.IsNaN) return IeeeFlags.None;                      // quiet NaNs pass through silently
        bool bNegative = op == '-' ? !b.IsNegative : b.IsNegative;
        switch (op)
        {
            case '+' or '-':
                if (a.IsInfinity && b.IsInfinity && a.IsNegative != bNegative) return IeeeFlags.Invalid;   // ∞ − ∞
                if (a.IsInfinity || b.IsInfinity) return IeeeFlags.None;
                break;
            case '*':
                if ((a.IsInfinity && b.IsZero) || (a.IsZero && b.IsInfinity)) return IeeeFlags.Invalid;   // 0 × ∞
                if (a.IsInfinity || b.IsInfinity) return IeeeFlags.None;
                break;
            default:
                if ((a.IsInfinity && b.IsInfinity) || (a.IsZero && b.IsZero)) return IeeeFlags.Invalid;   // ∞/∞, 0/0
                if (a.IsInfinity || b.IsInfinity) return IeeeFlags.None;                                  // ∞/x, x/∞: exact
                if (b.IsZero) return IeeeFlags.DivideByZero;
                break;
        }

        var (an, ad) = ToRational(a);
        var (bn, bd) = ToRational(b);
        var (num, den) = op switch
        {
            '+' => (an * bd + bn * ad, ad * bd),
            '-' => (an * bd - bn * ad, ad * bd),
            '*' => (an * bn, ad * bd),
            _ => (an * bd, ad * bn),
        };
        if (num.IsZero) return IeeeFlags.None;                               // an exact zero
        bool inexact = !result.IsFinite || result.IsZero || ToRational(result) is var (rn, rd) && rn * den != num * rd;
        IeeeFlags flags = inexact ? IeeeFlags.Inexact : IeeeFlags.None;
        if (format is not { } f) return flags;

        // Rounded to the precision with no floor or ceiling on the exponent: where it lands says
        // whether the result overflowed, and whether it's tiny.
        Rounded u = Round(num, den, f.Precision, mode);
        long top = u.E + (long)u.N.GetBitLength() - 1;
        if (top > f.MaxExponent) return IeeeFlags.Overflow | IeeeFlags.Inexact;
        if (top < f.MinExponent && inexact) flags |= IeeeFlags.Underflow;
        return flags;
    }

    private static void Check(char op, ApFloat a, ApFloat b, IeeeFormat format, RoundingMode mode, string context)
    {
        var ieee = new IeeeContext(format, mode);
        ApFloat r = Apply(ieee, op, a, b);
        AssertSame(Static(op, a, b, format, mode), r, context + ": not the static result");
        IeeeFlags expected = Expected(op, a, b, r, format, mode);
        Assert.True(expected == ieee.Flags, $"{context} {mode} = {Describe(r)}: expected {expected}, raised {ieee.Flags}");
    }

    // ------------------------------------------------------------------
    // Against the oracle
    // ------------------------------------------------------------------

    [Fact]
    public void Binary64FlagsMatchTheOracle()
    {
        // Operand pairs crowded round the subnormals and the top of the range, specials included.
        var rng = new Random(700);
        for (int i = 0; i < 20_000; i++)
        {
            var (aBits, bBits) = RandomPair(rng, 11, 53);
            ApFloat a = ApFloat.FromIeeeBits(aBits, 11, 53), b = ApFloat.FromIeeeBits(bBits, 11, 53);
            char op = Ops[rng.Next(4)];
            Check(op, a, b, IeeeFormat.Binary64, Modes[rng.Next(4)], $"{aBits:X16} {op} {bBits:X16}");
        }
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    public void TinyFormatsExhaustively(int w, int p)
    {
        // Every pair of values in a 6-bit format, NaN and infinities included, every operation
        // and mode: every flag's edge is in here somewhere.
        var format = new IeeeFormat(w, p);
        for (ulong x = 0; x < 1UL << (w + p); x++)
        for (ulong y = 0; y < 1UL << (w + p); y++)
        {
            ApFloat a = ApFloat.FromIeeeBits(x, w, p), b = ApFloat.FromIeeeBits(y, w, p);
            foreach (char op in Ops)
            foreach (RoundingMode mode in Modes)
                Check(op, a, b, format, mode, $"w={w} p={p}: {x:X} {op} {y:X}");
        }
    }

    [Fact]
    public void PrecisionContextsRaiseOnlyInexactAndTheSpecials()
    {
        // An unbounded exponent can't overflow or underflow, however far out the values are.
        var rng = new Random(701);
        foreach (int p in new[] { 1, 24, 53, 200 })
        {
            for (int i = 0; i < 1500; i++)
            {
                ApFloat a = RandomFinite(rng, 6, 5000), b = RandomFinite(rng, 6, 5000);
                char op = Ops[rng.Next(4)];
                RoundingMode mode = Modes[rng.Next(4)];
                var ieee = new IeeeContext(p, mode);
                ApFloat r = Apply(ieee, op, a, b);
                AssertSame(Static(op, a, b, p, mode), r);
                Assert.Equal(Expected(op, a, b, r, null, mode), ieee.Flags);
                Assert.False(ieee.Flags.HasFlag(IeeeFlags.Overflow) || ieee.Flags.HasFlag(IeeeFlags.Underflow));
            }
        }
    }

    [Fact]
    public void TininessIsJudgedAfterRounding()
    {
        // 2^-1022 - 2^-1076: just under the smallest normal double. Rounded to nearest with 53
        // bits it becomes 2^-1022, which isn't tiny, so the inexact result raises no Underflow.
        // Rounded toward zero it stays under, and becomes the largest subnormal: Underflow.
        // (Judged before rounding, both would underflow.)
        var justUnder = new ApFloat((ApInt.One << 54) - 1, -1076, 64);
        var nearest = new IeeeContext(IeeeFormat.Binary64);
        AssertSame(new ApFloat(ApInt.One, -1022), nearest.Round(justUnder));
        Assert.Equal(IeeeFlags.Inexact, nearest.Flags);

        var down = new IeeeContext(IeeeFormat.Binary64, RoundingMode.TowardZero);
        AssertSame(new ApFloat((ApInt.One << 52) - 1, -1074), down.Round(justUnder));
        Assert.Equal(IeeeFlags.Inexact | IeeeFlags.Underflow, down.Flags);

        // An exact subnormal result raises nothing, tiny as it is.
        var exact = new IeeeContext(IeeeFormat.Binary64);
        exact.Multiply(double.Epsilon, 3.0);
        Assert.Equal(IeeeFlags.None, exact.Flags);
    }

    [Fact]
    public void RoundIsConvertFormatWithFlags()
    {
        var binary64 = IeeeFormat.Binary64;
        (string Text, IeeeFlags Flags)[] cases =
        [
            ("0.5", IeeeFlags.None),
            ("0.1", IeeeFlags.Inexact),
            ("1e400", IeeeFlags.Overflow | IeeeFlags.Inexact),
            ("-1e400", IeeeFlags.Overflow | IeeeFlags.Inexact),
            ("1e-400", IeeeFlags.Underflow | IeeeFlags.Inexact),
            ("NaN", IeeeFlags.None),
            ("-Infinity", IeeeFlags.None),
            ("-0", IeeeFlags.None),
        ];
        foreach (RoundingMode mode in Modes)
        foreach (var (text, flags) in cases)
        {
            ApFloat x = ApFloat.Parse(text, 256);
            var ieee = new IeeeContext(binary64, mode);
            AssertSame(x.WithFormat(binary64, mode), ieee.Round(x), text);
            Assert.True(flags == ieee.Flags, $"{text} {mode}: expected {flags}, raised {ieee.Flags}");
        }

        var p24 = new IeeeContext(24);
        AssertSame(ApFloat.Parse("0.1", 256).WithPrecision(24), p24.Round(ApFloat.Parse("0.1", 256)));
        Assert.Equal(IeeeFlags.Inexact, p24.Flags);
    }

    // ------------------------------------------------------------------
    // The context itself
    // ------------------------------------------------------------------

    [Fact]
    public void FlagsAreStickyUntilCleared()
    {
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        ieee.Divide(1.0, 3.0);
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);
        ieee.Add(0.5, 0.25);                         // exact: lowers nothing
        Assert.Equal(IeeeFlags.Inexact, ieee.Flags);
        ieee.Divide(1.0, 0.0);
        Assert.Equal(IeeeFlags.Inexact | IeeeFlags.DivideByZero, ieee.Flags);
        ieee.Multiply(ApFloat.PositiveInfinity, 0.0);
        Assert.Equal(IeeeFlags.Inexact | IeeeFlags.DivideByZero | IeeeFlags.Invalid, ieee.Flags);

        IeeeFlags saved = ieee.Flags;
        ieee.ClearFlags();
        Assert.Equal(IeeeFlags.None, ieee.Flags);
        ieee.Flags = saved;                          // restore
        Assert.Equal(saved, ieee.Flags);
    }

    [Fact]
    public void TheModeIsTheContexts()
    {
        var ieee = new IeeeContext(IeeeFormat.Binary32, RoundingMode.TowardPositive);
        AssertSame(ApFloat.Divide(1.0f, 3.0f, IeeeFormat.Binary32, RoundingMode.TowardPositive), ieee.Divide(1.0f, 3.0f));
        ieee.Mode = RoundingMode.TowardNegative;
        AssertSame(ApFloat.Divide(1.0f, 3.0f, IeeeFormat.Binary32, RoundingMode.TowardNegative), ieee.Divide(1.0f, 3.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => { ieee.Mode = (RoundingMode)99; });
        Assert.Equal(RoundingMode.TowardNegative, ieee.Mode);
    }

    [Fact]
    public void ATargetIsAFormatOrAPrecision()
    {
        var format = new IeeeContext(IeeeFormat.Binary32);
        Assert.Equal<IeeeFormat?>(IeeeFormat.Binary32, format.Format);
        Assert.Equal(24, format.Precision);
        var precision = new IeeeContext(300, RoundingMode.TowardZero);
        Assert.Null(precision.Format);
        Assert.Equal(300, precision.Precision);
        Assert.Equal("300 bits, TowardZero, flags None", precision.ToString());

        Assert.Throws<ArgumentOutOfRangeException>(() => new IeeeContext(0));
        Assert.Throws<ArgumentException>(() => new IeeeContext(default(IeeeFormat)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IeeeContext(53, (RoundingMode)(-1)));
    }

    [Fact]
    public void TheStaticMethodsAreUnchanged()
    {
        // Same results, and no flags anywhere to raise: the static methods take no context.
        var ieee = new IeeeContext(IeeeFormat.Binary64);
        AssertSame(ApFloat.Divide(1e300, 1e-300, IeeeFormat.Binary64), ieee.Divide(1e300, 1e-300));
        Assert.Equal(IeeeFlags.Overflow | IeeeFlags.Inexact, ieee.Flags);
        Assert.True(ApFloat.Divide(1e300, 1e-300, IeeeFormat.Binary64).IsInfinity);
    }
}

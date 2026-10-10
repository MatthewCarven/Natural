namespace Natural;

/// <summary>IEEE 754's five status flags (§7). Raised, never lowered, by the operations of an <see cref="IeeeContext"/>.</summary>
[Flags]
public enum IeeeFlags
{
    None = 0,
    /// <summary>The result isn't the exact value: something was rounded off (or it overflowed).</summary>
    Inexact = 1,
    /// <summary>The result is tiny (below the smallest normal, judged after rounding) and inexact. Formats only.</summary>
    Underflow = 2,
    /// <summary>The result was too large for the format: it's infinite, or the largest finite value. Formats only.</summary>
    Overflow = 4,
    /// <summary>A finite non-zero value divided by zero: the result is an exact infinity.</summary>
    DivideByZero = 8,
    /// <summary>No useful result: 0 × ∞, ∞ − ∞, 0 / 0, ∞ / ∞. The result is NaN.</summary>
    Invalid = 16,
}

/// <summary>
/// IEEE 754's rounding attribute and status flags, held in an object you own (Matthew's choice,
/// 2026-10-10). Its operations round into its target, a format or a precision, in its
/// <see cref="Mode"/>, and raise <see cref="Flags"/>, which stay raised until cleared. The static
/// ApFloat methods give the same results without flags. With a precision the exponent is
/// unbounded, so only Inexact, DivideByZero and Invalid can be raised; with a format, Underflow
/// and Overflow too. Not thread-safe: one context per thread, as with any running total.
/// </summary>
public sealed class IeeeContext
{
    private readonly ApFloat.Target _target;
    private RoundingMode _mode;

    /// <summary>A context that rounds into the format: subnormals, overflow and all.</summary>
    public IeeeContext(IeeeFormat format, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        _target = ApFloat.Target.Of(format);
        Mode = mode;
    }

    /// <summary>A context that rounds to the precision, with an unbounded exponent.</summary>
    public IeeeContext(int precision, RoundingMode mode = RoundingMode.ToNearestEven)
    {
        _target = ApFloat.Target.Of(precision);
        Mode = mode;
    }

    /// <summary>The format results are rounded into, or null for a precision with an unbounded exponent.</summary>
    public IeeeFormat? Format => _target.Format;

    /// <summary>The precision results are rounded to (a format's, when there is one).</summary>
    public int Precision => _target.Precision;

    /// <summary>The rounding direction for every operation from here on.</summary>
    public RoundingMode Mode
    {
        get => _mode;
        set => _mode = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Not a rounding mode.");
    }

    /// <summary>The flags raised since the last clear. Set it to save and restore them.</summary>
    public IeeeFlags Flags { get; set; }

    /// <summary>Lowers every flag.</summary>
    public void ClearFlags() => Flags = IeeeFlags.None;

    public ApFloat Add(ApFloat a, ApFloat b)
    {
        IeeeFlags flags = Flags;
        ApFloat r = ApFloat.Add(a, b, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    public ApFloat Subtract(ApFloat a, ApFloat b) => Add(a, -b);

    public ApFloat Multiply(ApFloat a, ApFloat b)
    {
        IeeeFlags flags = Flags;
        ApFloat r = ApFloat.Multiply(a, b, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    public ApFloat Divide(ApFloat a, ApFloat b)
    {
        IeeeFlags flags = Flags;
        ApFloat r = ApFloat.Divide(a, b, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    /// <summary>
    /// x rounded into the target (IEEE's convertFormat): <see cref="ApFloat.WithFormat"/> or
    /// <see cref="ApFloat.WithPrecision"/>, with flags. (Called Round on 2026-10-10; renamed the next
    /// day, when ApFloat.Round came to mean rounding to an integer, as Math.Round does.)
    /// </summary>
    public ApFloat Convert(ApFloat x)
    {
        IeeeFlags flags = Flags;
        ApFloat r = ApFloat.Convert(x, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    /// <summary>a × b + c, rounded once into the target. Invalid for 0 × ∞, and for ∞ × y + ∞ of the other sign.</summary>
    public ApFloat FusedMultiplyAdd(ApFloat a, ApFloat b, ApFloat c)
    {
        IeeeFlags flags = Flags;
        ApFloat r = ApFloat.FusedMultiplyAdd(a, b, c, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    /// <summary>
    /// x rounded to an integer in the context's <see cref="Mode"/>, raising Inexact when that
    /// changed it (IEEE's roundToIntegralExact), then into the target. For .NET's midpoint rules,
    /// use <see cref="ApFloat.Round(ApFloat, MidpointRounding)"/> and <see cref="Convert"/>.
    /// </summary>
    public ApFloat RoundToIntegral(ApFloat x)
    {
        IeeeFlags flags = Flags;
        ApFloat integral = ApFloat.RoundToIntegral(x, _mode);
        if (x.IsFinite && integral != x) flags |= IeeeFlags.Inexact;
        ApFloat r = ApFloat.Convert(integral, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    /// <summary>
    /// IEEE's remainder, x − n·y with n the nearest integer to x/y (ties to even): exact, then
    /// into the target. Invalid for an infinite x or a zero y.
    /// </summary>
    public ApFloat Ieee754Remainder(ApFloat x, ApFloat y)
    {
        IeeeFlags flags = Flags;
        ApFloat remainder = ApFloat.Ieee754Remainder(x, y, ref flags);
        ApFloat r = ApFloat.Convert(remainder, _target, _mode, ref flags);
        Flags = flags;
        return r;
    }

    public override string ToString() =>
        $"{(Format is { } format ? format.ToString() : $"{Precision} bits")}, {Mode}, flags {Flags}";
}

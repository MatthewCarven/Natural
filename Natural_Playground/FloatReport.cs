using System.Text;
using Natural;

namespace Natural_Playground;

/// <summary>
/// Turns a pair of operands into the result plus a full account of how it is stored.
/// Kept free of any UI so it can be exercised without a window.
/// </summary>
internal static class FloatReport
{
    public const string Add = "+", Subtract = "-", Multiply = "*", Divide = "/";

    /// <summary>How wide a significand or bit string may get before it's clipped.</summary>
    private const int MaxDigitsShown = 72;

    public static string Describe(string aText, string bText, string op, int precision, RoundingMode mode)
    {
        try
        {
            // The operands are parsed at the working precision too, so everything on
            // screen is at one precision and the static methods -- not the operators,
            // which round to the larger of the operands' precisions -- do the rounding.
            ApFloat a = ApFloat.Parse(aText, precision, mode);
            ApFloat b = ApFloat.Parse(bText, precision, mode);
            ApFloat r = op switch
            {
                Add => ApFloat.Add(a, b, precision, mode),
                Subtract => ApFloat.Subtract(a, b, precision, mode),
                Multiply => ApFloat.Multiply(a, b, precision, mode),
                Divide => ApFloat.Divide(a, b, precision, mode),
                _ => throw new ArgumentException($"Unknown operation \"{op}\".", nameof(op)),
            };

            var sb = new StringBuilder();
            Values(sb, a, b, r, op, precision);
            Decomposition(sb, r, precision);
            DoubleCheck(sb, a, b, r, op, precision);
            return sb.ToString();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException)
        {
            return ex.Message;
        }
    }

    private static void Values(StringBuilder sb, ApFloat a, ApFloat b, ApFloat r, string op, int precision)
    {
        sb.AppendLine($"{a.ToString("R")} {op} {b.ToString("R")} = {r.ToString("R")}");
        int decimals = ApFloat.DecimalDigitsFor(precision) - 1;
        Format(sb, $"E{decimals}", () => r.ToString($"E{decimals}"));
        Format(sb, "F4", () => r.ToString("F4"));
        Format(sb, "A", () => r.ToString("A"));
        Format(sb, "B", () => BinaryView(r));
        Format(sb, "G", () => r.ToString("G"));
        sb.AppendLine();
    }

    /// <summary>
    /// One format on its own, so a format that can't represent the value -- "F4" of
    /// 1e1000000000000000000 wants more digits than exist -- shows why instead of
    /// taking the whole report down with it.
    /// </summary>
    private static void Format(StringBuilder sb, string label, Func<string> get)
    {
        string value;
        try
        {
            value = get();
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            value = $"-- {ex.GetType().Name}: {ex.Message}";
        }
        Row(sb, label, value);
    }

    /// <summary>The "B" format gives the point but not the scale, so tack the scale on.</summary>
    private static string BinaryView(ApFloat r)
    {
        if (!r.IsFinite || r.IsZero) return r.ToString("B");   // a zero has no binade
        return $"{Ellipsis(r.ToString("B"), MaxDigitsShown)} × 2^{Top(r)}";
    }

    private static void Decomposition(StringBuilder sb, ApFloat r, int precision)
    {
        sb.AppendLine("── decomposition " + new string('─', 44));

        // Significand and Exponent both throw for an infinity or a NaN, so the kind
        // has to be checked before reaching for either of them.
        if (r.IsNaN)
        {
            Row(sb, "kind", "NaN");
            Row(sb, "precision", precision.ToString());
            sb.AppendLine();
            return;
        }
        if (r.IsInfinity)
        {
            Row(sb, "kind", $"infinity {(r.IsNegative ? "-" : "+")}");
            Row(sb, "precision", precision.ToString());
            sb.AppendLine();
            return;
        }

        Row(sb, "kind", r.IsZero ? "zero" : "finite");
        Row(sb, "sign", r.IsNegative ? "-" : "+");
        Row(sb, "precision", $"{precision}  ({ApFloat.DecimalDigitsFor(precision)} decimal digits)");
        Row(sb, "exponent", r.Exponent.ToString());

        if (r.IsZero)
        {
            Row(sb, "significand", "0");
            Row(sb, "top", "-- a zero has no binade --");
            Row(sb, "ulp", $"2^{-precision + 1}   (smallest non-zero at this precision)");
            sb.AppendLine();
            return;
        }

        ApInt significand = r.Significand;
        long top = r.Exponent + significand.BitLength - 1;
        long ulpExponent = top - precision + 1;

        Row(sb, "significand", $"{Ellipsis(significand.ToString("X"), MaxDigitsShown)}  ({significand.BitLength} {Bit(significand.BitLength)})");
        Row(sb, "", Ellipsis(significand.ToString("B"), MaxDigitsShown));
        Row(sb, "top", top.ToString());
        Row(sb, "ulp", $"2^{ulpExponent} = {new ApFloat(ApInt.One, ulpExponent, precision).ToString("E6")}");
        sb.AppendLine();
    }

    /// <summary>
    /// The library claims to match double exactly at 53 bits through double's normal
    /// range, so run the same operation on the hardware and show whether it does.
    /// </summary>
    private static void DoubleCheck(StringBuilder sb, ApFloat a, ApFloat b, ApFloat r, string op, int precision)
    {
        sb.AppendLine("── versus double " + new string('─', 43));

        double da = (double)a, db = (double)b;
        double expected = op switch
        {
            Add => da + db,
            Subtract => da - db,
            Multiply => da * db,
            _ => da / db,
        };
        double actual = (double)r;

        Row(sb, "double", expected.ToString("R"));
        Row(sb, "apfloat", actual.ToString("R"));

        // The exponent here is unbounded, so a finite result can sit past double's reach
        // entirely. Both sides then read infinity, which is agreement about nothing.
        if (r.IsFinite && !r.IsZero && (double.IsInfinity(actual) || actual == 0.0))
        {
            Row(sb, "", double.IsInfinity(actual)
                ? "finite here, but past double's range"
                : "finite here, but under double's smallest subnormal");
        }
        else
        {
            Row(sb, "", actual.Equals(expected) ? "agrees" : "DIFFERS");
        }

        if (precision != 53)
            sb.AppendLine("(only expected to agree at 53 bits; this ran at " + precision + ")");
        sb.AppendLine();
    }

    /// <summary>The binade's leading power of two: 2^Top &lt;= |x| &lt; 2^(Top+1).</summary>
    private static long Top(ApFloat r) => r.Exponent + r.Significand.BitLength - 1;

    private static void Row(StringBuilder sb, string label, string value) =>
        sb.AppendLine($"{label,-13}{value}");

    private static string Bit(long n) => n == 1 ? "bit" : "bits";

    private static string Ellipsis(string s, int max) =>
        s.Length <= max ? s : $"{s[..max]}… (+{s.Length - max} chars)";
}
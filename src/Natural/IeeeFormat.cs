namespace Natural;

/// <summary>
/// An IEEE 754 binary interchange format, <see cref="Width"/> bits wide: a sign bit, then
/// <see cref="ExponentBits"/> bits of biased exponent, then <see cref="Precision"/> - 1
/// fraction bits. The significand's leading bit isn't stored: it's 1 for normal numbers,
/// and 0 for zero and the subnormals (exponent field all zeros). A field of all ones is
/// infinity (fraction zero) or NaN.
///
/// The standard formats are <see cref="Binary(int)"/>: binary16, 32, 64, 128, and every
/// multiple of 32 above 128. Others can be made by hand: bfloat16 is <c>new IeeeFormat(8, 8)</c>.
/// </summary>
public readonly record struct IeeeFormat
{
    /// <summary>The widest exponent field whose range still fits a <see cref="long"/> exponent.</summary>
    public const int MaxExponentBits = 62;

    public int ExponentBits { get; }

    /// <summary>Significand bits, the implicit leading bit included: 53 for binary64.</summary>
    public int Precision { get; }

    /// <summary>Total bits: sign + exponent + fraction = <see cref="ExponentBits"/> + <see cref="Precision"/>.</summary>
    public int Width => ExponentBits + Precision;

    public IeeeFormat(int exponentBits, int precision)
    {
        if (exponentBits < 2 || exponentBits > MaxExponentBits)
            throw new ArgumentOutOfRangeException(nameof(exponentBits), exponentBits,
                $"An IEEE format has 2 to {MaxExponentBits} exponent bits.");
        // At least one fraction bit, or NaN couldn't be told from infinity.
        if (precision < 2 || precision > ApFloat.MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"An IEEE format has a precision of 2 to {ApFloat.MaxPrecision} bits.");
        ExponentBits = exponentBits;
        Precision = precision;
    }

    /// <summary>
    /// The largest exponent field over two, max / 2 = 0111...1: 15, 127, 1023, 16383.
    /// A stored field f means 2^(f - bias).
    /// </summary>
    public long Bias => (long)(~(~0UL << ExponentBits) >> 1);

    /// <summary>emax: the largest finite values lie in [2^emax, 2^(emax+1)). It equals the bias.</summary>
    public long MaxExponent => Bias;

    /// <summary>emin = 1 - bias: the smallest normal value is 2^emin. Below it are the subnormals.</summary>
    public long MinExponent => 1 - Bias;

    public static IeeeFormat Binary16 => new(5, 11);
    public static IeeeFormat Binary32 => new(8, 24);
    public static IeeeFormat Binary64 => new(11, 53);
    public static IeeeFormat Binary128 => new(15, 113);
    public static IeeeFormat Binary256 => new(19, 237);

    /// <summary>
    /// binary{k} as IEEE 754-2008 §3.6 defines it: k = 16, 32 or 64, or any multiple of 32
    /// from 128, where the exponent field is round(4·log2 k) - 13 bits and the rest is
    /// precision. binary256 is (19, 237); binary1024 is (27, 997). The widest whose exponent
    /// fits is binary480768.
    /// </summary>
    public static IeeeFormat Binary(int k) =>
        TryStandard(k, out IeeeFormat format)
            ? format
            : throw new ArgumentOutOfRangeException(nameof(k), k,
                "binary{k} exists for k = 16, 32, 64, and multiples of 32 from 128 to 480768.");

    private static bool TryStandard(int k, out IeeeFormat format)
    {
        format = default;
        switch (k)
        {
            case 16: format = Binary16; return true;
            case 32: format = Binary32; return true;
            case 64: format = Binary64; return true;
        }
        if (k < 128 || (k & 31) != 0) return false;

        // round(4·log2 k) is the t with t - 1/2 <= 4·log2 k < t + 1/2. Doubling and raising
        // 2 to each side: 2^(2t-1) <= k^8 < 2^(2t+1). So k^8's bit length, floor(log2 k^8) + 1,
        // is 2t or 2t + 1, and t is half of it, rounded down. (It is never a tie: k^8 would
        // have to be an odd power of two.) No floating-point log anywhere.
        ApInt k8 = k;
        k8 *= k8;
        k8 *= k8;
        k8 *= k8;
        long w = (k8.BitLength >> 1) - 13;
        if (w > MaxExponentBits) return false;
        format = new IeeeFormat((int)w, k - (int)w);
        return true;
    }

    /// <summary>"binary128" for a standard format; otherwise the two parameters.</summary>
    public override string ToString() =>
        TryStandard(Width, out IeeeFormat standard) && standard == this
            ? $"binary{Width}"
            : $"IeeeFormat(exponent bits {ExponentBits}, precision {Precision})";
}

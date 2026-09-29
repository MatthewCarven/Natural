using System.Numerics;

namespace Natural.Tests;

/// <summary>
/// Test helpers. System.Numerics.BigInteger is the reference answer here; the
/// library itself never touches it. Conversions go through raw magnitude bytes so
/// they don't depend on the parsing and formatting code under test.
/// </summary>
internal static class Oracle
{
    public static ApInt ToAp(BigInteger b) =>
        ApInt.FromMagnitude(BigInteger.Abs(b).ToByteArray(isUnsigned: true), b.Sign < 0);

    public static BigInteger ToBig(ApInt a)
    {
        var magnitude = new BigInteger(a.ToMagnitudeBytes(), isUnsigned: true);
        return a.IsNegative ? -magnitude : magnitude;
    }

    /// <summary>
    /// A random integer of up to <paramref name="maxLimbs"/> 32-bit limbs. A third
    /// of limbs are all ones and a third all zeros: long carry and borrow chains are
    /// where adders go wrong, and uniformly random limbs almost never produce them.
    /// </summary>
    public static BigInteger RandomBig(Random rng, int maxLimbs, bool allowNegative = true)
    {
        int limbs = rng.Next(maxLimbs + 1);
        var bytes = new byte[limbs * 4];
        for (int i = 0; i < limbs; i++)
        {
            uint limb = rng.Next(3) switch
            {
                0 => uint.MaxValue,
                1 => 0,
                _ => (uint)rng.NextInt64(1L << 32),
            };
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), limb);
        }
        var value = new BigInteger(bytes, isUnsigned: true);
        return allowNegative && rng.Next(2) == 0 ? -value : value;
    }
}

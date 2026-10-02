using System.Globalization;
using System.Numerics;

namespace Natural.Tests;

/// <summary>
/// The BCD register that binary-to-decimal runs through is sized from an estimate of
/// log10(2), and it has to be an over-estimate: the register is written to by index,
/// so falling short is an overrun, not a slow path.
/// </summary>
public class DecimalDigitsEstimateTests
{
    /// <summary>log10(2) × 2^128, rounded down. Deliberately independent of the library's copy.</summary>
    private static readonly BigInteger Log10Of2 =
        BigInteger.Parse("4D104D427DE7FBCC47C4ACD605BE48BC", NumberStyles.HexNumber);

    /// <summary>Decimal digits in 2^bits - 1, the most digits a bits-bit magnitude can have.</summary>
    private static long DigitsOfLargestWithBits(long bits) =>
        (long)((BigInteger)bits * Log10Of2 >> 128) + 1;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(1000)]
    [InlineData(1_000_000)]
    [InlineData(1_955_153)]   // one bit short of the first overrun
    [InlineData(1_955_154)]   // the first bit length at which 1233/4096 fell one word short
    [InlineData(1_955_155)]
    [InlineData(2_000_000)]
    [InlineData(5_000_000)]
    public void EstimateOverEstimatesAtBoundaryLengths(long bits) =>
        Assert.True(
            ApInt.DecimalDigitsForBits(bits) >= DigitsOfLargestWithBits(bits),
            $"At {bits} bits the estimate reserved {ApInt.DecimalDigitsForBits(bits)} digits " +
            $"but a value that large needs {DigitsOfLargestWithBits(bits)}.");

    [Fact]
    public void EstimateOverEstimatesEverywhereUpToTheKnownThreshold()
    {
        // The overrun is a drift, so it does not show at the boundaries a spot check
        // picks -- it has to hold at every length in between too.
        for (long bits = 1; bits <= 2_000_000; bits++)
        {
            long estimated = ApInt.DecimalDigitsForBits(bits);
            long needed = DigitsOfLargestWithBits(bits);
            if (estimated < needed)
            {
                Assert.Fail($"At {bits} bits the estimate reserved {estimated} digits, " +
                            $"but {needed} are needed.");
            }
        }
    }

    [Fact]
    public void EstimateStaysCloseEnoughNotToWasteMemory()
    {
        // Over-estimating is safe, wildly over-estimating is not free: the register is
        // allocated from this, so hold it within a percent of the truth.
        for (long bits = 1000; bits <= 5_000_000; bits += 4999)
        {
            long estimated = ApInt.DecimalDigitsForBits(bits);
            long needed = DigitsOfLargestWithBits(bits);
            Assert.InRange(estimated, needed, needed + needed / 100 + 8);
        }
    }
}
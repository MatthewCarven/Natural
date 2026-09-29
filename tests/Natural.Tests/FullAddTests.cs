namespace Natural.Tests;

/// <summary>The one-word adder everything else is built on.</summary>
public class FullAddTests
{
    private static void Check(uint a, uint b, uint carryIn)
    {
        ulong expected = (ulong)a + b + carryIn;
        uint carry = carryIn;
        uint sum = Magnitude.FullAdd(a, b, ref carry);
        Assert.Equal((uint)expected, sum);
        Assert.Equal((uint)(expected >> 32), carry);
    }

    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(0u, 0u, 1u)]
    [InlineData(1u, 1u, 0u)]
    [InlineData(uint.MaxValue, 1u, 0u)]              // carry ripples through all 32 bits
    [InlineData(uint.MaxValue, 0u, 1u)]              // same, but from the carry-in
    [InlineData(uint.MaxValue, uint.MaxValue, 0u)]
    [InlineData(uint.MaxValue, uint.MaxValue, 1u)]   // the largest possible input
    [InlineData(0x80000000u, 0x80000000u, 0u)]       // carry out with a zero sum
    [InlineData(0x7FFFFFFFu, 1u, 0u)]
    [InlineData(0x55555555u, 0xAAAAAAAAu, 1u)]
    public void Edges(uint a, uint b, uint carryIn) => Check(a, b, carryIn);

    [Fact]
    public void AllPairsOfSmallWords()
    {
        for (uint a = 0; a < 256; a++)
            for (uint b = 0; b < 256; b++)
            {
                Check(a, b, 0);
                Check(a, b, 1);
                Check(~a, b, 1);   // near the top of the range too
                Check(~a, ~b, 1);
            }
    }

    [Fact]
    public void RandomWords()
    {
        var rng = new Random(1);
        for (int i = 0; i < 200_000; i++)
            Check((uint)rng.NextInt64(1L << 32), (uint)rng.NextInt64(1L << 32), (uint)rng.Next(2));
    }
}

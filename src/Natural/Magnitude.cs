namespace Natural;

/// <summary>
/// Arithmetic on raw magnitudes: arrays of 32-bit limbs, least significant limb
/// first, with no leading zero limbs (zero is the empty array).
///
/// Everything here is built out of bitwise operations -- XOR, AND, OR, NOT and
/// shifts. The CPU's own add and multiply instructions are not used for the
/// arithmetic; the adder below is the only primitive, and subtraction and
/// multiplication are both expressed in terms of it.
/// </summary>
internal static class Magnitude
{
    internal static readonly uint[] Empty = [];

    // ------------------------------------------------------------------
    // The adder
    // ------------------------------------------------------------------

    /// <summary>
    /// One 32-bit slice of a ripple-carry adder: returns (a + b + carry) mod 2^32
    /// and replaces <paramref name="carry"/> with the carry out of the top bit.
    /// Requires carry to be 0 or 1 on entry; it is 0 or 1 on exit.
    /// </summary>
    internal static uint FullAdd(uint a, uint b, ref uint carry)
    {
        uint carryOut = 0;
        uint sum = HalfAddAll(a, b, ref carryOut);
        sum = HalfAddAll(sum, carry, ref carryOut);
        carry = carryOut;
        return sum;
    }

    /// <summary>
    /// Adds two words with nothing but XOR, AND and shift. XOR is the sum with
    /// the carries left out; AND is exactly where carries are generated. Shift the
    /// carries up one place and add them in again, until there are none left.
    /// Any carry shifted off bit 31 is ORed into <paramref name="carryOut"/>.
    /// </summary>
    private static uint HalfAddAll(uint sum, uint pending, ref uint carryOut)
    {
        while (pending != 0)
        {
            uint generated = sum & pending;
            carryOut |= generated >> 31;
            sum ^= pending;
            pending = generated << 1;
        }
        return sum;
    }
    // a + b + carry < 2 * 2^32, so at most one bit can ever leave the top of the
    // word across both HalfAddAll calls -- ORing into carryOut never loses one.

    // ------------------------------------------------------------------
    // Magnitude operations
    // ------------------------------------------------------------------

    internal static uint[] Add(uint[] a, uint[] b)
    {
        if (a.Length < b.Length) (a, b) = (b, a);

        var result = new uint[a.Length + 1];
        uint carry = 0;
        int i = 0;
        for (; i < b.Length; i++)
            result[i] = FullAdd(a[i], b[i], ref carry);
        for (; i < a.Length; i++)
            result[i] = FullAdd(a[i], 0, ref carry);
        result[i] = carry;
        return Trim(result);
    }

    /// <summary>
    /// a - b for a &gt;= b, done the way hardware does it: a + ~b + 1 (two's
    /// complement), with b zero-extended to a's length and the final carry thrown away.
    /// </summary>
    internal static uint[] Subtract(uint[] a, uint[] b)
    {
        var result = new uint[a.Length];
        uint carry = 1; // the "+ 1"
        for (int i = 0; i < a.Length; i++)
        {
            uint bi = i < b.Length ? b[i] : 0;
            result[i] = FullAdd(a[i], ~bi, ref carry);
        }
        return Trim(result);
    }

    /// <summary>
    /// Binary long multiplication (shift-and-add). For every set bit k of the
    /// multiplier, the multiplicand shifted left by k is added into the product.
    /// </summary>
    internal static uint[] Multiply(uint[] a, uint[] b)
    {
        if (a.Length == 0 || b.Length == 0) return Empty;

        // Scan the shorter operand's bits; add copies of the longer one.
        if (a.Length < b.Length) (a, b) = (b, a);
        uint[] multiplicand = a, multiplier = b;

        var product = new uint[multiplicand.Length + multiplier.Length];

        // A shift by 32*j + k is a whole-limb offset j plus a bit shift k, so only
        // 32 distinct shifted copies of the multiplicand are ever needed. Build each
        // one the first time a bit at that position turns up.
        var shiftedBy = new uint[32][];

        for (int j = 0; j < multiplier.Length; j++)
        {
            uint word = multiplier[j];
            for (int k = 0; k < 32; k++)
            {
                if (((word >> k) & 1) == 0) continue;
                uint[] addend = shiftedBy[k] ??= ShiftLimbsLeft(multiplicand, k);
                AddInto(product, addend, j);
            }
        }
        return Trim(product);
    }

    /// <summary>target += addend * 2^(32*offset), in place.</summary>
    private static void AddInto(uint[] target, uint[] addend, int offset)
    {
        uint carry = 0;
        int i = 0;
        for (; i < addend.Length; i++)
            target[offset + i] = FullAdd(target[offset + i], addend[i], ref carry);
        for (int t = offset + i; carry != 0; t++)
            target[t] = FullAdd(target[t], 0, ref carry);
    }

    /// <summary>
    /// Binary long division (restoring) -- the pencil-and-paper method, in base 2.
    /// Bring the dividend's bits down one at a time, top first, into a running
    /// remainder. Whenever the remainder is at least the divisor, subtract the
    /// divisor and write a 1 in that place of the quotient; otherwise write a 0.
    /// Every quotient digit is 0 or 1, so unlike base 10 there's no guessing
    /// how many times the divisor goes in: it either fits once or not at all.
    /// </summary>
    internal static uint[] DivRem(uint[] dividend, uint[] divisor, out uint[] remainder)
    {
        if (divisor.Length == 0) throw new DivideByZeroException();
        if (Compare(dividend, divisor) < 0)
        {
            remainder = dividend;
            return Empty;
        }

        var quotient = new uint[dividend.Length];

        // Before each step the remainder is below the divisor, so after shifting a
        // bit in it's below twice the divisor: one limb more than the divisor always
        // holds it. It's worked on in place, so nothing is allocated per bit.
        var rem = new uint[divisor.Length + 1];

        for (long i = BitLength(dividend) - 1; i >= 0; i--)
        {
            uint bit = (dividend[i >> 5] >> (int)(i & 31)) & 1;
            ShiftLeftOneInPlace(rem, bit);
            if (CompareZeroExtended(rem, divisor) >= 0)
            {
                SubtractInPlace(rem, divisor);
                quotient[i >> 5] |= 1u << (int)(i & 31);
            }
        }

        remainder = Trim(rem);
        return Trim(quotient);
    }

    /// <summary>a = (a &lt;&lt; 1) | bit, in place; the bit shifted off the top is dropped.</summary>
    private static void ShiftLeftOneInPlace(uint[] a, uint bit)
    {
        for (int i = 0; i < a.Length; i++)
        {
            uint x = a[i];
            a[i] = (x << 1) | bit;
            bit = x >> 31;
        }
    }

    /// <summary>Compares a with b, where b may be shorter (and a may be untrimmed).</summary>
    private static int CompareZeroExtended(uint[] a, uint[] b)
    {
        for (int i = a.Length - 1; i >= 0; i--)
        {
            uint bi = i < b.Length ? b[i] : 0;
            if (a[i] != bi) return a[i] < bi ? -1 : 1;
        }
        return 0;
    }

    /// <summary>a -= b in place, for a &gt;= b: a + ~b + 1, the same trick as <see cref="Subtract"/>.</summary>
    private static void SubtractInPlace(uint[] a, uint[] b)
    {
        uint carry = 1;
        for (int i = 0; i < a.Length; i++)
        {
            uint bi = i < b.Length ? b[i] : 0;
            a[i] = FullAdd(a[i], ~bi, ref carry);
        }
    }

    /// <summary>
    /// Shifts by 0..31 bits into an array exactly one limb longer. Not trimmed:
    /// Multiply relies on every copy having the same length.
    /// </summary>
    private static uint[] ShiftLimbsLeft(uint[] a, int bits)
    {
        var result = new uint[a.Length + 1];
        if (bits == 0)
        {
            // C# masks shift counts to 5 bits, so x >> 32 is x, not 0. Special-case it.
            Array.Copy(a, result, a.Length);
            return result;
        }
        uint spill = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = (a[i] << bits) | spill;
            spill = a[i] >> (32 - bits);
        }
        result[a.Length] = spill;
        return result;
    }

    internal static uint[] ShiftLeft(uint[] a, long bits)
    {
        if (a.Length == 0 || bits == 0) return a;
        int wordShift = checked((int)(bits >> 5));
        int bitShift = (int)(bits & 31);

        uint[] shifted = ShiftLimbsLeft(a, bitShift);
        var result = new uint[checked(shifted.Length + wordShift)];
        Array.Copy(shifted, 0, result, wordShift, shifted.Length);
        return Trim(result);
    }

    internal static uint[] ShiftRight(uint[] a, long bits)
    {
        if (a.Length == 0 || bits == 0) return a;
        if ((bits >> 5) >= a.Length) return Empty;
        int wordShift = (int)(bits >> 5);
        int bitShift = (int)(bits & 31);

        var result = new uint[a.Length - wordShift];
        for (int i = 0; i < result.Length; i++)
        {
            uint lo = a[i + wordShift];
            uint hi = i + wordShift + 1 < a.Length ? a[i + wordShift + 1] : 0;
            result[i] = bitShift == 0 ? lo : (lo >> bitShift) | (hi << (32 - bitShift));
        }
        return Trim(result);
    }

    // ------------------------------------------------------------------
    // Comparison and housekeeping
    // ------------------------------------------------------------------

    /// <summary>Compares two trimmed magnitudes: -1, 0 or 1.</summary>
    internal static int Compare(uint[] a, uint[] b)
    {
        if (a.Length != b.Length) return a.Length < b.Length ? -1 : 1;
        for (int i = a.Length - 1; i >= 0; i--)
            if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
        return 0;
    }

    /// <summary>Number of significant bits; zero has 0.</summary>
    internal static long BitLength(uint[] a)
    {
        if (a.Length == 0) return 0;
        return 32L * (a.Length - 1) + (32 - System.Numerics.BitOperations.LeadingZeroCount(a[^1]));
    }

    /// <summary>Bit <paramref name="index"/> (0 = least significant); bits past the end are 0.</summary>
    internal static bool TestBit(uint[] a, long index)
    {
        if (index < 0 || (index >> 5) >= a.Length) return false;
        return ((a[index >> 5] >> (int)(index & 31)) & 1) != 0;
    }

    /// <summary>Whether any of bits 0 .. count-1 are set -- the "sticky" question rounding asks.</summary>
    internal static bool AnyBitsBelow(uint[] a, long count)
    {
        if (count <= 0) return false;
        long whole = count >> 5;
        for (long i = 0; i < whole && i < a.Length; i++)
            if (a[i] != 0) return true;
        int partial = (int)(count & 31);
        return partial != 0 && whole < a.Length && (a[whole] & ((1u << partial) - 1)) != 0;
    }

    /// <summary>Number of zero bits below the lowest set bit. <paramref name="a"/> must be non-zero.</summary>
    internal static long TrailingZeroCount(uint[] a)
    {
        int i = 0;
        while (a[i] == 0) i++;
        return 32L * i + System.Numerics.BitOperations.TrailingZeroCount(a[i]);
    }

    internal static uint[] Trim(uint[] a)
    {
        int n = a.Length;
        while (n > 0 && a[n - 1] == 0) n--;
        if (n == a.Length) return a;
        if (n == 0) return Empty;
        return a[..n];
    }
}

# Natural — arbitrary-precision numbers, built from bits

(Started as ADPDT, "Arbitrary Data Precision Data Type" — still the folder name.)

A hand-built arbitrary-precision integer for C# (`Natural.ApInt`), where the
arithmetic is done with **bitwise operations**, not by leaning on the CPU's add
and multiply or on `System.Numerics.BigInteger`. That's the point of the project,
so keep it that way: new arithmetic is built on `Magnitude.FullAdd` (or on other
bitwise primitives), and `BigInteger` appears only in the tests, as the oracle.

## Layout

- `src/Natural/Magnitude.cs` — magnitude arithmetic on little-endian `uint[]` limbs
  (trimmed; zero is the empty array). `FullAdd` is the one-word ripple adder
  (XOR = sum without carries, AND = carries generated, shift and repeat);
  subtract is `a + ~b + 1`; multiply is shift-and-add; divide is restoring
  binary long division (shift in a bit, subtract if it fits), in place.
- `src/Natural/ApInt.cs` — the public struct: sign + magnitude, operators,
  conversions, comparison. `default(ApInt)` is zero; there is no negative zero.
- `src/Natural/ApInt.Text.cs` — parse (decimal via `x*10 = (x<<3)+(x<<1)`, hex,
  binary) and format (decimal via double dabble, eight BCD digits per `uint`).
- `src/Natural/ApFloat.cs` — IEEE-style arbitrary-precision float: ±m × 2^e, m odd,
  `long` exponent, precision per value. All rounding goes through `RoundExact`
  (compute exact, round once). Each operation takes a precision (unbounded exponent)
  or an `IeeeFormat` (rounded once into that format's range too, via `RoundToFormat`,
  which the byte encoder also uses). The operators use the precision form.
- `src/Natural/IeeeFormat.cs` — an IEEE binary format as (exponent bits, precision).
  `IeeeFormat.Binary(k)` is binary{k} per IEEE 754-2008 §3.6. Its width formula is
  done in integers, from k^8's bit length.
- `src/Natural/ApFloat.Ieee.cs` — one encoder/decoder for any `IeeeFormat`, working on
  the bit pattern as little-endian words. It backs `ToIeeeBytes`/`FromIeeeBytes`
  (little-endian by default) and the `Half`/`float`/`double` conversions. Encoding
  rounds once, straight to the format (subnormals via `RoundExact`'s `minExp`). NaN
  always encodes as the one quiet NaN.
- `src/Natural/ApFloat.Text.cs` — decimal text both ways, `IFormattable`/`ISpanFormattable`/
  `IParsable`. The default `ToString()` is always scientific with every digit the precision
  carries (`DecimalDigitsFor(p)`, 17 for 53 bits), so a column lines up (Matthew's choice).
  Output starts from the exact value (m × 5^k / 10^k) and rounds once through `RoundExact`,
  ties to even (not .NET's away-from-zero). Parse builds digits × 10^exp exactly and rounds
  once. Powers of five are cached below 5^4096.
- `src/Natural/ApFloat.Certified.cs` — the certified interval engine (Ziv's strategy), for
  X × 10^n past the cache: bounds on 5^n by square-and-multiply with directed rounding, both
  ends rounded to the target, accepted when they agree; otherwise double the working bits,
  and hand over to the exact route once that's cheaper. Text's two cores (`FromDecimal`,
  `ScaledToInteger`) go through it, so `Parse` has no cap on decimal exponents: it refuses only
  a value whose binary exponent wouldn't fit a long. Test hooks: `ForcedRoute`, `LastRounds`,
  `LastExact` (thread-static).
- `tests/Natural.Tests` — xUnit. `Oracle.cs` converts to/from `BigInteger` through
  raw magnitude bytes, so arithmetic tests don't depend on the text code.
  `FloatOracle.cs` is ApFloat's reference: exact rationals rounded by long division
  (no code shared with `RoundExact`), an IEEE encoder, and biased random bit patterns.
  The hardware (`double`, `float`, `Half`) is the second oracle. `CertifiedTests.cs` pins
  values for exponents like 10^7 that were computed offline with exact Python integers.

## Commands

```
dotnet test Natural.slnx
dotnet run -c Release tests/bench/timing.cs            # decimal text timings (-- quick skips the exact route)
```

## Conventions

- Shift semantics match `BigInteger`: `>>` floors toward minus infinity for negatives.
- Division matches C#: `/` truncates toward zero, `%` takes the dividend's sign
  (so `-7 / 2 == -3` but `-7 >> 1 == -4`, exactly as with `long`).
- Random test operands are biased to all-ones / all-zero limbs on purpose — that's
  where carry and borrow bugs live. Keep that bias in any new randomized test.
- Before trusting a new green suite, break the code on purpose once and watch it go red.

# ADPDT — Arbitrary Data Precision Data Type

A hand-built arbitrary-precision integer for C# (`Adpdt.ApInt`), where the
arithmetic is done with **bitwise operations**, not by leaning on the CPU's add
and multiply or on `System.Numerics.BigInteger`. That's the point of the project,
so keep it that way: new arithmetic is built on `Magnitude.FullAdd` (or on other
bitwise primitives), and `BigInteger` appears only in the tests, as the oracle.

## Layout

- `src/Adpdt/Magnitude.cs` — magnitude arithmetic on little-endian `uint[]` limbs
  (trimmed; zero is the empty array). `FullAdd` is the one-word ripple adder
  (XOR = sum without carries, AND = carries generated, shift and repeat);
  subtract is `a + ~b + 1`; multiply is shift-and-add.
- `src/Adpdt/ApInt.cs` — the public struct: sign + magnitude, operators,
  conversions, comparison. `default(ApInt)` is zero; there is no negative zero.
- `src/Adpdt/ApInt.Text.cs` — parse (decimal via `x*10 = (x<<3)+(x<<1)`, hex,
  binary) and format (decimal via double dabble, eight BCD digits per `uint`).
- `tests/Adpdt.Tests` — xUnit. `Oracle.cs` converts to/from `BigInteger` through
  raw magnitude bytes, so arithmetic tests don't depend on the text code.

## Commands

```
dotnet test Adpdt.slnx
```

## Conventions

- Shift semantics match `BigInteger`: `>>` floors toward minus infinity for negatives.
- Random test operands are biased to all-ones / all-zero limbs on purpose — that's
  where carry and borrow bugs live. Keep that bias in any new randomized test.
- Before trusting a new green suite, break the code on purpose once and watch it go red.

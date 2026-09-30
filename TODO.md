# TODO

## ApFloat — the plan (agreed 2026-09-27; core written 2026-09-30)
Arbitrary precision in the arithmetic, IEEE 754 in behaviour and in the byte format.

**Built so far** (`ApFloat.cs`, `ApFloat.Ieee.cs`, `ApFloat.Text.cs`, `IeeeFormat.cs`):
±m × 2^e with m odd and a signed `long` exponent; precision per value (default 256,
Matthew's choice, 2026-09-30);
±0, ±∞, NaN; `RoundExact`, the single rounding point (4 IEEE modes, sticky bit,
`minExp` floor for subnormals); correctly rounded `+ − × ÷` with IEEE special
cases; the big-exponent-gap shortcut in `Add`; IEEE comparisons; integer conversions;
IEEE bytes in any `binary{k}` or custom format, either byte order; `Half`/`float`/`double`
both ways; decimal, binary and hex text both ways, correctly rounded in every mode.

### Arithmetic in a format (Matthew's call, 2026-09-30: done the same day)
- [x] `Add`/`Subtract`/`Multiply`/`Divide(a, b, IeeeFormat format, mode)` round once
      straight into the format, subnormals and overflow included; `x.WithFormat(format,
      mode)` does the same for a single value. `ApFloat.Multiply(x, y, IeeeFormat.Binary64)`
      is the hardware's `x * y`, bit for bit, everywhere. This answers session 1's question:
      the 53-bit *operators* (unbounded exponent) still round twice among the subnormals
      (about 1%), and that stays documented on the type. The session 1 probe re-run gives
      0 of 1,188,696 differing, against 12,619 for the operators.

### Session 1 — prove the core, then Half / float / double (done 2026-09-30)
- [x] **Reference oracle in the tests** (`FloatOracle.cs`): exact rationals rounded by
      `BigInteger` long division, remainder against half the divisor; no code shared
      with `RoundExact`. Constructor, `WithPrecision`, `+ − × ÷`: random limb-biased
      operands, precision 1..200, all four modes, 3,000 rounds each.
- [x] **Special-case table**: 17 specials (NaN, ±∞, ±0, subnormals, ±max, ordinary
      values), every pair, all four ops and all comparisons against `double` bit for
      bit. Signed-zero rules in every mode. NaN/∞ rules.
- [x] **Gap shortcut**: b's leading bit within ±4 places of the threshold, either
      sign, against exact addition. (The threshold has one place of slack: the exact
      condition is `topB < min(a._exp, topA − p − 1)`, so mutations that only eat the
      slack stay green, correctly.)
- [x] **IEEE conversions for widths ≤ 64** (`ApFloat.Ieee.cs`): `FromIeeeBits` /
      `ToIeeeBits` (internal until session 2 designs the public byte API). Implicit
      `Half`/`float`/`double` in (exact, keeping 11/24/53 bits); explicit out, plus
      `ToDouble(mode)`, `ToSingle(mode)`, `ToHalf(mode)`. Canonical NaN on encode.
      Also added exact `uint`/`ulong` in and `long`/`ulong` out: without them C# would
      silently route a `ulong` through the new `float` conversion.
- [x] **Hardware as oracle**: `+ − × ÷` bit-identical to `double` and `float` (20,000
      pairs each, crowded round subnormal/overflow results), both rounded once from
      exact and through the 53/24-bit operators. Every `Half` pattern round-trips; tiny
      formats (w 2–5, p 2–6) exhaustively in every mode; encoder against the oracle
      for 8 formats × 4 modes; narrowing matches the hardware's `(float)`/`(Half)`.
- [x] Mutation check: 12 real mutations, all red. Details in WORKLOG.

### Session 2 — IEEE `binary{k}`, any width (done 2026-09-30)
- [x] `IeeeFormat` (exponent bits, precision), with `IeeeFormat.Binary(k)` for k = 16,
      32, 64 and multiples of 32 from 128 up to 480,768 (the widest whose exponent fits a
      `long`: w = 62). The width formula is `(bitLength(k^8) >> 1) − 13`, with k^8 an
      `ApInt`, so there's no floating log. Custom formats work too (bfloat16 is (8, 8)).
- [x] `ToIeeeBytes(format, mode, bigEndian)` / `FromIeeeBytes(bytes, format, bigEndian)`:
      little-endian by default. One encoder/decoder over words of any width now backs
      `Half`/`float`/`double` too.
- [x] **NaN is canonical**: encode writes the one quiet NaN; decode reads any NaN, any
      payload or sign, as NaN.
- [x] Tests: binary16/32/64 bytes identical to `BitConverter` both ways and in both byte
      orders (every `Half`, 50k each of `float`/`double`). binary128 vectors (1, −2, π,
      1/3, max, min normal/subnormal, −0, ±∞, NaN) and the same for binary256, with π and
      1/3 from an independent Python encoder. π brackets correctly in every mode.
      Round trips at 113/160/256/512/1024 bits, bfloat16, an 80-bit and an 8-bit format.
      The encoder against the oracle for 5 formats × 4 modes. bfloat16 toward zero is
      exactly a float's top two bytes.
- [x] Mutation check: 13 of 13 red. Details in WORKLOG.

### Session 3 — decimal text, built for eyeballing (done 2026-09-30)
Matthew (2026-09-30): zeros aren't noise, they're alignment. Read a column of numbers
like a bar graph, the way Chess Bruteforcer's `{n,10:N0}` columns work: the width of
each number shows its size without doing any maths, and the patterns jump out.
- [x] **Default `ToString()`**: every digit the precision carries, zeros kept, **always
      scientific** (Matthew's pick, 2026-09-30): `1.0000000000000001E-001` for 0.1 at 53
      bits. Same precision, same width, so `{x,24}` columns line up on the point and the E.
      `DecimalDigitsFor(p)` = floor(p·log10 2) + 2, from a 128-bit fixed-point log10 2
      held as an `ApInt`: 17 for 53 bits, 79 for the 256 default.
- [x] **Ties go to even** (Matthew's pick): 0.125 F2 is `0.12`, where .NET says `0.13`.
      Every format takes a `RoundingMode` too: `ToString(format, provider, mode)`.
- [x] `IFormattable` / `ISpanFormattable`: `E<n>` (three-digit exponent, the same as .NET's
      E), `F<n>`, `G<n>`, `R`/`G` (shortest round trip, plain from 1E-005 up to the digit
      count, otherwise scientific), `B<n>` (binary point, "." always), `A`/`X<n>` (C's `%a`,
      "." always), and custom `0`/`#`/`.`/`,` patterns with literal text before or after.
      Sections, `%` and exponents in custom patterns throw rather than print wrongly.
- [x] Output rounds from the exact value: |v| × 10^t = m × 5^t × 2^(e+t), through
      `RoundExact` with `minExp: 0`.
- [x] `Parse` / `TryParse` / `IParsable`: decimal (culture separator, `_` between digits),
      hex floats, `0b` binary with a `p` exponent, NaN/Infinity/inf/∞. The result is
      digits × 10^exp as an exact rational, then one `DivRem` + sticky + `RoundExact`,
      at a given precision and mode.
- [x] 150+ tests against a `BigInteger` oracle and against .NET's own formatting and
      parsing. Mutation check in WORKLOG.

### Open, from session 3
- [ ] **Parsing a huge decimal exponent is slow, not refused.** "1e1000000" builds
      5^1000000 (2.3M bits) with the bitwise multiply, which takes minutes. Options:
      leave it (ApInt is the same with million-digit input), or cap |exponent| and throw
      `OverflowException` past the cap. Formatting a value with a huge binary exponent
      costs the same way.
- [ ] **Found in .NET 10.0.12, not ours:** `double.ToString("R")` (and plain `ToString()`)
      for 2^-25 and 2^-958 gives 16 digits that don't read back. They parse to the double
      below, because a power of two's lower neighbour is only half as far away. Python's
      repr and ApFloat both give 17. Pinned in `ShortestMatchesDotNetR`. Reporting it
      upstream (dotnet/runtime) would be Matthew's call.

### Later
- [ ] `FusedMultiplyAdd` (exact a×b + c, round once — nearly free here), `Sqrt`
      (needs `ApInt` integer square root), `Floor`/`Ceiling`/`Truncate`/`Round`.
- [ ] Transcendentals (exp, log, sin, π) — need error bounds (Ziv's retry strategy)
      to stay correctly rounded. A project of its own.
- [ ] Generic math (`INumber<ApFloat>`, `IFloatingPointIeee754<ApFloat>`).

## Done
- [x] Division — `DivRem`, `/`, `%`, truncating like C# (2026-09-27). Repeated
      `% 10` is cross-checked against double dabble in the tests.

## Later
- [ ] Bitwise operators on `ApInt` (`&`, `|`, `^`, `~`) — two's-complement
      semantics for negatives, to match `BigInteger`.
- [ ] `Pow`, `ModPow`, `Gcd`, integer `Sqrt`.
- [ ] Beyond integers — fixed-point or rational/floating built on top of `ApInt`
      (the "precision" half of the name).
- [ ] Speed, if it ever matters: Karatsuba multiply; a bit-parallel adder that
      doesn't loop per carry. Keep the bitwise-only rule.
- [ ] Generic math interfaces (`INumber<ApInt>` etc.) if it's to be used as a drop-in.

# TODO

## ApFloat — the plan (agreed 2026-09-27; core written 2026-09-30)
Arbitrary precision in the arithmetic, IEEE 754 in behaviour and in the byte format.

**Built so far** (`src/Natural/ApFloat.cs`, `ApFloat.Ieee.cs`; 36 tests):
±m × 2^e with m odd and a signed `long` exponent; precision per value (default 256,
Matthew's choice, 2026-09-30);
±0, ±∞, NaN; `RoundExact`, the single rounding point (4 IEEE modes, sticky bit,
`minExp` floor for subnormals); correctly rounded `+ − × ÷` with IEEE special
cases; the big-exponent-gap shortcut in `Add`; IEEE comparisons; integer conversions;
hex-float `ToString` (C's `%a`); IEEE encode/decode for any format ≤ 64 bits, and
`Half`/`float`/`double` both ways.

### Open question for Matthew (from session 1)
- [ ] **Double rounding at 53 bits in the subnormal range.** ApFloat's exponent is
      unbounded, so `(double)(x * y)` for two converted doubles rounds twice when the
      product lands where double would go subnormal: once to 53 bits, then again to the
      subnormal grid. The hardware rounds once. Measured: about 1% of such products and
      quotients differ in the last place (10,454 of 987,109 products). Rounding the exact
      result once is always right (`Multiply(x, y, 106).ToDouble()`), and the tests do
      that. Options: leave it, since it's documented on the type and inherent to an
      unbounded exponent; or add arithmetic *in a format*, e.g. `Multiply(x, y, Binary64)`,
      passing the format's `minExp` down to `RoundExact`. That is a small change, since
      the floor already exists.

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

### Session 2 — IEEE `binary{k}`, any width
- [ ] Generalise the encoder to bytes: k = 16, 32, 64, 128, then any multiple of 32
      from 128 (IEEE 754-2008 §3.6): w = round(4·log2 k) − 13, p = k − w. Do the
      round() in integers: w + 13 = t where 2^(2t−1) ≤ k^8 < 2^(2t+1) — no floating
      log anywhere.
- [ ] Byte order: little-endian by default (as `BitConverter` on x86), big-endian on request.
- [ ] **NaN is canonical** (Matthew, 2026-09-30): encode always writes the one quiet NaN
      (sign 0, exponent all ones, top fraction bit 1, the rest 0). Decode reads any NaN
      (quiet or signalling, any payload) as plain NaN. Note .NET's `double.NaN` is
      `0xFFF8…` (sign bit set), so the tests check `IsNaN`, not the bit pattern.
- [ ] Tests: k = 16/32/64 bit-identical to `Half`/`float`/`double`; binary128 known
      vectors (1.0 = `3FFF 0000…`, π = `4000 921F B544 42D1 8469 898C C517 01B8`);
      binary256 1.0 = `3FFF F000…`; round trips at k = 160, 256, 512, 1024.

### Session 3 — decimal text, built for eyeballing (all three agreed 2026-09-30)
Matthew (2026-09-30): zeros aren't noise, they're alignment. Read a column of numbers
like a bar graph, the way Chess Bruteforcer's `{n,10:N0}` columns work: the width of
each number shows its size without doing any maths, and the patterns jump out.
- [ ] **`IFormattable` / `ISpanFormattable`**, so `$"{x,12:F4}"` aligns like any .NET number.
      - `F<n>`: fixed decimals with trailing zeros kept, so the points line up.
      - `E<n>`: scientific with an exponent at least 3 digits wide (`3.3333E-001`),
        so the exponent column reads as a log-scale bar.
      - Custom `0` / `#` patterns (`00000.0000`) for zero-padding on both sides.
      - `R`: shortest round-trip. `X` / `A`: hex float (what `ToString()` does today).
- [ ] **Precision-width default (agreed)**: `ToString()` prints exactly the significant
      digits the precision carries, zeros kept: ceil(1 + p·log10 2), so 17 for 53 bits and
      62 for 200. Same precision, same width; more precision, visibly longer. It also
      shows the truth (0.1 at 53 bits is `0.10000000000000001`). Shortest round-trip
      stays available as `R`.
- [ ] **Binary-point view** (`B<n>`, matching `ApInt.ToString("B")`): significand bits lined up on the binary point, so
      the leading 1's position (the power of two) is the bar.
- [ ] Exact decimal is always finite (m × 2^−e = m × 5^e / 10^e), so every format can
      round from the exact digits, correctly, in any mode.
- [ ] `Parse`, correctly rounded: digits × 10^exp as an exact rational, then one
      `DivRem` + sticky + `RoundExact`; every piece already exists. Hex floats too.

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

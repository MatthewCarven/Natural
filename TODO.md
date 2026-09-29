# TODO

## ApFloat — the plan (agreed 2026-09-27; core written 2026-09-30)
Arbitrary precision in the arithmetic, IEEE 754 in behaviour and in the byte format.

**Built so far** (`src/Natural/ApFloat.cs`, smoke-tested only, no unit tests yet):
±m × 2^e with m odd and a signed `long` exponent; precision per value (default 256,
Matthew's choice, 2026-09-30);
±0, ±∞, NaN; `RoundExact`, the single rounding point (4 IEEE modes, sticky bit,
`minExp` floor ready for subnormals); correctly rounded `+ − × ÷` with IEEE special
cases; the big-exponent-gap shortcut in `Add`; IEEE comparisons; integer conversions;
hex-float `ToString` (C's `%a`). Smoke check: 12/12 against known `%a` values, the
200-bit one confirmed with Python `fractions`.

### Session 1 — prove the core, then Half / float / double
- [ ] **Reference oracle in the tests**: an independent rounding of an exact rational
      (`BigInteger` num/den → p bits, any mode), sharing no code with `RoundExact`.
      Random operands (limb-biased as usual), random precision 1..200, all four modes,
      all four ops, plus the constructor and `WithPrecision`.
- [ ] **Special-case table**: IEEE 754 §6–7 (0×∞, ∞−∞, 0/0 → NaN; x/0 → ±∞; signed
      zeros: −0 + −0, x − x, rounding down gives −0; NaN compares false).
- [ ] **Gap shortcut**: same results as brute-force exact addition for gaps just
      above and below the threshold (small enough that brute force is cheap).
- [ ] **IEEE conversions for widths ≤ 64**: one encoder/decoder parameterised by
      exponent bits w and precision p — bias = `((1 << w) − 1) >> 1` (Matthew's
      max / 2), subnormals via `minExp`, overflow per mode (∞ or the largest finite).
      Implicit `Half`/`float`/`double` → `ApFloat` (exact), explicit back (rounded),
      plus `ToDouble(mode)` etc. A converted value keeps its source's precision
      (11 / 24 / 53), not the 256 default. Mixed with a default-precision value, the
      max rule takes it to 256 anyway, and two converted doubles still compute at 53,
      which is what makes `double` usable as the oracle.
- [ ] **Hardware as oracle**: at 53 bits, `+ − × ÷` bit-identical to `double`; at 24
      to `float`. Products that land subnormal or overflow, compared with the
      hardware's own result (it rounds once, straight to the subnormal). Round trips
      of every bit-pattern class, all three types.
- [ ] Mutation check (e.g. drop `|| rest` from ties-to-even), then commit.

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

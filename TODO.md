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
- [x] **Parsing a huge decimal exponent: capped** (Matthew, 2026-09-30). `MaxDecimalExponent`
      = 100,000: past it, `Parse` throws `OverflowException` before any arithmetic, and
      `TryParse` returns false. Measured: 1e10000 parses in 0.1 s and 1e100000 in 5 s; the
      cost grows with the square of the exponent. Zeros, and hex/binary text, have no limit.
      **Not capped: formatting.** `ToString()` of 1e100000 takes 15 s by the same route.
- [ ] **A reference mode with no cap**: agreed 2026-09-30, planned below as sessions 4–6.

## Reference mode: the plan (agreed with Matthew 2026-09-30)

**Decisions (Matthew, 2026-09-30):**
1. The engine is **certified intervals** (Ziv's strategy): compute the power of ten well
   enough, *provably*, and fall back to exact only when the rounding can't be settled.
2. Job budgets: **both** wall-clock time and work units, with work units underneath
   (deterministic; this machine runs at two speeds).
3. Jobs live **in memory**. The caller holds the job, like a stream or a message queue that
   mustn't drop messages. Disk-backed resubmission (checkpoints) is a possible later add-on
   that he floated; not agreed yet.
4. The default `Parse` **keeps the cap** (`MaxDecimalExponent` = 100,000) as the sensible
   default. The reference mode is **opt-in** and uncapped.

Order: session 4 (engine), then 5 (jobs), then 6 (optional extras).

### Session 4 — the certified interval engine (brief for a fresh chat)
Goal: parsing "1e10000000" and printing 1e100000 take milliseconds, not minutes, with the
answer proven correctly rounded. Exact stays as the fallback.

Baseline to beat (Release, this machine, 2026-09-30, at 53 bits):
| input      | Parse   | ToString() |
|------------|---------|------------|
| 1e10000    | 0.095 s | 0.135 s    |
| 1e30000    | 0.48 s  | 1.46 s     |
| 1e100000   | 5.0 s   | 15.0 s     |

The idea: 10^n = 5^n × 2^n, and only 5^n is expensive. So compute bounds lo <= 5^n <= hi
by square-and-multiply with `Multiply(..., workingBits, TowardNegative)` for lo and
`TowardPositive` for hi. Directed rounding makes the bounds rigorous, with no error
analysis. Build the value's interval from them (rounded outward), then round both ends
to the target (precision and mode, or an IeeeFormat). If they agree, it's certified.
If not, double `workingBits` and go again. Once `workingBits` reaches 5^|n|'s bit length
the bounds are exact, which is the fallback. So it always ends, and always correctly.

Where it plugs in (all in `src/Natural/ApFloat.Text.cs`):
- `FromDecimal(negative, q, s, precision, mode)`: the parse core, also used by `R`'s
  read-back check.
- `ScaledToInteger(t, mode)`: the output core, |v| × 10^t rounded to an integer. There
  the certified question is "do both ends round to the same integer?"
- `DecimalExponent()` calls `ScaledToInteger` with `TowardZero`.
- `PowerOfFive(n)`: cached below 5^4096. **Below the cache, keep the exact route** (it's
  instant); use intervals only above it.
- The cap: default `Parse` checks it before any work, as now. The reference mode (session
  5's opt-in entry point) skips it.

Things to get right:
- Signs: bound magnitudes, then round the signed endpoints in the caller's mode.
  `TowardPositive` on a negative value rounds its magnitude down.
- Ties and exact values never certify from an interval (the ends straddle or touch a
  midpoint), so they reach the fallback. Small |n| is exact anyway. For large |n| a tie
  needs D to be a multiple of 5^|n|, a digit string of about 0.7·|n| digits, so the
  fallback's cost is proportional to the input.
- Start at p + 64 working bits; cap the number of doublings as a guard against an
  infinite loop, which the tests should never hit.
- Keep the one-rounding-point rule: the final rounding goes through `RoundExact` /
  `RoundToFormat`.

Tests:
- Everything existing stays green (it mostly runs below the cache, on the exact route).
- For |n| between 4096 and about 30,000, sampled: the interval route equals the exact route.
  Add an internal switch to force either one.
- Huge n (1e10000000, 1e-10000000, a few digit strings): reference results computed
  **offline in Python** (integers only; 10**n for n = 10^7 takes seconds there) and pinned
  as hex floats in the tests, since the exact route can't reach them.
- Hard cases: take a p-bit midpoint, write out its exact decimal expansion (it's finite),
  cut it off after many digits. That decimal sits within a hair of the midpoint and forces
  extra rounds. Check it against the exact route, and count rounds through an internal hook.
- Timing: the table above, re-measured afterwards (not asserted in tests: two speeds).
- Mutation check with a subprocess timeout under the tool's cap (see the notebook): swap
  lo/hi rounding; drop the outward rounding of D × bound; certify from one end only; skip
  the doubling (should hang, then be caught by the timeout).

### Session 5 — resumable jobs (the reference mode)
- Sketch: `var job = ApFloat.StartParse(text, precision, mode, provider);`
  `while (!job.Complete) job.Continue(TimeSpan.FromSeconds(1));` or
  `job.Continue(workUnits: 1_000_000)`; then `job.Result`. Also `job.Progress` (0..1),
  and the round count and final interval as a certificate. Cancelling is just not calling
  `Continue` (maybe a `CancellationToken` too).
- A work unit is one inner-loop step (a shift-and-add iteration, a division step), the same
  on any machine. Time budgets are layered on top.
- Resumable pieces: the engine's rounds, plus the exact fallback's multiply and divide
  loops. Each is a bit index plus an array or two, so each can stop anywhere.
- In memory only (decision 3). Opt-in reference entry point, uncapped (decision 4):
  e.g. `ApFloat.ParseReference(...)` = StartParse, then run to completion.
- `StartFormat` for printing huge values, and later for million-digit constants.

### Session 6 — optional extras (pick any)
- IEEE status flags: inexact, overflow, underflow, invalid, divide-by-zero.
- `Parse` into an `IeeeFormat`: a bounded range means bounded cost, so 1e400 → ∞ in binary64 at once.
- Karatsuba multiplication and a dedicated squaring, still bitwise, for the exact routes.
- Streaming digits for huge output; disk-backed checkpoints (decision 3, if wanted).

## ApFloat — later
- [ ] `FusedMultiplyAdd` (exact a×b + c, round once — nearly free here), `Sqrt`
      (needs `ApInt` integer square root), `Floor`/`Ceiling`/`Truncate`/`Round`.
- [ ] Transcendentals (exp, log, sin, π) — need error bounds (Ziv's retry strategy)
      to stay correctly rounded. A project of its own; session 4's engine is the start of it.
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

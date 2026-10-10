# TODO

## Order of work (agreed with Matthew 2026-09-30)
1. ~~**Session 4**: the certified interval engine.~~ Done 2026-09-30.
2. **Session 5**: resumable jobs, built on 4. ~~5a (`Natural.Jobs`, `StartParse`)~~ done
   2026-10-09; ~~5b (`StartFormat`, double dabble as steps)~~ done 2026-10-10. 5c (the exact
   route as steps) is left, and measured cheap: parked (Matthew went on to session 6,
   2026-10-10).
3. ~~**The IEEE operations ApFloat still lacks**: FMA, Floor/Ceiling/Truncate/Round, IEEE
   remainder, and `ApInt` integer square root, then `ApFloat.Sqrt`. The **IEEE status
   flags** go in first, in the same session, so each new operation reports them from day one.~~
   Done: 6a (the flags, `IeeeContext`) 2026-10-10; 6b (FMA, rounding to integral, remainder)
   and 6c (square roots) 2026-10-11.
4. **Parse into an `IeeeFormat`**: small, and for very wide formats it uses 4's engine.
   **Next: session 7**; brief below, after session 6.
5. **`ApInt` bitwise operators** (`&`, `|`, `^`, `~`), then `Pow`, `ModPow`, `Gcd`.
6. **Generic math**: `INumber<ApInt>` / `IBinaryInteger<ApInt>` (needs 5), `INumber<ApFloat>`
   (needs 3).
7. **Speed**: Karatsuba plus a dedicated squaring, and a bit-parallel adder, all still
   bitwise. Measure first: after 4, the exact route mostly runs only on ties.
8. **Transcendentals** (exp, log, sin, π), streaming digits, million-digit constants.
   A project of its own; needs 4's engine and likes 7's speed.
9. **`IFloatingPointIeee754<ApFloat>`**: last, because the interface requires exp, log and trig.

**Matthew's call:** report .NET 10's `double.ToString("R")` bug upstream (dotnet/runtime)?
2^-25 and 2^-958 each print 16 digits that parse back to the double below (runtime 10.0.12;
Python's repr is right). Repro: `double.Parse(Math.Pow(2, -25).ToString("R")) !=
Math.Pow(2, -25)`. Pinned in `ShortestMatchesDotNetR`; details in WORKLOG (session 3).

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
      **The cap was dropped the same day, after session 4** (Matthew's call): see below.
- [ ] **A reference mode**: agreed 2026-09-30. Session 4 (the engine) is done, and the default
      `Parse` has no cap now. Session 5 (resumable jobs) is next.

## Reference mode: the plan (agreed with Matthew 2026-09-30)

**Decisions (Matthew, 2026-09-30):**
1. The engine is **certified intervals** (Ziv's strategy): compute the power of ten well
   enough, *provably*, and fall back to exact only when the rounding can't be settled.
2. Job budgets: **both** wall-clock time and work units, with work units underneath
   (deterministic; this machine runs at two speeds).
3. Jobs live **in memory**. The caller holds the job, like a stream or a message queue that
   mustn't drop messages. Disk-backed resubmission (checkpoints) is a possible later add-on
   that he floated; not agreed yet.
4. ~~The default `Parse` keeps the cap (`MaxDecimalExponent` = 100,000); the reference mode is
   opt-in and uncapped.~~ **Changed after session 4 (Matthew, 2026-09-30): the cap is gone.**
   `Parse` takes any exponent whose value's binary exponent fits a long, so the reference
   mode is about stepping and pausing long work, not unlocking exponents.

Order: session 4 (engine), then 5 (jobs). Session 6's extras were split up on
2026-09-30 and now sit in "Order of work" at the top.

### Session 4 — the certified interval engine (done 2026-09-30)
- [x] `ApFloat.Certified.cs`: bounds on 5^n by square-and-multiply with directed rounding,
      X × or ÷ the bounds rounded outward, both ends rounded to the target, accepted when
      they agree. Otherwise W doubles, and the exact route finishes once W reaches an eighth
      of 5^n's bit length (cheaper by then). Below the cache (|n| < 4096) the exact route runs,
      as before. `FromDecimal` and `ScaledToInteger` go through it.
- [x] The first round works at p + 64 + bitlength(n) bits: squaring doubles a relative
      error, so the bounds are about n·2^(2−W) apart (the brief said log n; a test caught it).
- [x] Parse of 1e100000 went from 5.1 s to 0.22 ms and its `ToString()` from 15.2 s to
      0.47 ms. 1e10000000 parses in 0.36 ms and 1e(10^18) in 1.8 ms. Table in WORKLOG.
- [x] 151 new tests (333 → 484), with values for 10^±(10^7) from exact Python integers
      (`tests/reference/certified_reference.py`). Mutation check: 13 mutations, 12 red,
      1 equivalent. Details in WORKLOG.

### Open, from session 4
- [x] **The cap is dropped** (Matthew, 2026-09-30, asked with the timings). It was there because
      1e100000 took 5 s and grew with the square of the exponent. Now a short input takes
      milliseconds whatever its exponent. `MaxDecimalExponent` is gone, and `Parse` refuses only
      a value whose binary exponent wouldn't fit a long (decimal exponents past about
      ±2.7 × 10^18). What's left that's slow is long input, and that's session 5's.
- [ ] Long digit strings both ways are quadratic in their length: ApInt's decimal digit loop
      when parsing (30,000 digits: 0.75 s), and double dabble when printing (`F0` of 1e30000:
      0.22 s). Session 5's jobs make them resumable (parsing: `StartParse`, 2026-10-09;
      printing: `StartFormat`, 2026-10-10); order item 7 (speed) would make them faster.

### Session 5 — resumable jobs
Goal: an opt-in reference mode whose work the caller holds, steps, pauses and resumes.
`StartParse` / `StartFormat` return a job; `Continue(budget)` does up to that much work; then
`IsComplete`, `Result`, `Progress` (0..1), and a certificate. The default `Parse` has no cap
any more (decision 4, changed), so jobs are for long work, not for big exponents. The job
machinery is a general piece, not tied to parsing (Matthew's reuse aim below).

**Asked and answered (Matthew, 2026-10-09), both as recommended:**
1. **A job you step.** `var job = ApFloat.StartParse(text, precision: 256);` then
   `while (!job.Continue(TimeSpan.FromMilliseconds(50))) …` (or `Continue(units)`), with
   `job.Progress`, `job.WorkDone`, `job.Result` (the same bits `Parse` gives) and
   `job.Certificate` (rounds, working bits, exact or not, Low ≤ exact value ≤ High). No
   `RunAsync` yet; it can go on top later without changing this.
2. **`Natural.Jobs`, in this project**: one file, its own namespace, nothing from the numbers,
   so it can move to its own project later with no change to callers.

#### 5a — done 2026-10-09
- [x] `Natural.Jobs` (`src/Natural/Jobs/Job.cs`, nothing from the numbers): `Job.Start(body)`,
      where the body is an iterator that yield-returns the units it just did and sets
      `context.Result` (and `ExpectedWork`). `Job<T>`: `Continue(units)`, `Continue(TimeSpan)`,
      `RunToEnd`, `Result`, `WorkDone`, `Steps`, `Progress`, `IsComplete`, `Dispose`.
      `Job<T, TCertificate>` adds `Certificate`.
- [x] `ApFloat.StartParse(text, precision, mode, provider)` → `Job<ApFloat, DecimalCertificate>`.
      `Parse` runs the same steps to the end. Units are limb steps: the digit loop counts them
      (4,096 a step, about 0.065 ms); an interval round or the exact route counts an estimate
      from its sizes.
- [x] 506 → 555 tests; 23 mutations, all red (two only after the tests were tightened).
      Short parses pay about 0.18 µs for the steps. Details in WORKLOG.

#### 5b — done 2026-10-10
- [x] `x.StartFormat(format, provider, mode)` → `Job<string, DecimalCertificate>`, giving exactly
      `x.ToString(format, provider, mode)`. `ToString` drains the same steps (`DecimalFormat` in
      `ApFloat.Text.cs`). The format and the output's length are checked at once: F or a custom
      pattern with more digits before the point than a string holds throws `OverflowException`
      at the start (`ToString` too, with a clearer message than before).
- [x] Double dabble as steps (`ApInt.DecimalStringSteps`, `2·used + 1` limb steps a bit, 4,096 a
      step). Its loops live in a plain method (`Dabble`): inside the iterator they ran on fields,
      and F0 of 1e30000 took 300 ms instead of 200. Now the same as before.
- [x] `DecimalCertificate` gained `Scale`: for a format, the power of ten the digits came from
      (Low ≤ x × 10^Scale ≤ High); 0 for a parse.
- [x] 555 → 581 tests; 17 mutations, 16 red, 1 explained (which scaling sets the test hooks
      after R). Short formats cost the same or up to 0.15 µs more. Details in WORKLOG.

#### 5c — the exact route as steps (small; rarely needed)
`Magnitude.Multiply`, `DivRem` and `PowerOfFive` past the cache as iterators, yielding between
whole row additions or whole quotient bits (so no carry crosses a yield), replacing the exact
route's one estimated step with counted ones. It runs for ties and exact values, and below the
engine's crossover (10^±4096 at up to ~1,100 bits of precision). Measured 2026-10-09, it's cheap
next to the digit loop: a 30,000-digit parse takes 0.86 s at e0, 0.91 s at e-3000 (a division
keeping all ~93,000 quotient bits) and 0.91 s at e3000. So it's worth doing only for
completeness. Mutations: a row added twice or skipped on resume; a quotient bit lost across a
yield.

Things to get right (all three):
- The thread-static test hooks (`LastRounds`, `LastExact`) mustn't carry a job's state: a job
  may be continued on another thread. A job reads `ForcedRoute` when it starts, on the caller's
  thread, and keeps its rounds in its certificate (5a does this).
- A job's result must be bit-identical to the one-shot result. There's one rounding point
  (`RoundExact` / `RoundToFormat`), and the job reaches it by the same route.
- `Continue(n)` may overshoot n by at most one step. Document the step size.
- Memory: a job holds its arrays for as long as the caller holds the job (decision 3: in
  memory; disk checkpoints are not agreed).
- Not built, on purpose: `RunAsync` (it can go on top later) and a `CancellationToken`
  overload (stopping is not calling Continue again).

Matthew, 2026-09-30, on reuse: he expects this structure to turn up in other projects "where
we need bigger numbers to put a cap on possibilities" and "must be able to iterate across
them in a controlled and safe way". So the budget, the work-unit count,
`Continue`/`Complete`/`Progress` and the certificate belong in a general piece that a parse or
a format plugs into.

### Session 6 — IEEE status flags, then the missing operations
Order of work item 3. Big, so split as **6a** (flags), **6b** (FMA, round to integral,
remainder) and **6c** (integer square root, then `ApFloat.Sqrt`). The flags go first so every new
operation reports them from day one (Matthew's order, 2026-09-30).

#### 6a — done 2026-10-10
- [x] `IeeeFlags` and `IeeeContext` (`src/Natural/IeeeContext.cs`): `Add`, `Subtract`,
      `Multiply`, `Divide` and `Round(x)` (convertFormat), into a format or a precision, in the
      context's `Mode`, raising sticky `Flags`. The static methods are unchanged.
- [x] Underflow is tiny and inexact, tiny judged after rounding (x86's choice); a value just
      under 2^emin that rounds up to it at the precision doesn't underflow. Pinned by a test.
- [x] 581 → 592 tests (every pair of three 6-bit formats, NaN and ∞ included, in every mode,
      against flags predicted from the exact rational); 15 mutations, all red.

#### 6b — done 2026-10-11
- [x] `ApFloat.Operations.cs`, with .NET's names: `FusedMultiplyAdd` (three overloads: the
      operands' largest precision, a precision, a format), `Floor`, `Ceiling`, `Truncate`,
      `Round`, `Round(x, MidpointRounding)` (exact, at x's precision), `Ieee754Remainder`
      (exact, at the larger precision, any exponent gap). On the context: `FusedMultiplyAdd`,
      `RoundToIntegral` (roundToIntegralExact: the context's mode, Inexact), `Ieee754Remainder`.
- [x] The context's `Round(x)` (convertFormat) is now `Convert(x)`, so `Round` means one thing.
- [x] 592 → 615 tests: the hardware bit for bit (FMA in binary64 and binary32, all five
      `MidpointRounding`s, `double.Ieee754Remainder`), exact rationals at any precision and
      mode (flags included, through the shared `FloatOracle.RoundingFlags`), remainders across
      2^(10^12). 19 mutations: see WORKLOG.

#### 6c — done 2026-10-11
- [x] `ApInt.Sqrt` and `ApInt.SqrtRem` (`Magnitude.Sqrt`: digit by digit, two bits at a time, in
      place). `ApFloat.Sqrt` at x's precision, a precision, or a format, and `ieee.Sqrt` on the
      context. A significand longer than the root needs is shifted down into the sticky bit, so
      the cost follows the precision asked for.
- [x] 615 → 630 tests: `Math.Sqrt`/`MathF.Sqrt` bit for bit, every `Half`, the `SqrtOf` stand-in
      rational in every mode and format (flags included), a squaring check that takes no root,
      every value of three 6-bit formats. 21 mutations, 20 red, one equivalent (a root of p + 1
      bits is enough): see WORKLOG.

The brief, kept for reference:
Goal: `ApInt.Sqrt` (the integer square root, floor) and `ApFloat.Sqrt`, correctly rounded, on
the static API (precision, format, and operand-precision overloads, like FMA) and on the
context (`ieee.Sqrt(x)`, Invalid for a negative, nothing for ±0, ∞ and NaN). Plumb it the 6b
way: one flagged core, `internal static ApFloat Sqrt(ApFloat x, Target target, RoundingMode
mode, ref IeeeFlags flags)`, rounding through `target.Round(..., ref flags)`.
- `ApInt.Sqrt`: bitwise, two bits of the radicand at a time, the "digit by digit" method:
  bring down two bits, try (4·root + 1), subtract if it fits, as restoring `DivRem` does one bit
  at a time. No CPU multiply (the project's rule). Negative throws `ArithmeticException`.
- `ApFloat.Sqrt`: make the exponent even, scale the significand so the integer root has at
  least p + 2 bits, take `ApInt.Sqrt`, and a non-zero remainder is the sticky bit. Then one
  rounding. √(−0) = −0, √(+∞) = +∞, √(negative) = NaN and Invalid.
- Oracles: `Math.Sqrt` and `MathF.Sqrt` bit for bit (crowd round 2^k and the subnormals, whose
  roots are normal); BigInteger Newton for the integer root (check r² ≤ n < (r + 1)²); the
  rational oracle for every mode and precision (compare r² with x exactly: the rounded root r
  is correct iff x lies on the right side of the rounding boundaries, squared). Extend the
  flags predictor: a square root's exact value is irrational unless x is a square, so "inexact"
  is r² ≠ x.
- Mutations to plan: the two-bit step's carry, the exponent's parity, the sticky bit dropped,
  √(−0) losing its sign, Invalid for −0.
- After 6c, order item 3 is done. Next is item 4 (parse into an `IeeeFormat`; now that the
  context exists, `ieee.Parse(text)` with Inexact/Overflow/Underflow is the natural shape: ask).

The 6a design notes, kept for reference:

**Answered (Matthew, 2026-10-10): a context object.** `var ctx = new IeeeContext(IeeeFormat.Binary64)`
(or a precision) holds the target, the rounding mode (settable) and sticky `Flags` (cleared with
`ClearFlags()`); `ctx.Divide(a, b)`, `ctx.Sqrt(x)` and so on raise them. The static
`ApFloat.Add/Divide/...` stay as they are, without flags; new operations go on both. (The
brief had recommended an `out` parameter; reading the code showed it would need the mode written
out and about 20 overloads, so the recommendation changed before asking.) The options were:
- **An `out IeeeFlags` on the format overloads** (recommended): `ApFloat.Divide(a, b,
  IeeeFormat.Binary64, mode, out IeeeFlags flags)`. Pure, no hidden state, safe with jobs on
  other threads, easy to test against the oracle. `[Flags] enum IeeeFlags { None, Inexact,
  Underflow, Overflow, DivideByZero, Invalid }`.
- A context object that accumulates them (`var ctx = new IeeeContext(); Add(a, b, fmt, mode,
  ctx); ctx.Flags`), like Python's `decimal`: convenient for long calculations, but a second
  way to call everything.
- Thread-static sticky flags, as C's `fenv.h`: no new parameters, but state that leaks
  between callers and breaks under async. Not recommended.

Where they come from (IEEE 754-2019 §7):
- **Inexact**: `RoundExact`/`RoundToFormat` dropped a non-zero bit or had the sticky bit.
- **Overflow / underflow**: only with an `IeeeFormat` (the precision-only overloads have an
  unbounded exponent, so they can't). Underflow is tiny *and* inexact; IEEE lets tininess be
  detected before or after rounding. x86 SSE detects it after; pick one, say which, and test
  the values between the two definitions.
- **DivideByZero**: finite non-zero / ±0. **Invalid**: 0 × ∞, ∞ − ∞, 0/0, ∞/∞, sqrt of a
  negative, remainder(x, 0) or remainder(∞, y), and converting NaN or out-of-range to an integer.
- NaN in, NaN out raises nothing (quiet NaNs only here: NaN is canonical).
- The rational oracle (`FloatOracle.Round`) knows exactness, so it can predict every flag.
  .NET doesn't expose the hardware's flags, so the hardware can't be the oracle for them.

6b's operations, each correctly rounded once, `IeeeFormat` and precision overloads, flags included:
- `FusedMultiplyAdd(a, b, c)`: the exact product (a significand multiply) plus c, aligned,
  rounded once. Hardware oracle: `Math.FusedMultiplyAdd` at binary64, `MathF` at binary32.
- `Floor`, `Ceiling`, `Truncate`, `Round` (ties to even, and `MidpointRounding.AwayFromZero`):
  IEEE's roundToIntegral. Exact results, so only Inexact (and only for the "-Exact" variants
  IEEE names). Oracle: `Math.Floor` etc.
- `IeeeRemainder(x, y)`: x − n·y with n = x/y rounded to nearest even. Always exact. Oracle:
  `Math.IEEERemainder`, and the rational oracle.

6c:
- `ApInt.Sqrt` (integer square root): bitwise, digit by digit, two bits at a time with a
  restoring subtract, like `DivRem` (the project's rule: no CPU multiply). Oracle: BigInteger
  Newton in the tests; check floor(√n)² ≤ n < (floor(√n)+1)².
- `ApFloat.Sqrt`: scale the significand to an even exponent with p + 2 bits of root, take the
  integer root, and the remainder's non-zero is the sticky bit. Oracle: `Math.Sqrt` bit for bit
  at binary64, and the rational oracle (r² against x, exactly).

Pitfalls to expect: signed zeros (√−0 = −0; FMA's zero sign rules); FMA where a×b and c cancel
exactly (the sign of zero depends on the mode); the exponent alignment in FMA when c is far
below a×b (the gap shortcut from Add applies). Mutation plans per piece, as before.

### Session 7 — parse into an `IeeeFormat` (order item 4): brief for a fresh chat
Goal: text rounded **once**, straight into a format, subnormals and overflow included, with
flags. Today the only route is `Parse(s, 53).WithFormat(IeeeFormat.Binary64)`, which rounds twice
(to 53 bits, then again among the subnormals: wrong in the last place for some subnormal
text), and does the full work for "1e400" before overflowing.

**Ask Matthew first** (one question, with a preview of the calls):
- Shape. Recommended: `ApFloat.Parse(s, IeeeFormat format, RoundingMode mode = ToNearestEven,
  IFormatProvider? provider = null)` and `TryParse` to match, plus `ieee.Parse(s)` on the context
  raising Inexact / Overflow / Underflow (IEEE's convertFromDecimalCharacter). That's the same
  pattern as every operation since 6a: static without flags, the context with them.
- Whether `StartParse` gets a format overload too. It's cheap if the rounding target is
  already threaded through. Recommend yes, but it can wait.

Where it plugs in:
- `ParseCore` / `PrepareParse` take a precision today. Thread an `ApFloat.Target` (and
  `ref IeeeFlags`) through instead. `Target.Round` is already the rounding point that knows
  formats (`RoundToFormat`).
- Decimal: `FromDecimalSteps` passes `(mag, exp) => RoundExact(...)` to the certified engine,
  and `FromDecimalExact` calls `RoundExact`. Both become `target.Round`. The engine accepts when
  both ends of its interval round to the same value. Rounding is monotone in a format too, so
  rounding both ends into the format keeps that sound.
- Hex and binary: `new ApFloat(m, exp, precision, mode)` near the end of the hex path becomes
  `target.Round` as well.
- Bounded range means bounded cost: a decimal exponent clearly past 2^(emax+1) is overflow at
  once, and one clearly below half the smallest subnormal is a signed zero or the smallest
  subnormal (by mode), with Underflow | Inexact. Use a safe margin on log2(10), and send the
  grey zone down the ordinary path.

Pitfalls:
- **Inexact needs exactness, and the certified route doesn't give it**: the engine knows the
  rounding, not whether the value was already representable. Decide it from the digits:
  X × 10^n is a dyadic rational iff n ≥ 0 or 5^-n divides X. When it is, the exact route is
  cheap; compare the result with it. Otherwise the result is inexact (a non-dyadic can't be a
  binary float).
- Underflow is judged after rounding (6a's rule). A decimal just under 2^emin can round up to
  it. `RoundToFormat` does this already, as long as every route ends in `target.Round`.
- NaN and infinity text raise nothing. Text that isn't a number throws, as now. It doesn't raise
  Invalid.
- The job hooks (`ForcedRoute`, `LastRounds`, `LastExact`) are thread-static and the tests rely
  on them. Keep them meaning the same thing.

Oracles: `double.Parse` and `float.Parse` are correctly rounded in .NET (since Core 3.0), and
`Half.Parse` should be, since it shares their code. Confirm that against `EncodeRational` before
leaning on it. Then check all three bit for bit on text crowded at the subnormal and overflow edges, at
halfway points written out in full (e.g. 2^-1075 to every digit), and with long digit strings.
`FloatOracle.EncodeRational` on the text's exact rational (digits × 10^exp) covers every mode
and the 6-bit formats. `FloatOracle.RoundingFlags` predicts the flags. Remember the session-4
lesson: force the engine's route (`ForcedRoute`) so a mutation can't hide behind the default
one.

Mutations to plan: the engine's ends rounded to the precision rather than into the format (a
double rounding), the early-out margins too tight, Inexact taken from the engine instead of from
exactness, hex text skipping the format, tininess judged before rounding.

### The rest (from the reference-mode plan's old session 6; see "Order of work" for where each now goes)
- ~~IEEE status flags: inexact, overflow, underflow, invalid, divide-by-zero.~~ Done in 6a.
- `Parse` into an `IeeeFormat`: a bounded range means bounded cost, so 1e400 → ∞ in binary64
  at once. (Order item 4.)
- Karatsuba multiplication and a dedicated squaring, still bitwise, for the exact routes
  and for `ApInt`. (Order item 7.)
- Streaming digits for huge output (order item 8); disk-backed checkpoints (decision 3, if
  wanted, not placed yet).

## ApFloat — later
- [x] `FusedMultiplyAdd` (exact a×b + c, round once — nearly free here), `Sqrt`
      (needs `ApInt` integer square root), `Floor`/`Ceiling`/`Truncate`/`Round`, IEEE
      remainder. (Order item 3: done 2026-10-11, sessions 6b and 6c.)
- [ ] Transcendentals (exp, log, sin, π) — need error bounds (Ziv's retry strategy)
      to stay correctly rounded. A project of its own; session 4's engine is the start of it.
      (Order item 8.)
- [ ] Generic math: `INumber<ApFloat>` (order item 6), `IFloatingPointIeee754<ApFloat>`
      (order item 9: it requires the transcendentals).

## Done
- [x] Division — `DivRem`, `/`, `%`, truncating like C# (2026-09-27). Repeated
      `% 10` is cross-checked against double dabble in the tests.

- [x] Beyond integers — fixed-point or rational/floating built on top of `ApInt`
      (the "precision" half of the name). This became ApFloat (2026-09-30).

## Later
- [ ] Bitwise operators on `ApInt` (`&`, `|`, `^`, `~`) — two's-complement
      semantics for negatives, to match `BigInteger`. (Order item 5.)
- [ ] `Pow`, `ModPow`, `Gcd` (order item 5). (Integer `Sqrt` and `SqrtRem` came with 6c.)
- [ ] Speed, if it ever matters: Karatsuba multiply; a bit-parallel adder that
      doesn't loop per carry. Keep the bitwise-only rule. (Order item 7: the same item as
      the Karatsuba line under the reference mode's "The rest".)
- [ ] Generic math interfaces (`INumber<ApInt>` etc.) if it's to be used as a drop-in.
      (Order item 6.)

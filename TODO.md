# TODO

## Order of work (agreed with Matthew 2026-09-30)
1. ~~**Session 4**: the certified interval engine.~~ Done 2026-09-30.
2. **Session 5**: resumable jobs, built on 4. **Next**; brief below, proposed as 5a and 5b.
3. **The IEEE operations ApFloat still lacks**: FMA, Floor/Ceiling/Truncate/Round, IEEE
   remainder, and `ApInt` integer square root, then `ApFloat.Sqrt`. The **IEEE status
   flags** go in first, in the same session, so each new operation reports them from day one.
4. **Parse into an `IeeeFormat`**: small, and for very wide formats it uses 4's engine.
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
- [ ] **A reference mode with no cap**: agreed 2026-09-30. Session 4 (the engine) is done;
      session 5 (jobs) is next. Whether the default keeps its cap is open (below).

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
   default. The reference mode is **opt-in** and uncapped. (Reopened after session 4, since
   the reason for the cap has mostly gone: see "Open, from session 4".)

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
- [ ] **Matthew's call: does the default Parse still need its cap?** It was there because
      1e100000 took 5 s and grew with the square of the exponent. Now a short input takes
      milliseconds whatever its exponent (up to about 10^(2.7·10^18), where the binary
      exponent passes a long). What's left that's slow is long input: a 30,000-digit decimal
      parses in 0.75 s, quadratic in its length, capped or not. The options: drop the cap
      (only a binary exponent past a long is refused), keep ±100,000 (decision 4), or raise it.
      The answer changes what session 5's opt-in entry point is for: unlocking exponents, or
      only stepping long jobs.
- [ ] Long digit strings both ways are quadratic in their length: ApInt's decimal digit loop
      when parsing (30,000 digits: 0.75 s), and double dabble when printing (`F0` of 1e30000:
      0.22 s). Session 5's jobs make them resumable; order item 7 (speed) would make them faster.

### Session 5 — resumable jobs (brief for a fresh chat)
Goal: an opt-in reference mode whose work the caller holds, steps, pauses and resumes.
`StartParse` / `StartFormat` return a job; `Continue(budget)` does up to that much work; then
`Complete`, `Result`, `Progress` (0..1), and a certificate. It's uncapped (decision 4). The
job machinery must be a general piece, not tied to parsing (Matthew's reuse aim below).

**Ask Matthew first** (one short question with a preview, recommendation first, as usual):
the cap question under "Open, from session 4", and the API shape below. Then build.

What actually takes time now (Release, measured 2026-09-30), so what a job must be able to
pause:
- Long digit strings in: ApInt's decimal loop (x·10 + d per digit) is quadratic. 30,000 digits
  take 0.75 s, so a million would take about 15 minutes.
- Long digit strings out: double dabble, one pass per bit. `F0` of 1e30000 takes 0.22 s.
- Ties and exact values with a huge exponent: the exact route (5^n in full, then a multiply
  or a restoring division). These only come from long inputs.
- A long input a hair from a boundary: engine rounds at W of about 3.3 bits per digit.
Short inputs finish in milliseconds and don't need a job.

Recommended design (a proposal to confirm, not settled):
- **Iterators as the resumable form.** Write each long loop as a C# iterator that
  `yield return`s the work it just did (`IEnumerable<long>`). The compiler keeps the loop's
  state (the bit index, the arrays), so resumable code reads like the one-shot code. The job
  driver pulls from the iterator until the budget is spent. Measure the overhead first. If it
  costs the everyday path, keep the one-shot loops as they are and test the two against each
  other.
- **Work units**: one unit per limb-sized step (a FullAdd over one limb, or one division
  step), counted where the loops yield, so it's identical on any machine. A time budget
  checks a `Stopwatch` every few thousand units (decision 2: both, units underneath).
- **Generic core** (Matthew's reuse aim): `Job<T>` with `Continue(long units)`,
  `Continue(TimeSpan)`, `Complete`, `Result`, `Progress`, `WorkDone`, over any
  `IEnumerable<long>` plus a result. Numbers are just its first user. In its own file, and
  possibly a namespace (`Natural.Jobs`).
- **Progress**: the digit loops and the exact route know their total work up front (from
  lengths and bit lengths). The engine's rounds grow 4× each (W doubles, cost ~W²), so
  estimate from the current round.
- **Certificate**: rounds taken, the final W, the two interval ends, and whether the exact
  route finished it.
- **Entry points**: `ApFloat.StartParse(text, precision, mode, provider)`,
  `ApFloat.ParseReference(...)` (start, then run to completion), and
  `x.StartFormat(format, provider, mode)`. The internal `ParseUncapped` becomes
  `ParseReference`.
- Cancelling is just not calling `Continue` again. Maybe add a `CancellationToken` overload.

**Proposed split** (it's big): **5a** is the generic `Job<T>`, the resumable decimal digit
loop, and `StartParse`/`ParseReference`, with the engine's rounds as whole steps. **5b** is
the resumable exact route (multiply and divide loops, 5^n), `StartFormat` with resumable
double dabble, and progress estimates for the engine.

Things to get right:
- The thread-static test hooks (`LastRounds`, `LastExact`) mustn't carry a job's state: a job
  may be continued on another thread. Keep a job's round count in the job.
- A job's result must be bit-identical to the one-shot result. There's one rounding point
  (`RoundExact` / `RoundToFormat`), and the job reaches it by the same route.
- `Continue(n)` may overshoot n by at most one step. Document the step size.
- Memory: a job holds its arrays for as long as the caller holds the job (decision 3: in
  memory; disk checkpoints are not agreed).

Tests:
- A job equals the one-shot `Parse`/`ToString` for random inputs in every mode, including when
  stepped one unit at a time.
- The budget is honoured (work done is at most budget + one step). `Progress` never goes down
  and ends at 1. `Complete` is false until the result exists.
- Long inputs (100,000 digits) stepped to completion, against the oracle.
- The certificate's two ends bracket the exact value (use the rational oracle).
- Time budgets roughly honoured, but not asserted tightly (this machine has two speeds).
- Mutation plan: a resume that restarts a loop from 0; a `Continue` that ignores its budget;
  `Complete` set one step early; a carry lost across a yield in a resumable multiply;
  `Progress` computed from the wrong total.

Matthew, 2026-09-30, on reuse: he expects this structure to turn up in other projects "where
we need bigger numbers to put a cap on possibilities" and "must be able to iterate across
them in a controlled and safe way". So the budget, the work-unit count,
`Continue`/`Complete`/`Progress` and the certificate belong in a general piece that a parse or
a format plugs into.

### The rest (was session 6; see "Order of work" for where each now goes)
- IEEE status flags: inexact, overflow, underflow, invalid, divide-by-zero. (Order item 3.)
- `Parse` into an `IeeeFormat`: a bounded range means bounded cost, so 1e400 → ∞ in binary64
  at once. (Order item 4.)
- Karatsuba multiplication and a dedicated squaring, still bitwise, for the exact routes
  and for `ApInt`. (Order item 7.)
- Streaming digits for huge output (order item 8); disk-backed checkpoints (decision 3, if
  wanted, not placed yet).

## ApFloat — later
- [ ] `FusedMultiplyAdd` (exact a×b + c, round once — nearly free here), `Sqrt`
      (needs `ApInt` integer square root), `Floor`/`Ceiling`/`Truncate`/`Round`, IEEE
      remainder. (Order item 3.)
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
- [ ] `Pow`, `ModPow`, `Gcd` (order item 5); integer `Sqrt` (order item 3, for `ApFloat.Sqrt`).
- [ ] Speed, if it ever matters: Karatsuba multiply; a bit-parallel adder that
      doesn't loop per carry. Keep the bitwise-only rule. (Order item 7: the same item as
      the Karatsuba line under the reference mode's "The rest".)
- [ ] Generic math interfaces (`INumber<ApInt>` etc.) if it's to be used as a drop-in.
      (Order item 6.)

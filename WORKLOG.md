# WORKLOG

## 2026-09-27 — Project started: add, subtract, multiply

Matthew's brief: an arbitrary-precision data type in C#, with addition,
subtraction and multiplication done "using binary operations"; division next.

Built `Adpdt.ApInt` (sign + magnitude, `uint[]` limbs, little-endian):

- **Add** — `BitMath.FullAdd`, a word-wide ripple adder from XOR/AND/shift
  only; the carry out of bit 31 is caught as it shifts off the top.
- **Subtract** — two's complement, `a + ~b + 1`, reusing the adder.
- **Multiply** — shift-and-add over the multiplier's set bits; only 32 shifted
  copies of the multiplicand are ever needed (bit offset 0..31, plus a whole-limb
  offset), built lazily.
- **Shifts, comparison, equality, long/ulong conversions, ++/--.**
- **Text** — decimal output by double dabble (no division needed, which matters
  since there's no divide yet), packed 8 BCD digits per `uint` with a SWAR add-3;
  decimal input by `acc*10 + d` with `*10` as `(acc<<3)+(acc<<1)`; hex and binary
  both ways; `_` digit separators.

Tests: 50, all green, `BigInteger` as oracle. Mutation check: dropping the carry
out of `FullAdd` fails 21 of them.

Rough speed (Release, operands ~all ones = worst case for shift-and-add):
100,000-bit add 0.14 ms, multiply ~0.75 s, decimal ToString of the 200k-bit
product ~0.9 s.

Not a git repo yet — flagged to Matthew.

## 2026-09-27 — Renamed BitMath to Magnitude

The class is named for what it operates on (the magnitude half of sign + magnitude)
rather than how. File is now `src/Adpdt/Magnitude.cs`; no behaviour change.

## 2026-09-27 — Division; float design agreed

Initial commit `34eef95` after Matthew's `git init` (no remote yet).

**Division**: `ApInt.DivRem` (tuple), `/`, `%`, truncating like C# — quotient sign
is the XOR of the signs, remainder takes the dividend's. `Magnitude.DivRem` is
restoring binary long division: shift the dividend's bits into a running
remainder one at a time, subtract the divisor (a + ~b + 1 again) whenever it
fits, write a 1 in that quotient place. The remainder lives in one array of
divisor-length + 1 and is worked on in place. Divide by zero throws
`DivideByZeroException`; `long.MinValue / -1` is just 2^63 here.

Tests 50 → 59, checked against C#'s own `long` `/` and `%` as well as
`BigInteger`, plus: repeated `% 10` agrees with double dabble (two independent
routes to the decimal digits). Mutation check: `>= 0` → `> 0` in the "does it
fit" comparison fails 7. Speed: 200,000-bit / 100,000-bit in ~0.3 s.

**Floating point design** (discussed with Matthew, his choice): arbitrary precision
but IEEE-style and byte-aligned. Matthew proposed "max / 2" as an exponent bias —
which is exactly IEEE 754's bias (`max >> 1` = `0111…1`: 127, 1023, 16383). Plan:
MPFR-style `ApFloat` in memory (signed `long` exponent, no bias needed), correctly
rounded like IEEE, and IEEE `binary{k}` as the byte format, where the bias lives;
k any multiple of 32 from 128 via the IEEE 754-2008 width formula. Details in TODO.

## 2026-09-30 — Project renamed Adpdt → Natural

Matthew named the GitHub repo `MatthewCarven/Natural`, so the code follows:
namespace `Natural`, `Natural.slnx`, `src/Natural/Natural.csproj`,
`tests/Natural.Tests/`. All moves via `git mv`. The internal limb-arithmetic class
stays `Magnitude` — the rename to `Natural` considered on 2026-09-28 was only to
dodge a `Magnitude` namespace, which didn't happen. The folder on disk is still
`ADPDT` (Matthew's call). `origin` moved from `Magnitude.git` to `Natural.git`;
the old `Magnitude` repo is Matthew's to delete or archive.

Done two days late: on 2026-09-28 a server-side permission-check outage blocked
every shell command for the rest of the session.

## 2026-09-30 — ApFloat core (smoke-tested), and the plan for the rest

`src/Natural/ApFloat.cs`: ±m × 2^e (m odd, `long` exponent, no bias in memory),
precision per value, ±0 / ±∞ / NaN. Every operation computes the exact result with
the integer code and rounds once in `RoundExact` (four IEEE modes; a sticky bit for
division's remainder; a `minExp` floor for the subnormals to come). `+ − × ÷` with
IEEE's special cases; `Add` swaps an operand far below the rounding point for a
single sticky bit, so 1 + 2^-1000000 costs nothing. Hex-float `ToString`. Small
additions: `Magnitude.TestBit` / `AnyBitsBelow` / `TrailingZeroCount`, and
`ApInt.Limbs` / `FromLimbs` (internal).

Smoke check, not tests: 12/12 against known `%a` strings (1/3, 1/10, 0.1 + 0.2 =
0x1.3333333333334p-2, signed zeros, 1/0, 0/0, the gap shortcut both ways). The one
"failure" was my expected value: 1/3 at 200 bits ends ...56p-2, not ...58p-2, as
Python's `fractions` confirmed independently.

Matthew asked to stop at a plan this session; the rest is in TODO as three sessions
(prove the core + Half/float/double; `binary{k}`; decimal text) and three open questions.

**Decided, same day**: decimal text is designed for eyeballing. Matthew reads a column
of numbers like a bar graph (zeros kept, so widths and points line up, as in Chess
Bruteforcer's fixed-width output). Default `ToString()` will print the digits the
precision carries, zeros kept (17 for 53 bits, 62 for 200); aligned `F`/`E`/custom-`0`
formats and a binary-point view come too. All three are in TODO session 3.

**Also decided**: default precision **256** bits (`ApFloat.DefaultPrecision`, about 77
significant digits), changed in code, checked against Python for 1/3. And **canonical NaN**:
the byte encoding writes a single quiet NaN pattern and reads any NaN as NaN (session 2).
Values converted from `double`/`float`/`Half` keep 53/24/11 bits, so `double` still
works as the test oracle. No open questions left in TODO.

## 2026-09-30 — ApFloat session 1: the core proved, and Half / float / double

**Tests 59 → 95**, all green in ~2 s. Everything the TODO's session 1 asked for:

- `FloatOracle.cs`, the reference: exact rationals (`BigInteger` over `BigInteger`),
  rounded by long division with the remainder compared against half the divisor. It
  shares no code with `RoundExact`, which tests bits. It has a `minExp` floor and an
  IEEE encoder of its own, so it can check subnormals and overflow in every mode,
  where the hardware only knows round-to-nearest.
- Constructor, `WithPrecision` and `+ − × ÷` against it: random limb-biased operands,
  precision 1..200, all four modes, plus near-cancelling pairs.
- The special-case table checked against `double` itself: 17 specials, every pair,
  every op and comparison, bit for bit. Also signed zeros in all four modes.
- The gap shortcut against exact addition, with b's leading bit within ±4 places of
  the threshold.
- `ApFloat.Ieee.cs`: one encoder/decoder over (exponent bits w, precision p), with the
  bias as Matthew's max / 2 (`maxField >> 1`). The largest finite value is
  `maxField ^ 1` in the exponent field with the fraction all ones. It rounds once,
  straight to the format, via `RoundExact`'s `minExp` floor. Past the top, or under
  half the smallest subnormal, it exits early, so `2^(2^60)` converts at once. Implicit
  `Half`/`float`/`double` in (exact, keeping 11/24/53 bits), explicit out, and
  `ToDouble/ToSingle/ToHalf(mode)`.
- Hardware as oracle: every `Half` pattern round-trips; tiny formats (w 2–5, p 2–6)
  do so exhaustively, in every mode; 100k `float` and 100k `double`; narrowing matches
  the hardware's `(float)` and `(Half)` casts. `+ − × ÷` are bit-identical to `double`
  and `float` over 20,000 pairs each, crowded round the subnormal and overflow edges.

**A trap the new conversions opened, closed.** With implicit `float` and `double`
conversions, C# resolves `ApFloat x = someUlong` to the *most specific* source type it
can reach. That was `float`, silently, losing bits. `(long)x` likewise went through
`float`. Added exact `uint`/`ulong` in and `long`/`ulong` out (truncating, throwing on
overflow), with tests. `uint` is there because without it a `uint` or `byte` is
ambiguous between `long` and `ulong`.

**Found: double rounding at 53 bits among the subnormals.** ApFloat's exponent is
unbounded, so a 53-bit product that double would hold as a subnormal rounds a second
time on conversion. A probe of a million random products landing there: 10,454 of
987,109 differ from the hardware in the last place (about 1%); quotients 2,165 of 201,587.
Rounding the exact result once is always right, and the hardware tests do that: sums
at 2,200 bits (exact), products at 106 (exact), quotients at 256. A quotient that isn't
a midpoint stays at least 2^-106 of its size away from every midpoint, so 256 bits
can't make or cross one. The class doc used to say "the same results as double at 53
bits" without that caveat; it has it now. Open question in TODO: whether to add
arithmetic *in a format*, which would pass the format's `minExp` to `RoundExact`.

**Mutation check**: 12 real mutations, all red. They were ties-to-even without `|| rest`;
toward-+∞ ignoring sticky; `x − x` always +0; divide ignoring its remainder; divide
without guard bits; three gap-threshold/stand-in breaks; the subnormal floor one place
high; the underflow stand-in too eager; toward-zero overflow going to ∞; NaN encoded
signalling. Two mutations I tried first stayed green and are **equivalent**: `Add`'s
threshold is `min(a._exp, topA − p − 1) − 1` where `min(...)` alone would do, so
mutations that only use up that one place of slack are still correct. The ones past
the true boundary all go red.

## 2026-09-30 — ApFloat session 2: IEEE binary{k} as bytes, any width

**Tests 95 → 155**, all green in ~2 s.

- `IeeeFormat` (`src/Natural/IeeeFormat.cs`): (exponent bits, precision), with bias,
  emax and emin. `IeeeFormat.Binary(k)` covers binary16/32/64 and every multiple of 32
  from 128. The width rule w = round(4·log2 k) − 13 is done in integers:
  2^(2t−1) ≤ k^8 < 2^(2t+1), so t is half of k^8's bit length, with k^8 an `ApInt`. It
  can't tie, because k^8 would have to be an odd power of two. The widest format whose
  exponent fits a `long` is binary480768 (w = 62). The test checks the formula against
  `Math.Log2` for all 15,000 widths. Custom formats work too: bfloat16 is (8, 8).
- `ApFloat.ToIeeeBytes(format, mode, bigEndian)` / `FromIeeeBytes(bytes, format,
  bigEndian)`, little-endian by default. The encoder and decoder now work on the bit
  pattern as little-endian words of any length. Bit fields are written with bitwise
  loops (`PutBits`, `GetBits`), and the significand is ORed in so it can't clobber a
  sign sharing its word. `Half`/`float`/`double` and the internal `ulong` entry points
  now run on the same core, so session 1's 95 tests kept checking it through the rewrite.
- NaN is canonical, as agreed: it always encodes as the one quiet NaN, and any NaN decodes as NaN.

The oracle went wide with it: patterns are `BigInteger`s. Its encoder rounds the
significand alone, with the subnormal floor moved by the exponent, because rounding
commutes with scaling by 2^e. binary1024's range reaches 2^67,108,864, which the old
rational route would have had to build as a number.

Reference values: π to 400 bits by Machin's formula in Python integers, matching the
published hex expansion. binary128 π came out as `4000921FB54442D18469898CC51701B8`,
the TODO's vector. binary256 π and 1/3 come from a separate Python encoder (Fractions
only), so the two implementations agree on vectors neither was written against.
The binary128 table is 1, −2, π, 1/3, max, min normal, min subnormal, −0, ±∞ and NaN;
binary256 has the same.

A cross-check I liked: bfloat16 has float's exponent, so a float rounded toward zero
into bfloat16 must be exactly the float's top two bytes. It is, for 20,000 floats.

**Mutation check**: 13 of 13 red. The mutations were: the width formula rounding up;
emin off by one; big-endian ignored on read and on write; the fraction keeping the
exponent's bits; `PutBits` never clearing; the largest finite value losing its partial
word or getting the wrong exponent field; the quiet-NaN bit at the bottom; the
significand overwriting the sign; the subnormal floor one place high; toward-zero
overflow going to ∞; the sign read one bit low.

The session 1 question (double rounding among the subnormals at 53 bits, and whether to
add arithmetic *in a format*) is still open. `IeeeFormat` now exists, so that option
would be `ApFloat.Multiply(x, y, IeeeFormat.Binary64)`.

## 2026-09-30 — ApFloat session 3: decimal text, built for eyeballing

**Tests 155 → 314**, all green in ~7 s.

Two design calls from Matthew at the start. The default `ToString()` is **always
scientific** with every digit the precision carries, zeros kept: `1.0000000000000001E-001`.
Every value of one precision is the same width, so a right-aligned column lines up on the
point and the E. And exact **ties go to even** (0.125 F2 is `0.12`; .NET says `0.13`).

`src/Natural/ApFloat.Text.cs`:
- `DecimalDigitsFor(p)` = floor(p·log10 2) + 2, the smallest N with 10^(N-1) > 2^p (17 for
  a double, 79 for the 256 default). log10 2 is a 128-bit fixed-point `ApInt` constant, so
  there's no floating point. Checked against 2^p's digit count for every p up to 3000.
- Output starts from the exact value, |v| × 10^t = m × 5^t × 2^(e+t), and rounds it to an
  integer through `RoundExact` with `minExp: 0`. So text shares the single rounding
  point, and every format takes a `RoundingMode`. The decimal exponent starts from an
  estimate, floor(Top · log10 2), and is corrected exactly.
- Formats: default, `E<n>`, `F<n>`, `G<n>`, `R`/`G` (shortest round trip, found by binary
  search over the digit count), `B<n>` (binary point), `A`/`X<n>` (C's `%a`), and custom
  `0 # . ,` patterns with literals. Custom sections, `%` and exponents throw rather than
  print something wrong. `IFormattable`, `ISpanFormattable`, `IParsable`; the culture
  decides the separator and signs, except for hex and binary, which always use ".".
- `Parse`: decimal (with `_` between digits), hex floats, `0b` binary with a `p` exponent,
  NaN/Infinity/inf/∞. It builds digits × 10^exp exactly and makes one rounding, at a
  chosen precision and mode.

Checked against a new `BigInteger` oracle (`ScaledRound`, `Scientific`, `Fixed` in
`FloatOracle`) in every mode, and against .NET itself. `E16` equals ApFloat's default for
doubles; `E<n>`, `F<n>` and `G<n>` match .NET's digits (ties excluded, since .NET goes away
from zero); `R` matches .NET's digits in the normal range; `Parse` matches `double.Parse`.

**Found: .NET 10.0.12's `double.ToString("R")` doesn't round-trip 2^-25 or 2^-958.** Each
gives 16 digits that parse to the double below: the lower neighbour of a power of two is
half as far away, and the 16-digit text lands outside that half-gap. Python's repr gives 17
digits, as ApFloat does. Pinned in `ShortestMatchesDotNetR`. And by design, ApFloat's `R`
differs from .NET's for subnormal doubles (2^-1074 needs 17 digits at 53 bits with an
unbounded exponent, not "5E-324").

**Speed.** The first run took 37 s, most of it in `R` over 20,000 doubles, and it was
recomputing 5^n by squaring each time. Powers of five below 5^4096 are now cached,
built as 5x = 4x + x. `Magnitude.DivRem` now also brings the dividend's top
(divisor bits − 1) bits down in one shift, since they can't make a quotient bit: a
700-bit over 697-bit division takes 4 steps, not 700. The suite went back to ~7 s.

**Mutation check**: 16 mutations, 15 red, and 1 caught as a hang. Rounding to even
integers makes `DecimalExponent` oscillate forever; I killed the test host by hand so the
script could restore the file. One survived at first: hex rounding to 4n bits instead of
4n + 1. Every hex test case happened to agree either way, so two cases now go through the
kept bit (1 + 2^-12, and the tie 0x1.0018): red.

Open, in TODO: parsing "1e1000000" is slow (the 5^1000000 multiply) rather than refused.
Reporting the .NET bug upstream is Matthew's call.

## 2026-09-30 — Arithmetic in a format

Matthew's call on session 1's open question: add it, "Binary64 style". So each operation
now comes two ways:

- `Add(a, b, 53)`: a precision, with an unbounded exponent, as before.
- `Add(a, b, IeeeFormat.Binary64)`: rounded once, straight into the format, with its
  subnormals near zero and its overflow at the top (∞, or the largest finite value when
  rounding toward zero from that side), in all four modes.
- `x.WithFormat(format, mode)` does the same for a single value.

How: a private `Target` (a precision, and optionally a format) goes down through one core
per operation, and the last step is either `RoundExact` or the new `RoundToFormat`.
`RoundToFormat` is `RoundExact` with the format's `minExp` floor, the stand-in for values
under half the smallest subnormal, and overflow by mode. The byte encoder was refactored
onto it: `EncodeIeee` = `WithFormat`, then an exact `PackIeee`. So the encoder and the
arithmetic can't disagree about what a format holds. All 314 existing tests passed across
the refactor.

Evidence: binary64 and binary32 arithmetic is bit-identical to `double`/`float` over 30,000
edge-crowded pairs each, **with nothing excused**. Every rounding mode is checked against
the rational oracle, whose encoder now takes an exact rational. Three 6-bit formats are
checked exhaustively: every finite pair × 4 ops × 4 modes. Operands from outside the
format (wider, and past both ends of its range) are covered too. binary128 and binary1024
have known cases. The session 1 probe (a million products landing subnormal) re-run:
**0 of 1,188,696 differ in binary64**, against 12,619 for the 53-bit operators.

**Mutation check**: 8 of 8 red. Two lessons from it:
- **"0 + b skips the format"** was caught only by the operands-from-outside test, added
  just before the run because in-format operands can never show it.
- **"Overflow only checked before rounding"** was first caught by one test. The new
  overflow test missed it, because it read the result through `(double)`, and converting
  applies the overflow again, hiding a result that had wrongly stayed a finite 2^1024. The
  tests now check that every in-format result is exactly in the format (no rounding mode
  moves it), and 7 tests catch that mutation.

The operators (`x * y`) keep the unbounded exponent, and the class doc says so and points
to the format overloads.

## 2026-09-30 — Parse cap for huge decimal exponents

Matthew's call. Exact parsing builds 5^|n| in full, which is slow: 1e10000 takes 0.1 s,
1e30000 0.5 s, 1e100000 5 s, growing with the square of n (measured with Release on this
machine, where `ToString()` of the same values took 0.14 / 1.5 / 15 s). `MaxDecimalExponent`
= 100,000 is now checked before any arithmetic. Past it, `Parse` throws `OverflowException`
(.NET's convention for a number too big, as `int.Parse` does) and `TryParse` returns false.
The parse core now reports why it failed (`ParseOutcome`): a malformed exponent is a
`FormatException`, and one past a long is an `OverflowException`. Zeros and hex/binary text
are unaffected. Formatting isn't capped. Tests 327 → 333. Mutation check: 4 of 4 red.

Matthew then asked for an uncapped "reference mode": iterative, resumable, with a
"complete?" flag. The plan in progress is in TODO under "Proposed".

## 2026-09-30 — Reference mode planned (sessions 4–6)

Matthew wants an uncapped "reference mode" that iterates, reports "complete?", and can be
resubmitted to continue. Agreed, with his four decisions recorded in TODO ("Reference mode:
the plan"):
1. **Certified intervals** (Ziv's strategy) as the engine: bound 5^n from below and above
   with directed rounding, round both ends, and accept when they agree; otherwise double
   the working precision, ending at exact.
2. **Both budgets**: wall-clock time and work units, with work units underneath.
3. **Jobs in memory**: the caller holds the job and nothing is dropped while it lives.
   Disk-backed resubmission was floated as a maybe-later.
4. **The cap stays the default**; the reference mode is opt-in and uncapped.

Session 4 (the engine) has a full brief in TODO, written for a fresh chat: the baseline
timings to beat, where it plugs in (`FromDecimal`, `ScaledToInteger`, `PowerOfFive`), the
sign and tie pitfalls, and the tests. The tests include reference values for huge
exponents computed offline in Python, and constructed near-midpoint inputs that force
extra rounds. Session 5 is the resumable job API; session 6 is optional extras (status
flags, parse into a format, Karatsuba).

Matthew is archiving this chat and starting fresh next time. He expects to be offline from
about 1 October to 8 or 9 October, with a few more sessions before then.

## 2026-09-30 — Order of work agreed

Matthew agreed a nine-item order for everything left (now at the top of TODO): sessions 4
and 5, then the IEEE operations ApFloat lacks (status flags first), parse into a format,
`ApInt` bitwise, generic math, speed, transcendentals, `IFloatingPointIeee754`. Session 6's
extras were split up into that list. He expects the job machinery (session 5) to be reused
elsewhere, wherever big numbers put a cap on possibilities and have to be iterated across in
a controlled, safe way. That's now a requirement in session 5's notes.

## 2026-09-30 — Session 4: the certified interval engine

Decimal text past the cache of powers of five (|n| >= 4096) no longer builds 5^|n|.
`ApFloat.Certified.cs` bounds it instead. lo <= 5^n <= hi comes from square-and-multiply at
W bits, rounding down for lo and up for hi. X is multiplied (or divided) by the bounds,
rounded outward, and both ends are rounded to the target. If they agree, the result is
certified. If not, W doubles, and once W reaches an eighth of 5^n's bit length the exact route
finishes: it's cheaper by then, and it's always right. Both text cores go through it:
`FromDecimal` (parsing, and R's read-back) and `ScaledToInteger` (every decimal output).

**Speed** (Release, 53 bits, both routes interleaved in one process):

| input      | Parse, exact | Parse, intervals | ToString(), exact | ToString(), intervals |
|------------|--------------|------------------|-------------------|-----------------------|
| 1e10000    | 76 ms        | 0.25 ms          | 129 ms            | 0.47 ms               |
| 1e30000    | 476 ms       | 0.25 ms          | 1.47 s            | 0.45 ms               |
| 1e100000   | 5.1 s        | 0.22 ms          | 15.2 s            | 0.47 ms               |
| 1e1000000  |              | 0.28 ms          |                   | 0.61 ms               |
| 1e10000000 |              | 0.36 ms          |                   | 0.78 ms               |
| 1e10^18    |              | 1.8 ms           |                   | 4.3 ms                |

At 256 bits the interval route takes 0.65–6.2 ms to parse and 2.6–24.5 ms to print. The exact
column re-measures the TODO's baseline to within 20%.

**Found: squaring doubles a relative error.** The bounds on 5^n end up about n·2^(2−W)
apart, not log(n)·2^−W as the brief assumed. My own tightness test caught it (5^1000 at 24
bits). The first round now works at p + 64 + bitlength(n) bits, so the margin stays 64 bits
for any n. Without that, n = 10^18 would have left about 2.

**Where the exact route still runs**: ties, and exact values in the directed modes, because
the interval ends straddle the boundary they sit on. Past the cache these need long digit
strings: q × 10^−n is a tie or exact only if q is a multiple of 5^n. A decimal a hair from a
boundary takes extra rounds (W reaches about 3.3 bits per digit of input). So parsing now
costs in proportion to the input's length, not its exponent. Short inputs take milliseconds
right up to 10^(10^18). What's still slow is long digit strings, both ways, and that's ApInt's
digit loop and double dabble, not the engine. A 30,000-digit decimal parses in 0.75 s, and
`ToString("F0")` of 1e30000 takes 0.22 s (both quadratic).

**Tests**: 333 → 484, all in `CertifiedTests`:
- The bounds bracket 5^n and are as tight as the n·2^−W analysis says.
- Forced intervals, even below the cache, against the rational oracle, for parsing and
  formatting.
- Past the cache: correct against the oracle, and certified without the fallback. The
  routes agree with each other.
- Decimals near a boundary, cut from exact midpoint expansions: two or more rounds, no fallback.
- Ties and exact values: they reach the fallback.
- Values a hair from a boundary where the bounds are exact (see below).
- 64 parse and 64 format values at 10^±5000 and 10^±(10^7), computed in Python with exact
  integers (`tests/reference/certified_reference.py`; 5**10**7 takes 4 s). The 10^±5000 rows
  also go through the old exact route, which checks the script against code written before
  the engine existed.
- Round trips at 10^±(10^18).

**Mutation check**: 13 mutations, 12 red, 1 equivalent. On the brief's four:
- Swapping lo's rounding goes red in the bounds test only. It perturbs the multiplies, not
  the squarings, so the error isn't amplified.
- Dropping the outward rounding of X × bound **survived** at first. Past the cache the bounds
  on 5^n are thousands of ulps apart, which hides it. A new test forces intervals where the
  bounds are exact (5^25 when parsing, 5^0 for "F0"), with values 10^−25 from a midpoint or a
  representable value. That test turns this mutation and its three mirrors red.
- Certifying from one end goes red.
- Skipping the doubling goes red through the round guard in about 50 ms, without hanging.

The equivalent one: log2(5)'s constant off in its last bit changes floor(n·log2 5) only within
n·2^−128 of an integer, and no n that fits a long gets that close.

The internal `ParseUncapped` gets the tests past the cap until session 5's public reference
mode. The default `Parse` keeps its cap (decision 4). Whether the cap still earns its place,
now that 1e100000 takes 0.2 ms, is a question for Matthew (TODO).

The bench was a .NET 10 file-based app: `dotnet run -c Release bench.cs` with
`#:project <csproj>` at the top, reaching the internals by reflection. It needs no project
file of its own.

## 2026-09-30 — The parse cap dropped

Matthew's call, asked with the session 4 timings: the cap's reason (5 s for 1e100000, growing
with the square of the exponent) had gone, since a short input now takes milliseconds whatever
its exponent. `MaxDecimalExponent` and the internal `ParseUncapped` are gone. `Parse` now
refuses only a value whose binary exponent wouldn't fit a long, which means decimal exponents
past about ±2.7 × 10^18 (the `checked` exponent sums in the engine throw, and `ParseCore` turns
that into `OverflowException`). "25e1999999999999999999" parses and prints back.
`HugeExponentsAreRefused` became `ExponentsPastALongAreRefused` (with the 3e18 and 4e18 edge
rows), and `AroundTheCap` became `ExponentExtremes`. Tests 484 → 483, after dropping a
duplicate. Breaking it on purpose: putting the cap back goes red in 58 tests. Making both
exponent sums unchecked goes red in the two 3e18 rows. Making only one unchecked stays green,
but that one is equivalent, since the other sum overflows on the same inputs.
Session 5's brief now says what the jobs are for: stepping long work, not unlocking exponents.

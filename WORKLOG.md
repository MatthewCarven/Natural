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

## 2026-10-09 — The space-bunny branch, reviewed

Matthew had another agent (opencode) work on a branch, `space-bunny`, on 2026-10-02/03, and
asked for a check. Seven commits on top of `f8fc52a`, all pushed to `origin/space-bunny`:
- The WinForms playground (`Natural_Playground/`), made to build (a stray `cc` after
  `</Project>`, net8.0-windows against a net10.0 library, a stray `public sdad`), then a
  calculator panel over ApFloat, and two fixes to it (a null `SelectedItem`, the layout).
- **A real bug in `ApInt.ToString`**: the BCD register was sized with 1233/4096 for log10 2,
  which is under it. At 1,955,154 bits (588,561 digits) the register is a word short and
  double dabble writes past the end. Now 1234/4096, as `DecimalDigitsForBits`, with tests at
  every length up to 2,000,000. Checked: the arithmetic holds (1,955,154 × 4.6e-6 = 9 digits
  short, against 8 digits of slack).
- **A custom pattern with no placeholders** ("'hello'") scaled the value anyway, so on
  1e(10^18) it threw. Now it prints the literal; "-hello" for a negative matches .NET (checked).
- README.md.

506 tests, all green; the build has no warnings. What the review found and fixed:
- **The `ScaledToInteger` comment was false.** The branch rewrote it to say output never uses
  the interval engine and can't. Measured: E16 and R of 1e100000 and 1e-100000 take one
  interval round in about 2 ms. F0/F4 go exact, but only because they scale by a few decimals,
  inside the cache (F0 of 1e100000: 2.5 s). The comment now says that.
- **The `Parse` comment had the crossover wrong**: "roughly 1,100 at 256 bits" (it's the
  cache, 4096, at every precision up to about 1,100 bits) and "past ~1,100 bits it goes
  straight to the exact route" (it starts later: measured exact at 10^14000 and intervals at
  10^15000 for 4096 bits, against about 14,400 from the formula).
- The custom-pattern fix had its lines at column 0; re-indented, comment shortened.
- README: `value.FromIeeeBytes` is static (`ApFloat.FromIeeeBytes`); "the CPU's add and
  multiply are never used" was too strong (exponents and lengths use them, and so does double
  dabble's add-3 on BCD); the test count was 501; the solution now builds on Windows only.
  The rest of its samples were run again and print what it says.
- Playground: the `double` check said DIFFERS for correct results in the directed modes
  (5, 7 and 4 of 20 cases), since the hardware only rounds to nearest; it now says it can't
  compare there. It compares bit for bit (±0 differ). The ulp shown for zero
  ("smallest non-zero") was wrong with an unbounded exponent. And F4 and B froze the window on
  big values, one keystroke at a time (F4 of 1e200000: 10 s; 1e1000000 would be minutes):
  past 100,000 bits either side of the point they now say how long they'd be instead.

Not changed, for Matthew: whether `space-bunny` goes to master (there's no local master
here, just `origin/master` at `f8fc52a`); the two Gemini branches (`Gemini`,
`gemini-illuminatigame`, from the same scaffold commit: a different GUI and internal debug
accessors on ApFloat, `RawMantissa` handing out the live array); and the Windows-only build.
(Later the same day he merged `space-bunny` into master, deleted it, and asked for a license:
MIT.)

## 2026-10-09 — Session 5a: Natural.Jobs, and StartParse

Matthew took both recommendations, asked with previews: a job you step, and the general piece
in `Natural.Jobs` inside this project.

**`Natural.Jobs`** (`src/Natural/Jobs/Job.cs`, nothing from the numbers). `Job.Start(body)` takes
an iterator that does a slice of work, yield-returns the units it did, and sets
`context.Result` (and `ExpectedWork`, for progress) before it ends. `Job<T>` has
`Continue(units)` and `Continue(TimeSpan)` (each returns whether it's finished, and a budget is
overrun by the last step only), `RunToEnd`, `Result`, `WorkDone`, `Steps`, `Progress` (never
down, at most 0.99 until the end, exactly 1 after), `IsComplete` and `Dispose`.
`Job<T, TCertificate>` adds `Certificate`. Nothing runs until the first Continue. A fault is
rethrown on every later call; work that ends without its result (or certificate) is a fault;
Continue from inside the work, or from two threads at once, is refused.

**`ApFloat.StartParse(text, precision, mode, provider)`** returns `Job<ApFloat, DecimalCertificate>`.
The syntax is checked at once (FormatException, or OverflowException for an exponent past a
long); the arithmetic waits for Continue, and a value whose binary exponent overflows throws from
Continue with Parse's message. Units are limb steps: the digit loop (`ApInt.DecimalDigitSteps`)
counts `used + 2` a digit and yields every 4,096 or a little more (about 0.065 ms here); a round
or the exact route counts an estimate from its sizes (`RoundUnits`, `ExactUnits`), so a budget
means the same on any machine. `DecimalCertificate(Rounds, WorkingBits, Exact, Low, High)`: the
last round's ends, signed, Low ≤ exact ≤ High, and both round to the result when the rounds
settled it.

**One implementation.** `Parse` drains the same steps (`DecimalParse.RunToEnd`), and the one-shot
`TimesPowerOfTen` drains `TimesPowerOfTenSteps`, so a job and Parse can't disagree. The test
hooks (`LastRounds`, `LastExact`) are set only by the one-shot calls. A job keeps its rounds in
its certificate, and reads `ForcedRoute` when it starts, on the caller's thread.

**Cost**, old build against new, alternating, three rounds each: long parses unchanged (30,000
digits 0.74 s both ways). Short ones pay for a handful of small objects, about +0.18 µs: "0.1"
within the noise, 20 digits 1.62 → 1.79 µs, "1.5e300" at 256 bits 0.66 → 0.84 µs. The first
version cost twice that, because the progress estimate called the bitwise `FloorLog2Of5Times`
up to three times per parse. Estimates now use |n|·2.321929.

Measured on the way: below the cache, the exact route's restoring division keeps every quotient
bit (about 93,000 for 30,000 digits at e-3000, to keep 53), but it's cheap next to the digit loop
(0.91 s against 0.86 s at e0). `ExactUnits` was 60 times low there; corrected.

**Tests**: 506 → 555. `JobTests` (15, counting jobs with no numbers). `ParseJobTests` (34): job =
Parse for 300 limb-biased decimals in every mode at 8 precisions, with the certificate equal to
the hooks; stepped one unit at a time (exactly one step per call, progress monotone and ≥ 0.9
with the work done, against the oracle); budgets kept to the exact step end, learned from a
stepped twin; 30,000 digits in 10 ms slices against the oracle; certificates bracket the exact
value (150 past the cache); a tie goes to the exact route; a multi-round certificate's ends come
from the last round, and progress between rounds says a bigger round is coming; the digit loop's
count checked against BigInteger limb counts; threads; errors. The README's new sample was run
as written.

**Mutation check**: 23 mutations, all red in the end. Two survived at first and the tests were
tightened: a job writing the test hooks (my test's second job happened to put them back) and
progress ignoring the engine's next round (nothing looked between rounds). A resume that restarts
from zero hung the suite instead of failing it, since tests loop until the job finishes; every
such loop is capped now, and it goes red in 7 tests.

## 2026-10-10 — Session 5b: StartFormat, and double dabble as steps

**`x.StartFormat(format, provider, mode)`** returns `Job<string, DecimalCertificate>` and gives
exactly `x.ToString(format, provider, mode)`: `ToString` now drains the same steps. The formatting
code moved into `DecimalFormat` (a nested class in `ApFloat.Text.cs`, like 5a's `DecimalParse`):
`Scaled` (|x| × 10^t to an integer: the engine's steps, or the exact route as one step),
`ReadBack` (R's check), `Digits` (double dabble), and the formats on top (`DecimalExponent`,
`SignificantDigits`, `Scientific`, `Fixed`, `General`, `Shortest`, `ReadsBackWith`, `Custom`),
each an iterator yielding from the ones below. B, A and X are linear and take no steps. The
custom pattern is parsed once, up front (`CustomPattern`). The format string and the output's
length are checked when the job is made: F or a custom pattern on a value with more digits before
the point than a string can hold throws `OverflowException` at once, `ToString` included (it used
to throw the same type from deep inside `ShiftLeft`). `DecimalCertificate` gained `Scale`, the
power of ten the printed digits came from, so Low ≤ x × 10^Scale ≤ High; parses keep 0.

**Double dabble as steps**: `ApInt.DecimalStringSteps` counts `2·used + 1` limb steps a bit (the
two passes over the BCD words, plus one) and yields every 4,096 or a little more, ending on a
bit; reading the digits out counts one per word. `ApInt.ToString` drains it.

**Measured, old build against new, alternating, three rounds each (Release):**
- First try: long output got 50% slower (F0 of 1e30000 200 → 300 ms). An iterator keeps its
  locals in fields, so double dabble's two hot loops ran on memory. They now live in a plain
  method (`Dabble`) that the iterator calls a step at a time: 197 ms against 197–203 ms.
- Short formats paid about +0.5 µs at first, from about 20 small objects a call. Keeping each
  stage's answer in a field instead of a box saved little. Taking the exact route's single step
  directly in `Scaled`/`ReadBack` (no engine iterator, no closures; one shared
  `ExactFromTheStart` predicate decides) saved most: now 0.1 default 2.5 µs (same), R 18.5 µs
  (same), π E6 at 256 bits 1.31 → 1.42 µs, F4 0.75 → 0.90 µs, ApInt 20 digits 0.37 → 0.41 µs.

**Tests**: 555 → 581, `FormatJobTests` (26): job = ToString for 108 values (specials, huge
exponents, limb-biased randoms at 8 precisions) × 20 formats in random modes, with the
certificate equal to the hooks where the printed digits come from the last scaling; stepped one
unit at a time (F0 of ~1e3000, E2500 at 10,000 bits, R at 3,000 bits, a custom pattern), with
progress low after the first step, between 0.4 and 0.7 at half the work (one-scaling formats),
and ≥ 0.9 at the end; budgets to the exact step end; F0 of 1e30000 in 10 ms slices against
BigInteger; double dabble's own count against BigInteger digit counts; certificates bracket
x × 10^Scale for E, G and R past the cache; errors; hooks; threads. One test first took 17 s:
a custom pattern on 9.999999e99999 printed all 100,000 digits (2.3 s a call); the every-digit
formats are listed explicitly now.

**Mutation check**: 17 mutations, 16 red. Two needed the new progress assertions to go red (the
digits' estimate left out of a scaling's progress, and a flat estimate during double dabble).
The survivor: the one-shot `ToString` setting the test hooks from the printed scaling instead of
the last scaling or read-back. That differs only for R and G (shortest), where the last call is
whichever candidate check ran last, which nothing relies on and no test can sensibly pin. The
hooks keep the old meaning (the last decimal conversion done); a job's certificate is the
meaningful record.

## 2026-10-10 — Session 6a: IEEE status flags, through IeeeContext

Matthew went on to session 6 (5c parked). One question first, how flags reach the caller, and
he took the recommendation: **a context object**. The brief had recommended an `out
IeeeFlags` per call, but reading the code changed that before asking. An `out` parameter must
come before the optional rounding mode, so every flagged call would spell out the mode, and
every operation would need another overload in both its precision and format forms (about 20
by the end of session 6). A context is IEEE 754's own model (attributes plus sticky flags),
with no hidden state.

**`IeeeContext`** (`src/Natural/IeeeContext.cs`): constructed with an `IeeeFormat` or a
precision, and a rounding mode (`Mode`, settable, checked). `Add`, `Subtract`, `Multiply`,
`Divide`, and `Round(x)` (IEEE's convertFormat: `WithFormat`/`WithPrecision` with flags). `Flags`
is sticky and settable (save/restore); `ClearFlags()` lowers them. `IeeeFlags` is a `[Flags]`
enum of the five.

**Where they come from**: `RoundExact` now reports inexact (a dropped non-zero bit, or the
sticky bit). `RoundToFormat` reports Overflow (always with Inexact) and Underflow: tiny and
inexact, tiny judged after rounding, as x86 does. Only a value in the binade just under 2^emin
can round up out of tininess, so only that case pays for a second rounding. The operations
raise Invalid (∞ − ∞, 0 × ∞, 0/0, ∞/∞) and DivideByZero (finite non-zero / 0). NaN operands
raise nothing, since every NaN here is quiet. Each operation has one core taking
`ref IeeeFlags`. The static methods pass a throwaway and return the same results as before
(581 tests unchanged).

**Tests**: 581 → 592, `IeeeFlagsTests` (11). Flags are predicted independently from the exact
rational, the oracle's rounding with no exponent floor (overflow, tininess), and the operand
kinds. The cases: 20,000 binary64 pairs crowded at the subnormal and overflow edges, specials
included; every pair of values in three 6-bit formats (NaN and ∞ included) × 4 operations × 4
modes; precision contexts (never overflow or underflow); 2^-1022 − 2^-1076, which is Inexact
alone to nearest (it rounds up to the smallest normal) but Underflow toward zero (where
judging before rounding would say Underflow both times); an exact subnormal product raises
nothing; convertFormat cases; stickiness, save and restore; the mode; the constructors. The
README sample was run as written (.NET prints "Inexact, Overflow" in value order).

**Mutation check**: 15 mutations, all red: inexact ignoring the sticky bit or the half bit;
tininess before rounding, or only below the subnormals; underflow when exact; overflow without
inexact or without its own flag; 0/0 as a division by zero; each invalid case silent; a
rounding that overwrote earlier flags; a context that wasn't sticky; precision contexts always
inexact; the context's mode ignored. The machine was in its slow phase (about 30 s a run,
against 12 the day before).

## 2026-10-11 — Session 6b: fused multiply-add, rounding to integers, the remainder

All in `ApFloat.Operations.cs`, with .NET's names, because the generic math interfaces (order
item 6) will require exactly `Round`, `Floor`, `Ceiling`, `Truncate`, `FusedMultiplyAdd` and
`Ieee754Remainder`. Each is on the static API and on `IeeeContext`.

- **`FusedMultiplyAdd(a, b, c)`**, plus precision and format overloads. The product is exact
  (an odd significand times an odd one is odd, so already normalised), wrapped as a value, and
  handed to `Add`'s core, which rounds once into the target. `Add`'s zero rules then give IEEE's
  signed zeros, and its gap shortcut covers a c far below the product. Invalid for 0 × ∞
  (unless an operand is NaN, which goes through quietly) and for ∞ × y + ∞ of the other sign.
- **`Floor`, `Ceiling`, `Truncate`, `Round(x)` (ties to even, as Math.Round) and
  `Round(x, MidpointRounding)`** (all five of .NET's): exact, at x's precision. `RoundExact`
  with `minExp: 0` does the directed and the even ones. Ties away from zero are |x| + 1/2,
  truncated, which is exact. The result always fits x's precision (for e < 0 the integer is at
  most 2^(top+1) with top < p − 1). A value already integral is returned untouched, however
  large its exponent, and a zero result keeps x's sign (Ceiling(−0.3) = −0).
- **`Ieee754Remainder(x, y)`**: exact, at the larger precision (which it always fits: when
  |x| ≥ |y|/2 their exponents are close). On the common grid 2^e, X mod 2Y gives both X mod Y
  and the parity of floor(X/Y), which a tie needs. It's computed as (mx mod 2Y) × (2^k mod 2Y)
  mod 2Y, with 2^k by square-and-multiply. So 2^(10^12) mod 3 takes 0.3 ms instead of a
  terabit integer. remainder(x, ±∞) = x; remainder(∞, y) and remainder(x, 0) are NaN and Invalid.
- On the context: `FusedMultiplyAdd`, `RoundToIntegral(x)` (IEEE's roundToIntegralExact: the
  context's mode, Inexact when it changed x), `Ieee754Remainder` (exact, then into the
  target). **The context's `Round(x)` from 6a is now `Convert(x)`**, so that `Round` means
  rounding to an integer everywhere, as in .NET. It had existed for one day.

**Tests**: 592 → 615, `IeeeOperationsTests` (23), and `FloatOracle.RoundingFlags` now shared
(moved out of `IeeeFlagsTests`). FMA matches `Math.FusedMultiplyAdd` and
`MathF.FusedMultiplyAdd` bit for bit (20,000 each, with addends unrelated, nearly or exactly
cancelling, far below, or zero), and every special triple in every mode for Invalid. It also
matches exact rationals in every mode, at precisions to 200 and in three formats, flags
included, plus the classic (1 + 2^-30)² error term. Rounding matches `Math.Floor`, `Ceiling`,
`Truncate` and `Round` in all five `MidpointRounding`s, bit for bit (−0 included), on 9,000
doubles crowded at integers and halves. It matches BigInteger floor/ceiling/half-even/half-away
at any precision, and handles 3·2^(±10^12). The remainder matches `double.Ieee754Remainder` on
20,000 pairs (.NET's agreed everywhere). It's exact against rationals at mixed precisions,
with x's sign on a zero and |r| ≤ |y|/2. Across 2^100000 it's checked against BigInteger
outright; across 2^(10^12), r ≡ x (mod y) by BigInteger.ModPow. The README sample was run.

**Mutation check**: 19 mutations, all red: the FMA rounding its product first, each FMA invalid
case silent, a NaN addend checked late; Floor truncating, Round tying away, half-away dropping
[0.5, 1), half-away rounding to nearest, an unsigned zero, the precision lost; the remainder's
tie to odd, its zero unsigned, 2^k off by one, the parity lost (mod Y not 2Y), an infinite
divisor not special, Invalid silent, a squaring skipped; the context's RoundToIntegral not
inexact, or ignoring its mode.

## 2026-10-11 — Session 6c: square roots

The brief ran as written. Order item 3 is done.

- **`ApInt.Sqrt(n)`** (floor) and **`ApInt.SqrtRem(n)`** (root and n − root², like `DivRem`;
  the remainder is zero exactly for a square). A negative throws `ArithmeticException`.
  `Magnitude.Sqrt` is the pencil-and-paper method in base 2. It brings the radicand down two
  bits at a time and appends a 1 to the root r when the remainder holds 4r + 1, which it then
  subtracts. Everything is done in place, in arrays sized once: the remainder never exceeds 2r,
  so one limb more than the root holds it. No CPU multiply. `DivRem`'s one-bit shift was left
  alone (its hot loop); the root has its own shift.
- **`ApFloat.Sqrt(x)`** at x's precision, plus precision and format overloads, and
  **`ieee.Sqrt(x)`** on the context: one flagged core, as in 6b. x = m × 2^e is scaled to M of
  2(p + 2) bits or one more, with an even exponent. The integer root then has p + 2 bits or
  more, and a non-zero remainder is the sticky bit. A significand longer than that is shifted
  *down* instead, and since m is odd that always drops a 1, which is sticky too. So the cost
  follows the precision asked for, not x's: √ of a 100,000-bit value to 53 bits is cheap.
  √(±0) = ±0 with no flag, √(+∞) = +∞, √ of anything below zero (−∞ included) is NaN and Invalid,
  and NaN is quiet.
- **Cost**: quadratic, like division. Against `Divide` of two full-precision operands at the same
  precision: 0.03 against 0.02 ms at 53 bits, 0.27 against 0.10 at 256, 6.6 against 4.7 at 4096,
  107 against 73 ms at 16,384. That's about twice the limb passes per bit (shift, trial, compare,
  subtract, shift the root). Order item 7's territory if it ever matters.

**Tests**: 615 → 630, `SqrtTests` (15). The integer root checks every n to 5000, 3000 biased
random values, squares and their neighbours (k² − 1, k², k² + 2k), 2^j and 2^j ± 1 to j = 300, and
a 12,800-bit value, all against BigInteger Newton (`FloatOracle.IntegerSqrt`) and r² ≤ n < (r + 1)².
The float root:
- matches `Math.Sqrt` bit for bit on 20,000 doubles (subnormals, exact squares, and "hard"
  squares: s² rounded, for s of 53 bits or an odd 54, so the root sits a hair from a double or
  a midpoint). Flags too: Inexact exactly when r² ≠ x.
- matches `MathF.Sqrt` on 20,000 floats, and `Half.Sqrt` on all 65,536 halves. (.NET's goes
  through float, and 24 ≥ 2 × 11 + 2, so its double rounding is harmless.)
- in every mode, at precisions 1–200 and in four formats, matches `FloatOracle.SqrtOf`, flags
  included. `SqrtOf` is the root itself when it's rational, otherwise (2a + 1)/2^(t+1) with
  a = floor(√x · 2^t) of p + 8 bits or more. √x lies strictly inside (a, a + 1)/2^t, and every
  rounding boundary is a multiple of 2^-t, so that rational rounds, and compares with any
  boundary, exactly as √x does. The radicands include exact midpoints' squares (true ties) and
  near-ties.
- passes a second check that takes no root at all: x must lie between the squares of the rounding
  boundaries around r (the neighbours for a directed mode, the midpoints for nearest, and on a
  midpoint r must be the even one).
- passes every value of three 6-bit formats in every mode. With two exponent bits the smallest
  normal is 1, so the roots of subnormals are tiny and Underflow is exercised.
- handles 2^(±10^12) exponents (√(m·2^2k) = √m·2^k, and odd exponents), overflow and underflow
  into binary64 from outside its range (an exact subnormal root raises nothing), a 100,000-bit
  radicand at 53 bits, and √(3^20000) exactly.

**Mutation check**: 21 mutations: 20 red, and one survivor, which is equivalent. Red for the
integer root: trial 4r or 2r + 1, one bit of each pair dropped, fits only when strictly above, the
top pair missed for an odd bit length, the remainder a limb short, the shift carrying the wrong
bits between limbs, and a negative not throwing. Red for the float root: the exponent's parity
not fixed, the sticky bit dropping the remainder, or dropping the shifted-out bits (only the
100,000-bit test caught that one: it matters only when the kept top half is a perfect square), a
root of p bits, √(−0) losing its sign or raising Invalid, a negative silent, √(−∞) = +∞, NaN
raising Invalid, `Sqrt(x)` at the default precision, and the context ignoring its mode or not
sticky (I added the stickiness check before the battery, once I saw nothing tested it).
**The survivor is a root of p + 1 bits**, which is enough. The half bit is still a root bit, and
every rounding boundary falls on a whole unit of the root, so none lies strictly between root
and root + 1, where the true value is. p + 2 is one bit of slack, kept to match `Divide`, whose
p + 2 is the same slack. The machine was in its fast phase (12 s a run).

## 2026-10-11 — Session 7: parsing straight into an IEEE format

Matthew answered the brief's questions in one round, all as recommended:
- **Shape**: static `Parse`/`TryParse(s, IeeeFormat, mode, provider)` with no flags, plus
  `ieee.Parse(s)` with them.
- **Huge exponents**: in a format, an exponent past a long saturates, as `double.Parse` does; with a
  precision it still throws.
- **Jobs**: `StartParse(s, IeeeFormat, mode)` too.

Measured before asking. Today's route, `Parse(s, 53).WithFormat(Binary64)`, gives 2·2^-1074 for
text just above 5·2^-1075, where `double.Parse` gives 3·2^-1074: the double rounding is real.
1e±10^18 cost 5.9 ms at 53 bits.

**Found reading the engine, not in the brief: its acceptance has to agree on the range.** Two
interval ends that round to the same format value can still differ on tininess (just under 2^emin,
one is tiny at the precision and the other rounds up out of it) or on overflow (toward zero, both
give the largest finite value, one by overflowing). The flags hang on that. So in a format,
`TimesPowerOfTenSteps` takes a `range` callback, `RangeOf`, which judges tiny and overflow as
`RoundToFormat` does. A round settles only when the ends agree on the value *and* the range. Both
are roundings, so monotone, and agreeing ends settle them for every value between.

**The pieces**:
- Parsing goes to an `ApFloat.Target` instead of a precision, all the way down: `PrepareParse`,
  `ScanDecimal`, the hex/binary path, `DecimalParse`, `FromDecimalSteps` and `FromDecimalExact`
  all end in `target.Round`. Formatting's read-back goes through the same pieces at x's precision,
  unchanged.
- **Inexact on the engine route** comes from the digits, since the interval can't say whether the
  text was exact. `IsExactly(r, q, s)`: with q = Q·2^t (Q odd), r must have exponent s + t and odd
  part Q·5^s (or Q/5^-s), and the exponent and bit lengths reject nearly everything before any
  multiply. The exact route's flags come straight from its rounding.
- **Settling early** (`SettlesEarly`): with D significant digits, 10^(D−1+n) ≤ |value| < 10^(D+n).
  3.321 is under log2 10, so far past 2^(emax+1) it overflows and under half the smallest subnormal
  it rounds as anything there does. Either way it's rounded as a one-bit stand-in to get the mode's
  answer and the flags. Zero text is zero. 1e±10^18 into binary64 went from 5.9 ms to under a
  microsecond. An exponent past a long comes back from `ParseExponent` as long's limit on its side,
  so it settles the same way. Hex exponents are clamped to ±2^62, which every format's range sits
  well inside.
- `ieee.Parse(s, provider)` raises Inexact, Overflow, Underflow; NaN and infinity raise nothing; a
  failed parse throws and leaves the flags alone.

**Old against new** (the recipe, alternated three times, best of five each): parsing 2000 short
texts at 53 bits took 15.5–16.3 ms on both builds, and 41 ms at 256 bits on both; "R" of 2000
doubles took 216–224 ms on both; 1e100000 took 0.18–0.19 ms on both; 3001 digits took 13.2–13.5 ms
on both. No cost to the precision path.

**Tests**: 630 → 643, `ParseFormatTests` (13). They cover:
- `double.Parse` bit for bit on 9,000 texts: "R"/"E16"/"E25"/"G17" of biased doubles, midpoints
  between neighbours written out in full and a hair either side, and random digit strings.
- `float.Parse` on 9,000, and `Half.Parse` on 6,000. It agreed with the oracle everywhere, as
  expected, since it shares double's parsing code.
- Every mode in eight formats, binary128 and three 6-bit ones included: `EncodeRational` for the
  bits, `RoundingFlags` for the flags, the static result equal to the context's.
- Every midpoint of three 6-bit formats, exactly and a hair either side.
- The engine forced on at three boundaries where neighbouring texts share a value but not their
  flags: 2^-1022 − 2^-1076 to nearest, 2^-1022 − 2^-1075 rounding up, and 2^1024 toward zero. The
  nudges run from 10^-20 of a digit (the first round settles it) to 10^-60 (its interval straddles
  the edge), both signs, against the oracle and the exact route.
- Exact texts settled by the engine (10^4200 × 10^-4200 is 1, with no flag, and `LastExact` false),
  and 10^4096 exact in a 10,000-bit format.
- Saturation for both signs in every mode, including zero mantissas, hex past a long, and the
  precision path still throwing.
- Hex and binary text into formats against the oracle; the context (sticky, its mode, a failed parse
  raising nothing); `StartParse` into a format against `Parse`, the engine included.

The first version of the mode test took 1 min 42 s. Binary128's midpoints written out in full run
to thousands of digits, and the parse is quadratic in the digits, so binary128 keeps only short
random texts (which still reach the engine past 10^±4096, near both edges) and the class runs in
about 12 s.

**Mutation check**: 21 mutations, 20 red at first, and **one survivor, which was a real hole: the
context's `Parse` not sticky.** My "sticky" check parsed "1e-400" after "0.1", but "1e-400" raises
Inexact by itself, so the expected flags came out the same either way. The test now follows with an
exact parse ("0.5") and an overflow, so only stickiness explains the result, and the mutation went
red. Red from the start:
- the engine ignoring the range, or getting no range callback;
- `RangeOf` never rounding at the edges, or judging tininess before rounding;
- the engine's ends rounded to the precision rather than into the format. At first only the
  forced-route tests caught that: binary128's subnormals are 34 of its 9,900 decades, so random
  texts rarely land there. Half the random texts now crowd each format's edges, and the default
  route catches it too.
- settled results always inexact, `IsExactly` forgetting the trailing zeros or with a one-sided
  length test, settled Underflow or Overflow not raised, the exact route's flags lost;
- the exact route rounding twice (precision, then format), hex skipping the format;
- the early-out margins too loose either way, an early zero losing its sign, an exponent past a
  long throwing in a format;
- the context ignoring its mode, and `StartParse` or the static `Parse` into a format using only
  its precision.

The fast phase held (13–19 s a run).

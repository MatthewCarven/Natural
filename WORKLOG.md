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

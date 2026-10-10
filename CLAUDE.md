# Natural — arbitrary-precision numbers, built from bits

(Started as ADPDT, "Arbitrary Data Precision Data Type". The folder was renamed Natural in
October 2026, and Claude's memory for it moved with it.)

A hand-built arbitrary-precision integer for C# (`Natural.ApInt`), where the
arithmetic is done with **bitwise operations**, not by leaning on the CPU's add
and multiply or on `System.Numerics.BigInteger`. That's the point of the project,
so keep it that way: new arithmetic is built on `Magnitude.FullAdd` (or on other
bitwise primitives), and `BigInteger` appears only in the tests, as the oracle.

## Layout

- `src/Natural/Magnitude.cs` — magnitude arithmetic on little-endian `uint[]` limbs
  (trimmed; zero is the empty array). `FullAdd` is the one-word ripple adder
  (XOR = sum without carries, AND = carries generated, shift and repeat);
  subtract is `a + ~b + 1`; multiply is shift-and-add; divide is restoring
  binary long division (shift in a bit, subtract if it fits), in place; the square root is
  the same digit by digit, two bits at a time (subtract 4r + 1 if it fits), in place too.
- `src/Natural/ApInt.cs` — the public struct: sign + magnitude, operators,
  conversions, comparison. `default(ApInt)` is zero; there is no negative zero.
- `src/Natural/ApInt.Text.cs` — parse (decimal via `x*10 = (x<<3)+(x<<1)`, hex,
  binary) and format (decimal via double dabble, eight BCD digits per `uint`).
- `src/Natural/ApFloat.cs` — IEEE-style arbitrary-precision float: ±m × 2^e, m odd,
  `long` exponent, precision per value. All rounding goes through `RoundExact`
  (compute exact, round once). Each operation takes a precision (unbounded exponent)
  or an `IeeeFormat` (rounded once into that format's range too, via `RoundToFormat`,
  which the byte encoder also uses). The operators use the precision form.
- `src/Natural/ApFloat.Operations.cs` — IEEE's other operations, with .NET's names (generic math
  will ask for them): `FusedMultiplyAdd` (exact product, then `Add`'s core: one rounding),
  `Sqrt` (the integer root of the significand scaled to 2(p + 2) bits and an even exponent, the
  remainder as the sticky bit; a longer significand is shifted down into the sticky bit, so the
  cost follows the precision asked for),
  `Floor`/`Ceiling`/`Truncate`/`Round` (+ `MidpointRounding`; exact, at x's precision, which
  always fits), and `Ieee754Remainder` (exact, at the larger precision; X mod 2Y gives the
  remainder and the quotient's parity, with 2^k mod 2Y by square-and-multiply, so 2^(10^12)
  mod 3 is instant).
- `src/Natural/IeeeContext.cs` — `IeeeFlags` and `IeeeContext` (Matthew's choice, 2026-10-10):
  a target (a format or a precision), a settable rounding mode, and sticky flags. Its methods
  call ApFloat's flagged cores (`Add`/`Multiply`/`Divide(..., Target, mode, ref IeeeFlags)`);
  the static methods pass a throwaway. Inexact comes from `RoundExact`; overflow and underflow
  from `RoundToFormat` (tiny judged after rounding, as x86); invalid and divide-by-zero from
  the operations' special cases. New operations go on both the static API and the context.
  `ieee.Parse(s)` is convertFromDecimalCharacter (hex and binary text too).
  The context's `Convert(x)` is IEEE's convertFormat (it was `Round` for a day, until
  `ApFloat.Round` came to mean rounding to an integer); `RoundToIntegral(x)` is
  roundToIntegralExact, in the context's mode, raising Inexact.
- `src/Natural/IeeeFormat.cs` — an IEEE binary format as (exponent bits, precision).
  `IeeeFormat.Binary(k)` is binary{k} per IEEE 754-2008 §3.6. Its width formula is
  done in integers, from k^8's bit length.
- `src/Natural/ApFloat.Ieee.cs` — one encoder/decoder for any `IeeeFormat`, working on
  the bit pattern as little-endian words. It backs `ToIeeeBytes`/`FromIeeeBytes`
  (little-endian by default) and the `Half`/`float`/`double` conversions. Encoding
  rounds once, straight to the format (subnormals via `RoundExact`'s `minExp`). NaN
  always encodes as the one quiet NaN.
- `src/Natural/ApFloat.Text.cs` — decimal text both ways, `IFormattable`/`ISpanFormattable`/
  `IParsable`. The default `ToString()` is always scientific with every digit the precision
  carries (`DecimalDigitsFor(p)`, 17 for 53 bits), so a column lines up (Matthew's choice).
  Output starts from the exact value (m × 5^k / 10^k) and rounds once through `RoundExact`,
  ties to even (not .NET's away-from-zero). Parse builds digits × 10^exp exactly and rounds
  once. Powers of five are cached below 5^4096. Parsing goes to a `Target` (a precision, or an
  `IeeeFormat` since session 7, rounded once straight into it, as double.Parse does). In a format,
  text far out of range is settled from its digit count and exponent (`SettlesEarly`), and an
  exponent past a long saturates (Matthew's choice); with a precision it throws. Inexact for a
  result the engine settled comes from the digits (`IsExactly`), since the interval can't say.
  Formatting is `DecimalFormat`: each format is
  an iterator over stages (`Scaled`, `ReadBack`, `Digits`), and `ToString` runs it to the end.
- `src/Natural/ApFloat.Certified.cs` — the certified interval engine (Ziv's strategy), for
  X × 10^n past the cache: bounds on 5^n by square-and-multiply with directed rounding, both
  ends rounded to the target, accepted when they agree; otherwise double the working bits,
  and hand over to the exact route once that's cheaper. In a format the ends must also agree on
  `RangeOf` (tiny, overflowing, or neither): two ends can round to the same value and still
  differ there, and Underflow and Overflow hang on it. Text's two cores (`FromDecimal`,
  `ScaledToInteger`) go through it, so `Parse` has no cap on decimal exponents: it refuses only
  a value whose binary exponent wouldn't fit a long. Test hooks: `ForcedRoute`, `LastRounds`,
  `LastExact` (thread-static). `TimesPowerOfTenSteps` is the engine as steps (one per round);
  the one-shot `TimesPowerOfTen` drains it and sets the hooks.
- `src/Natural/Jobs/Job.cs` — `Natural.Jobs`, knowing nothing of numbers: long work as a job the
  caller steps by units or time (`Continue`), with `Progress`, `Result` and an optional
  certificate. The work is an iterator that yield-returns the units it just did. Matthew wants
  this reusable in other projects, so keep numbers out of it.
- `src/Natural/ApFloat.Jobs.cs` — `StartParse` (to a precision or into a format) and `StartFormat`, a parse or a format as a
  job, and `DecimalCertificate` (with `Scale` for a format). `Parse` and `ToString` run the same
  steps (`DecimalParse`, `DecimalFormat`), so a job and the one-shot call can't disagree. Units
  are limb steps: counted in the digit loop and in double dabble (`ApInt.DecimalStringSteps`),
  estimated from sizes for a round or the exact route. Two rules from measuring, A against B:
  estimates must stay cheap (a short parse pays for them), and **hot loops stay out of
  iterators** (an iterator's locals are fields: double dabble ran 50% slower inside one), so
  an iterator calls a plain method for each step's work (`Dabble`, `TimesTenPlus`).
- `tests/Natural.Tests` — xUnit. `Oracle.cs` converts to/from `BigInteger` through
  raw magnitude bytes, so arithmetic tests don't depend on the text code.
  `FloatOracle.cs` is ApFloat's reference: exact rationals rounded by long division
  (no code shared with `RoundExact`), an IEEE encoder, and biased random bit patterns.
  The hardware (`double`, `float`, `Half`) is the second oracle. `CertifiedTests.cs` pins
  values for exponents like 10^7 that were computed offline with exact Python integers.
  `IeeeFlagsTests.cs` predicts every flag from the exact rational and the oracle's rounding
  with no exponent floor (every pair in three 6-bit formats, every mode, is the backstop);
  `FloatOracle.RoundingFlags` is that predictor, shared. `IeeeOperationsTests.cs` checks 6b's
  operations against the hardware (`Math.FusedMultiplyAdd`, `Math.Round(x, MidpointRounding)`,
  `double.Ieee754Remainder`) bit for bit, and against exact rationals at any precision.
  `SqrtTests.cs` checks the integer root against BigInteger Newton (`FloatOracle.IntegerSqrt`) and
  the float root against `Math.Sqrt`, `MathF.Sqrt` and every `Half`. In every mode and format it
  uses `FloatOracle.SqrtOf`, a rational that rounds exactly as the irrational root does. A
  squaring check (x between the squared rounding boundaries around the result) takes no root at all.
  `ParseFormatTests.cs` checks parsing into a format against `double.Parse`, `float.Parse` and
  `Half.Parse` bit for bit, and against the text's exact rational (`EncodeRational`,
  `RoundingFlags`) in every mode and format. Its texts include midpoints written out in full and a
  hair either side. It forces the engine on where its ends must agree on tininess or overflow.
  Midpoints of binary128 run to thousands of digits; keep them out of random loops (1 m 42 s).
  `JobTests.cs` checks `Natural.Jobs` on counting jobs; `ParseJobTests.cs` and
  `FormatJobTests.cs` check that a job gives what `Parse`/`ToString` gives however it's stepped,
  and that progress tracks the work. Cap every `while (!job.Continue(...))` loop in a test, or a
  resume that restarts hangs the suite instead of failing it.
- `Natural_Playground/` — a WinForms calculator over ApFloat that takes the result apart
  (significand, exponent, ulp, every format, and a check against `double`). It's in the
  solution, so the solution builds on Windows only. `FloatReport` makes the text with no UI:
  run it from a file-based app with `#:property TargetFramework=net10.0-windows`,
  `#:property PublishAot=false` and `#:project` on the playground, through reflection.
- `README.md` — the public front door. Its samples were run once; keep them compiling and
  its test count current.

## Commands

```
dotnet test Natural.slnx
dotnet run -c Release tests/bench/timing.cs            # decimal text timings (-- quick skips the exact route)
```

## Conventions

- Shift semantics match `BigInteger`: `>>` floors toward minus infinity for negatives.
- Division matches C#: `/` truncates toward zero, `%` takes the dividend's sign
  (so `-7 / 2 == -3` but `-7 >> 1 == -4`, exactly as with `long`).
- Random test operands are biased to all-ones / all-zero limbs on purpose — that's
  where carry and borrow bugs live. Keep that bias in any new randomized test.
- Before trusting a new green suite, break the code on purpose once and watch it go red.

## Recipes (the scratchpad dies with each chat; rebuild these from here)

- **Mutation battery**: a Python script in the scratchpad, written with the Write tool. A list
  of `(name, [(file, old, new), ...])`, each `old` an exact string found exactly once (try `\n`,
  then `\r\n`). For each: read the bytes, apply, run `dotnet test Natural.slnx --nologo` with
  `subprocess.run(..., timeout=240)`, report RED (with the failing tests) / green / BUILD ERROR /
  TIMEOUT, and restore the bytes in a `finally`. Run it with `run_in_background`, in batches
  under ten minutes (about 10–20 mutations at 12–30 s each), and touch nothing in `src` or
  `tests` while it runs. Afterwards check the originals are back before committing. A
  survivor is a hole until shown equivalent; say which in WORKLOG.
- **Old against new timing**: `git archive --format=tar -o <scratch>/old.tar HEAD src/Natural`,
  extract with `tar -xf`, then two file-based apps that differ only in their `#:project` line
  (old copy, live tree), built once with `dotnet build -c Release <file>.cs` and run
  alternately three times each (`dotnet run -c Release --no-build`). This machine has two
  speeds; one A-then-B pair proves nothing.
- **README samples**: paste the block into a file-based app verbatim, run it, and print the
  values the comments claim.

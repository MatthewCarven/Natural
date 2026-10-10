# Natural

Arbitrary-precision integers and binary floating-point numbers for .NET 10, with the
arithmetic built out of **bitwise operations**.

Not `System.Numerics.BigInteger` with more digits. The addition, subtraction,
multiplication and division are all built from XOR, AND, OR, NOT and shifts, not from the
CPU's own add and multiply instructions. (Plain integer arithmetic still counts lengths and
exponents, and decimal output adds 3 to BCD digits that can't carry.) `FullAdd` is the
single primitive every other operation is expressed in terms of:

> XOR is the sum with the carries left out; AND is exactly where carries are generated.
> Shift the carries up one place and add them in again, until there are none left.

`BigInteger` appears in this repository only in the tests, as the oracle to check the
hand-built arithmetic against.

Started as ADPDT (Arbitrary Data Precision Data Type).

## The two types

| | |
|---|---|
| **`ApInt`** | Arbitrary-precision signed integer. Sign + magnitude; `default` is zero; there is no negative zero. |
| **`ApFloat`** | Arbitrary-precision binary float with IEEE 754 behaviour. ±m × 2^e with m odd, a `long` exponent, and precision carried per value. |
| **`IeeeFormat`** | An IEEE binary interchange format as `(exponent bits, precision)` — binary16 through binary480768, or any custom pair. |

`ApFloat` is correctly rounded: every operation computes the exact result and rounds it
**once**, in any of the four IEEE rounding modes, with signed zeros, infinities and NaN.

## Quick start

```csharp
using Natural;

ApInt big = (ApInt.One << 200) + 1;
big.ToString("X");     // 100000000000000000000000000000000000000000000000001
big.ToString();        // 1606938044258990275541962092341162602522202993782792835301377
(big / 3).ToString();  // 535646014752996758513987364113720867507400997927597611767125
```

Division matches C# (`/` truncates toward zero, `%` takes the dividend's sign) and `>>`
floors, exactly as with `long`:

```csharp
((ApInt)(-7)) / 2;     // -3
((ApInt)(-7)) % 2;     // -1
((ApInt)(-7)) >> 1;    // -4
```

```csharp
ApFloat x = ApFloat.Parse("0.1");     // 256 bits by default
ApFloat y = ApFloat.Parse("0.2");

(x + y).ToString("R");               // 0.3     -- at 256 bits, which is closer than double gets
((ApFloat)1 / 3).ToString("R");
// 0.333333333333333333333333333333333333333333333333333333333333333333333333333335
```

## Precision is per value, and it is the operands' precision that bites

Every `ApFloat` carries its own precision; the **operators** round their result to the
larger of their operands' precisions. The static `Add`/`Subtract`/`Multiply`/`Divide`
take the precision explicitly, and the `IeeeFormat` overloads take a format.

This matters more than it looks. Rounding the *result* to 53 bits is not the same as
computing in 53 bits, because the operands are already values at whatever precision they
were parsed or constructed at:

```csharp
// Parsed and summed at 53 bits -- this is double's answer, bit for bit.
ApFloat.Add(ApFloat.Parse("0.1", 53), ApFloat.Parse("0.2", 53), 53).ToString("R");
// 0.30000000000000004      (0.1 + 0.2 in C# gives the same)

// Parsed at the default 256 bits, then the sum rounded to 53. Different, and correctly so.
ApFloat.Add(ApFloat.Parse("0.1"), ApFloat.Parse("0.2"), 53).ToString("R");
// 0.3
```

`DecimalDigitsFor` gives the significant decimal digits a precision is worth — 17 for 53
bits, 79 for 256, 156 for 512. The default `ToString()` prints exactly that many, always in
scientific notation, so a right-aligned column of values lines up on the point and the
exponent.

## Rounding once into an IEEE format

The exponent here is unbounded, which is the point — but it also means an unbounded-exponent
result is not always the same as what the hardware of a given width would produce. For
exact `double` behaviour, including subnormals and overflow, compute in the format:

```csharp
// bit for bit the hardware's own multiply, everywhere
ApFloat.Multiply(1.0000000000000002, 0.5, IeeeFormat.Binary64)
    .Equals((ApFloat)(1.0000000000000002 * 0.5));   // true

// ...and the flip side: where the format overflows to infinity, the unbounded form need not.
ApFloat.Multiply(1e300, 1e300, IeeeFormat.Binary64).ToString("R");  // ∞
ApFloat.Multiply(1e300, 1e300, 256).ToString("E6");                 // 1.000000E+600
```

The 53-bit *operators* (unbounded exponent) still round a second time when a result lands
among `double`'s subnormals — roughly 1% of products and quotients can differ in the last
place. That is documented on the type; the format overloads are the fix.

## IEEE status flags

IEEE 754's five flags (inexact, underflow, overflow, divide-by-zero, invalid) come from an
`IeeeContext`: an object you own that holds the target (a format, or a precision), the
rounding mode, and the flags, which stay raised until you clear them. The static methods give
the same results without flags.

```csharp
var ieee = new IeeeContext(IeeeFormat.Binary64);   // or new IeeeContext(precision: 256)

ApFloat third = ieee.Divide(1.0, 3.0);
IeeeFlags flags = ieee.Flags;                      // Inexact

ieee.Divide(1.0, 0.0);                             // +∞, exactly
flags = ieee.Flags;                                // Inexact, DivideByZero: they stay raised

ieee.ClearFlags();
ieee.Mode = RoundingMode.TowardZero;
ieee.Multiply(1e300, 1e300);                       // the largest finite double, toward zero
flags = ieee.Flags;                                // Inexact, Overflow
```

Underflow means tiny and inexact, with tiny judged after rounding (as x86 does; IEEE allows
either). With a precision the exponent is unbounded, so only inexact, divide-by-zero and
invalid can be raised.

The rest of IEEE's basic operations, with .NET's names, are on both `ApFloat` and the context:

```csharp
double x = 1 + Math.ScaleB(1, -30);
ApFloat error = ApFloat.FusedMultiplyAdd(x, x, -(x * x), IeeeFormat.Binary64);   // 2^-60: what x * x rounded off

ApFloat.Floor(-2.5);                                  // -3
ApFloat.Round(2.5);                                   // 2: ties to even, as Math.Round
ApFloat.Round(2.5, MidpointRounding.AwayFromZero);    // 3
ApFloat.Ieee754Remainder(7, 2);                       // -1: 7/2 = 3.5 rounds to 4

var ieee = new IeeeContext(IeeeFormat.Binary64, RoundingMode.TowardPositive);
ieee.RoundToIntegral(2.5);                            // 3, and Inexact
ieee.Convert(ApFloat.Parse("0.1"));                   // 0.1 rounded up into a double, and Inexact again
```

Rounding to an integer is exact, at the value's own precision. The remainder is exact too,
however far apart the exponents are: `Ieee754Remainder` of 2^(10^12) by 3 takes no time.

## Text

Formatting goes from the **exact** value and rounds once, in the given mode, ties to even.
`ApFloat` is `ISpanFormattable` and `IParsable<ApFloat>`; `ApInt` does decimal, hex and
binary.

```csharp
((ApFloat)5.25).ToString("E3");   // 5.250E+000
((ApFloat)5.25).ToString("F4");   // 5.2500
((ApFloat)5.25).ToString("R");    // 5.25      shortest text that reads back at this precision
((ApFloat)5.25).ToString("B");    // 101.01    the binary point; a column of these is a bar graph
((ApFloat)5.25).ToString("A");    // 0X1.5P+2  C's printf("%a")
((ApFloat)5.25).ToString("a3");   // 0x1.500p+2
((ApFloat)5.25).ToString("G");    // 5.25
```

`ApInt` formats to `"D"`, `"X"`/`"x"` and `"B"`/`"b"`; hex and binary carry no prefix, so
`-255` is `-FF`. Parsing accepts underscores between digits, and for `ApFloat` it takes
C's hex-float and binary-float syntax too:

```csharp
ApFloat.Parse("0x1.8p+1", 53).ToString("R");   // 3
ApFloat.Parse("0b101.01p-3").ToString("R");    // 0.65625
ApInt.Parse("1_000_000");                     // 1000000
```

## IEEE formats

`IeeeFormat.Binary(k)` is binary{k} per IEEE 754-2008 §3.6 — k = 16, 32, 64, and multiples
of 32 from 128 up. The width formula is worked out in integers from the bit length of k⁸,
so there is no floating-point logarithm anywhere in the library.

```csharp
IeeeFormat.Binary(1024).Width;       // 1024   (precision 997)
IeeeFormat.Binary(480768);           // the widest whose exponent field fits a long
new IeeeFormat(8, 8);                // bfloat16
```

Bytes in any format, either byte order:

```csharp
value.ToIeeeBytes(IeeeFormat.Binary32);          // little-endian by default
ApFloat.FromIeeeBytes(bytes, IeeeFormat.Binary64, bigEndian: true);
```

## Decimal exponents without a cap

Exact decimal conversion builds 5^|n| in full, which costs the square of n. Past a cache
of the small powers of five, `ApFloat` switches to a **certified interval engine** (Ziv's
strategy): bounds on 5^n by square-and-multiply with directed rounding, both ends rounded
to the target, accepted when they agree — and by monotonicity that certifies the exact
value. If they disagree, the working precision doubles and it goes again; past a point the
exact route is cheaper, and it takes over.

```csharp
ApFloat.Parse("1e1000000000000000000", 53).ToString("E6");   // 1.000000E+1000000000000000000
```

There was a cap at ±100,000 decimal exponents until this landed. It was removed, so `Parse`
now refuses only a value whose binary exponent would not fit a `long`.

## Long text, a slice at a time

A short decimal parses in microseconds whatever its exponent, but the cost grows with the square
of the number of digits, both ways: 30,000 digits take most of a second to parse, and printing
1e100000 in full (`F0`) takes 2.5 s. `StartParse` and `StartFormat` do the same work as `Parse`
and `ToString` as a job you hold and step, by work or by time, and give back the same result:

```csharp
var job = ApFloat.StartParse(text, precision: 256);

while (!job.Continue(TimeSpan.FromMilliseconds(50)))     // or job.Continue(100_000) units
    Console.WriteLine($"{job.Progress:P0}");

ApFloat x = job.Result;                    // what ApFloat.Parse(text, 256) gives
DecimalCertificate c = job.Certificate;    // rounds, working bits, exact or not, Low <= value <= High

var print = x.StartFormat("F0");          // the same for output: what x.ToString("F0") gives
while (!print.Continue(TimeSpan.FromMilliseconds(50))) { }
string digits = print.Result;
```

Work is counted in limb steps, so a budget of units means the same on any machine. The job
machinery is `Natural.Jobs`, which knows nothing about numbers: any loop written as an iterator
that yield-returns the work it just did can be a job.

## How it is tested

615 tests, `dotnet test Natural.slnx`. Three independent oracles, none of which shares code
with the thing under test:

- **`BigInteger`**, converted through raw magnitude bytes so the arithmetic tests don't
  depend on the text code.
- **`FloatOracle`** — exact rationals rounded by `BigInteger` long division, with an
  independent IEEE encoder and biased random bit patterns. It rounds with none of the
  production rounding code, so the two have to agree bit for bit.
- **The hardware** — `double`, `float` and `Half` are checked bit for bit, with results
  crowded around the subnormal and overflow boundaries.
- Plus values pinned offline with exact Python integers for the certified engine.

Random operands are deliberately biased toward all-ones and all-zero limbs, because that is
where carry and borrow bugs live.

This is not a claim of speed. Building the adder from XOR and AND rather than using the CPU's
add instruction is slower by design, and making it faster (Karatsuba, a bit-parallel adder)
is on the list but not done. What the approach buys is a result you can read and check: every
result is computed exactly and rounded exactly once, at a precision you choose.

## Build

```
dotnet test Natural.slnx                                        # 615 tests
dotnet run -c Release tests/bench/timing.cs                     # decimal text timings
```

Requires .NET 10. No package dependencies. The solution includes the playground, which is
WinForms, so it builds on Windows only.

## Layout

```
src/Natural/            the library
  Magnitude.cs          limb arithmetic on little-endian uint[]; FullAdd is the primitive
  ApInt.cs              sign + magnitude, operators, conversions, comparison
  ApInt.Text.cs         parse and format
  ApFloat.cs            ±m × 2^e, RoundExact as the one rounding point
  ApFloat.Ieee.cs       one encoder/decoder for any IeeeFormat
  ApFloat.Text.cs       decimal, hex and binary text
  ApFloat.Certified.cs  the certified interval engine
  ApFloat.Operations.cs fused multiply-add, rounding to integers, the remainder
  ApFloat.Jobs.cs       StartParse and StartFormat: text as a job, and its certificate
  IeeeFormat.cs         an IEEE format as (exponent bits, precision)
  IeeeContext.cs        the IEEE status flags, and a context that raises them
  Jobs/Job.cs           Natural.Jobs: long work stepped by units or time
tests/Natural.Tests/    xUnit, and the oracles
tests/bench/            timing.cs, a file-based app
Natural_Playground/     a WinForms calculator that takes results apart
```

## Status

Arbitrary-precision integers and binary floats are implemented and tested, with IEEE status
flags, fused multiply-add, rounding to integers and the remainder, and long parsing and
formatting run as resumable jobs. Not yet built, in the order they are planned: square roots
(`ApInt` and `ApFloat`); parsing into an `IeeeFormat`; `ApInt` bitwise
operators then `Pow`/`ModPow`/`Gcd`; generic math interfaces; speed; transcendentals.

See `TODO.md` for the full list and `CLAUDE.md` for the layout and conventions in more
detail.

## License

MIT. See `LICENSE`.
# Natural — Arbitrary-Precision Numbers, Built from Bits

**Natural** is a hand-crafted arbitrary-precision numeric arithmetic library for C# (.NET 10).

The core philosophy of the project is that **arithmetic is performed using bitwise operations**, simulating hardware-level digital logic rather than leaning on the CPU's native addition or multiplication instructions (`+`, `*`) or on `System.Numerics.BigInteger`. `BigInteger` is used solely in the test suite as an independent verification oracle.

---

## Highlights

- **Pure Bitwise Arithmetic**: All math is built on low-level bitwise operations (`XOR`, `AND`, `OR`, `NOT`, and shifts).
- **`Natural.ApInt`**: Arbitrary-precision signed integers stored as sign + little-endian 32-bit `uint[]` limbs.
- **`Natural.ApFloat`**: IEEE 754-style arbitrary-precision binary floating-point numbers ($\pm m \times 2^e$ with odd significand $m$, signed 64-bit exponent $e$, and per-value precision; default 256 bits).
- **IEEE 754 Compliance & Interoperability**:
  - Full support for `Binary16` (Half), `Binary32` (Single), `Binary64` (Double), `Binary128` (Quad), `Binary256`, and standard `Binary(k)` formats up to `Binary480768`.
  - Canonical Quiet NaN encoding and exact conversions to/from .NET `Half`, `float`, and `double`.
  - Four IEEE rounding modes: `ToNearestEven`, `TowardZero`, `TowardPositive`, `TowardNegative`.
- **Certified Interval Engine (Ziv's Strategy)**: Fast, rigorously certified decimal-to-binary and binary-to-decimal conversions for astronomical exponents ($10^{100000}$ to $10^{10^{18}}$ in milliseconds).
- **Interactive Debugging Playground**: A Windows Forms calculator exposing deep engine internals, limb arrays, IEEE bitfield decompositions, and certified engine diagnostics.

---

## How It Works

### 1. The Hardware Emulation Layer (`Magnitude`)

All operations on raw magnitudes (`uint[]` limbs, little-endian, trimmed) avoid CPU arithmetic primitives:

- **Ripple-Carry Adder (`FullAdd`)**:
  $$\text{sum} = a \oplus b \quad (\text{sum without carries})$$
  $$\text{carries} = a \ \& \ b \quad (\text{carries generated})$$
  Carries are shifted left by 1 and added back in until no carries remain. Overflow out of bit 31 is caught and rippled to the next word.
- **Two's Complement Subtraction (`Subtract`)**:
  $$a - b = a + \sim b + 1$$
  Zero-extends $b$ and reuses the ripple adder, discarding the final carry out.
- **Shift-and-Add Multiplication (`Multiply`)**:
  Scans set bits of the multiplier and accumulates shifted copies of the multiplicand. Only 32 shifted copies are ever required, built lazily as needed.
- **Restoring Binary Long Division (`DivRem`)**:
  Shifts dividend bits top-first into a running remainder array. If the remainder $\ge$ divisor, it subtracts the divisor in-place and sets the quotient bit. Every quotient bit is uniquely 0 or 1.

### 2. Arbitrary-Precision Integers (`ApInt`)

- **Data Representation**: Immutable readonly struct holding `uint[]? _mag` and `bool _negative`. `default(ApInt)` is zero (no negative zero).
- **Text Conversion**:
  - **Parsing**: Hex (`0x`), binary (`0b`), and decimal (via $\text{acc} \times 10 = (\text{acc} \ll 3) + (\text{acc} \ll 1)$ through the ripple adder). Supports `_` digit separators.
  - **Formatting**: Decimal output uses **Double Dabble** (shift-and-add-3) packing 8 BCD digits per `uint` with SIMD-within-a-register (SWAR) masking—converting binary to decimal without any division.

### 3. IEEE-Style Floats (`ApFloat`)

- **Representation**: $\pm m \times 2^e$, where $m$ is an **odd** magnitude (ensuring unique canonical representations), $e$ is an unbiased signed `long` exponent, and precision is carried per value (default 256 bits, about 77 decimal digits).
- **Correct Rounding (`RoundExact`)**:
  Every operation computes the mathematically exact result using the integer core, and rounds **exactly once** according to the selected IEEE mode.
  - Remainder from division is preserved as a sticky bit.
  - Subnormals are handled seamlessly via a `minExp` floor.
- **Gap Shortcut**: In additions like $1 + 2^{-1000000}$, operands far below the rounding boundary are replaced with a single sticky bit, avoiding massive memory allocations for discarded precision.
- **Format-Bounded Arithmetic**:
  Operations can target either unbounded precision or an exact `IeeeFormat` (e.g. `ApFloat.Multiply(x, y, IeeeFormat.Binary64)` reproduces hardware `double` behavior bit-for-bit, including subnormals and overflows).

### 4. Certified Interval Engine (`ApFloat.Certified`)

Evaluating $X \times 10^n$ for enormous exponents (e.g., $10^{100000}$) by computing $5^{|n|}$ exactly is quadratic in $n$. 

The certified engine uses **Ziv's interval doubling strategy**:
1. Computes rigorous lower and upper bounds on $5^n$ using square-and-multiply with directed rounding ($\text{lo} \le 5^n \le \text{hi}$) at $W$ working bits.
2. Multiplies or divides $X$ by the bounds, rounding outward.
3. Rounds both endpoints to the target precision.
4. If both ends produce the identical representable value, the result is **certified**.
5. If they differ (e.g., straddling a rounding boundary), $W$ is doubled. If $W$ grows beyond an eighth of $5^n$'s bit length, it hands off to the exact integer route.

---

## Project Structure

```
Natural/
├── src/
│   └── Natural/
│       ├── Magnitude.cs          # Raw limb bitwise arithmetic (FullAdd, DivRem, Multiply)
│       ├── ApInt.cs              # Public integer struct: operators, conversions, comparison
│       ├── ApInt.Text.cs         # Parse (acc*10) & format (Double Dabble BCD)
│       ├── ApFloat.cs            # Public float struct: ±m × 2^e, RoundExact, operators
│       ├── IeeeFormat.cs         # IEEE 754 format specifications (Binary16..Binary480768)
│       ├── ApFloat.Ieee.cs       # IEEE byte encoder/decoder, Half/float/double conversions
│       ├── ApFloat.Text.cs       # Decimal/hex/binary text formatting & uncapped parser
│       └── ApFloat.Certified.cs   # Certified interval engine (Ziv's strategy for huge 10^n)
├── tests/
│   ├── Natural.Tests/            # 483 xUnit tests against BigInteger & rational oracles
│   ├── bench/
│   │   └── timing.cs             # Standalone performance benchmark for huge exponents
│   └── reference/                # Python scripts generating offline reference vectors
├── Natural_Playground/           # Windows Forms debugging calculator & inspection tool
└── Natural.slnx                  # Solution file (.NET 10)
```

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)

### Building the Solution
```bash
dotnet build Natural.slnx
```

### Running the Test Suite
The test suite consists of 483 tests cross-checked against `BigInteger` and rational division oracles with randomized, limb-biased bit patterns (stressing ripple carries and borrows):
```bash
dotnet test Natural.slnx
```

### Running Benchmarks
Run the decimal text timings benchmark:
```bash
# Full benchmark
dotnet run -c Release tests/bench/timing.cs

# Fast mode (skips the slow exact fallback route)
dotnet run -c Release tests/bench/timing.cs -- quick
```

### Launching the Playground GUI
Launch the Windows Forms interactive calculator to inspect limbs, IEEE bitfields, and internal states:
```bash
dotnet run --project Natural_Playground
```

---

## The Natural Playground

The playground application provides two dedicated inspection environments:

1. **`ApInt` Debugger**:
   - Live parsing for Decimal, Hex (`0x`), and Binary (`0b`) with underscore digit separators.
   - Operations: `+`, `-`, `*`, `/`, `%`, `DivRem`, bit shifts (`<<`, `>>`), `Abs`, and `-A`.
   - Inspection of sign, total bit length, limb count, trailing zero bits, and byte size.
   - Interactive **32-bit Limb Table** displaying values in decimal, hex, and nibble-separated binary.

2. **`ApFloat` Debugger**:
   - Custom precision selection (with presets for Half, Single, Double, Quad, 256-bit default, and 1024-bit).
   - Selection of 4 IEEE rounding modes and format targets (`Binary16` to `Binary256`).
   - Multiple simultaneous outputs: Aligned Scientific (`ToString()`), Shortest Round-Trip (`"R"`), Fixed Point (`"F6"`), Hex Float (`"A"`), and Binary Point (`"B"`).
   - Mathematical decomposition display: $\pm m \times 2^e$, top bit place value, and significand limb array.
   - **IEEE 754 Bitfield Breakdown**: Decodes sign bit, biased exponent, fraction bits, category (Normal, Subnormal, Zero, Infinity, NaN), and hex byte dumps.
   - **Certified Diagnostics**: Live reporting of interval doubling rounds, fallback status, and microsecond execution timing.

---

## Roadmap

1. **Resumable Jobs (`Job<T>`)**: Steppable, work-budgeted execution for pausing and continuing long operations (e.g., 30,000+ digit strings).
2. **Additional IEEE Operations**: `FusedMultiplyAdd`, `Sqrt` (via integer square root), `Floor`/`Ceiling`/`Truncate`/`Round`, IEEE remainder, and IEEE status flags.
3. **`ApInt` Bitwise Operators**: `&`, `|`, `^`, `~` (with two's complement semantics matching `BigInteger`), followed by `Pow`, `ModPow`, and `Gcd`.
4. **Generic Math**: Support for `INumber<T>`, `IBinaryInteger<T>`, and `IFloatingPointIeee754<T>`.
5. **Arithmetic Optimizations**: Bit-parallel adder and Karatsuba multiplication (preserving the pure bitwise constraint).
6. **Transcendentals**: High-precision `exp`, `log`, `sin`, $\pi$ using error-bounded certified intervals.

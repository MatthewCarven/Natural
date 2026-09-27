# TODO

## Next — `ApFloat` (design agreed 2026-09-27)
Arbitrary precision in the arithmetic, IEEE 754 in behaviour and in the byte format.
- [ ] **Representation** (MPFR-style): sign, `ApInt` significand, `long` exponent,
      precision in bits per value; flags for ±0, ±∞, NaN. No bias in memory — the
      exponent is signed.
- [ ] **+, −, ×, correctly rounded**: compute exact with `ApInt`, round once.
      Round-half-to-even by default, plus toward zero / +∞ / −∞. Result precision =
      max of the operands' (overloads to choose it).
- [ ] **÷**: significand `DivRem` for the quotient bits, the remainder as the sticky
      bit for rounding.
- [ ] **IEEE `binary{k}` encode/decode** — Matthew's "max / 2" bias lives here.
      k = 16, 32, 64, then any multiple of 32 from 128 (IEEE 754-2008 §3.6):
      w = round(4·log2 k) − 13 exponent bits, bias 2^(w−1) − 1, p = k − w with the
      hidden bit. Subnormals, ±0, ±∞, NaN. Bit-exact round trips with
      `Half` / `float` / `double` as the test oracle; binary128 matches GCC's `__float128`.
- [ ] Decimal parse / format for `ApFloat`.

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

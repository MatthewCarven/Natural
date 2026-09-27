# TODO

## Next
- [ ] **Division** — `DivRem`, `/`, `%`. Natural first version in keeping with the
      project: binary long division (restoring shift-and-subtract, one quotient bit
      per step, built on `Magnitude.Subtract` / `Compare`). Decide truncation vs floor
      for negatives (C# `/` and `BigInteger` truncate toward zero; `%` takes the
      dividend's sign). Once it exists, decimal ToString could be done by repeated
      division as a cross-check on double dabble.

## Later
- [ ] Bitwise operators on `ApInt` (`&`, `|`, `^`, `~`) — two's-complement
      semantics for negatives, to match `BigInteger`.
- [ ] `Pow`, `ModPow`, `Gcd`, integer `Sqrt`.
- [ ] Beyond integers — fixed-point or rational/floating built on top of `ApInt`
      (the "precision" half of the name).
- [ ] Speed, if it ever matters: Karatsuba multiply; a bit-parallel adder that
      doesn't loop per carry. Keep the bitwise-only rule.
- [ ] Generic math interfaces (`INumber<ApInt>` etc.) if it's to be used as a drop-in.

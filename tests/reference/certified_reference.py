# Reference values for Natural's session 4 (the certified interval engine), computed with
# exact Python integers only: 5**N built in full, then one exact division per result.
# Independent of the C# engine (no bounds, no doubling).
import sys, time

N = int(sys.argv[1]) if len(sys.argv) > 1 else 10_000_000
K = (N << 128) // 0x4D104D427DE7FBCC47C4ACD605BE48BC
t0 = time.time()
A_N = 5 ** N
print(f"// 5**{N}: {A_N.bit_length()} bits in {time.time() - t0:.1f} s", file=sys.stderr)

_small = {}
def pow5(j):
    d = j - N
    if abs(d) <= 400:
        if d not in _small:
            _small[d] = A_N * 5 ** d if d >= 0 else A_N // 5 ** (-d)
        return _small[d]
    assert j < 50_000, j
    return 5 ** j

MODES = ["ToNearestEven", "TowardZero", "TowardPositive", "TowardNegative"]

def up(mode, negative, q, r, d):
    if mode == "ToNearestEven":
        c = (2 * r > d) - (2 * r < d)
        return c > 0 or (c == 0 and q & 1 == 1)
    if mode == "TowardZero":
        return False
    if mode == "TowardPositive":
        return not negative and r != 0
    return negative and r != 0

def round_bits(num, den, p, mode, negative):
    """num/den > 0 rounded to p bits: (n, e) with n odd, value n * 2^e."""
    k = num.bit_length() - den.bit_length()
    if k >= 0:
        if num < (den << k): k -= 1
    elif (num << -k) < den:
        k -= 1
    ulp = k - p + 1
    n_, d_ = (num, den << ulp) if ulp >= 0 else (num << -ulp, den)
    q, r = divmod(n_, d_)
    if up(mode, negative, q, r, d_): q += 1
    while q & 1 == 0:
        q >>= 1
        ulp += 1
    return q, ulp

def describe(negative, n, e):
    return f"{'-' if negative else '+'}0x{n:X}p{e}"

def parse(q, s, p, mode, negative):
    """q * 10^s rounded to p bits."""
    num, den = (q * pow5(s), 1) if s >= 0 else (q, pow5(-s))
    n, e = round_bits(num, den, p, mode, negative)
    return n, e + s

def ge_pow10(m, e, x):
    """m * 2^e >= 10^x?"""
    if x >= 0:
        f = pow5(x)
        return (m << (e - x)) >= f if e >= x else m >= (f << (x - e))
    g = m * pow5(-x)
    return True if e >= x else g >= (1 << (x - e))

LOG10_2 = 0x4D104D427DE7FBCC47C4ACD605BE48BC

def sci(m, e, k, mode, negative):
    """What ToString("E<k-1>") prints for the value m * 2^e: k significant digits."""
    top = m.bit_length() - 1 + e
    x = (top * LOG10_2) >> 128
    while ge_pow10(m, e, x + 1): x += 1
    while not ge_pow10(m, e, x): x -= 1
    t = k - 1 - x
    if t >= 0:
        num, den = m * pow5(t), 1
        sh = e + t
    else:
        num, den = m, pow5(-t)
        sh = e + t
    if sh >= 0: num <<= sh
    else: den <<= -sh
    q, r = divmod(num, den)
    if up(mode, negative, q, r, den): q += 1
    if q == 10 ** k:
        q //= 10
        x += 1
    d = str(q)
    body = d[0] + ("." + d[1:] if k > 1 else "")
    return f"{'-' if negative else ''}{body}E{'-' if x < 0 else '+'}{abs(x):03d}"

out = []
parses = [
    (f"1e{N}", 1, N, False),
    (f"1e-{N}", 1, -N, False),
    (f"-1.2345678901234567890123456789e{N}", 12345678901234567890123456789, N - 28, True),
    (f"9.99999999999999999999e-{N + 1}", 999999999999999999999, -(N + 1) - 20, False),
]
t0 = time.time()
rounded53 = {}
for text, q, s, neg in parses:
    for p in (53, 256):
        for mode in MODES:
            n, e = parse(q, s, p, mode, neg)
            out.append(f'    [InlineData("{text}", {p}, RoundingMode.{mode}, "{describe(neg, n, e)}")]')
            if p == 53 and mode == "ToNearestEven": rounded53[text] = (n, e, neg)
print(f"// parses: {time.time() - t0:.1f} s", file=sys.stderr)

out.append("")
t0 = time.time()
# Formatting: powers of two either side of 10^(+-10^7), and the parsed 53-bit values.
values = [(f"0x1p+{K}", 1, K, False), (f"0x1p-{K}", 1, -K, False)]
for text, (n, e, neg) in rounded53.items():
    values.append((f"{text} @53", n, e, neg))
for label, m, e, neg in values:
    for p, k in ((53, 17), (256, 79)):
        if "@53" in label and p != 53: continue
        out.append(f'    [InlineData("{label}", {p}, "E{k - 1}", RoundingMode.ToNearestEven, "{sci(m, e, k, "ToNearestEven", neg)}")]')
    for mode in MODES:
        out.append(f'    [InlineData("{label}", 53, "E5", RoundingMode.{mode}, "{sci(m, e, 6, mode, neg)}")]')
print(f"// formats: {time.time() - t0:.1f} s", file=sys.stderr)
print("\n".join(out))

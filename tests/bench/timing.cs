#:project ../../src/Natural/Natural.csproj
// Timing for Natural's decimal text. Run from the repo root:
//     dotnet run -c Release tests/bench/timing.cs            (both tables, about 30 s)
//     dotnet run -c Release tests/bench/timing.cs -- quick   (skips the slow exact route)
// The exact route and the interval engine are timed in the same process, alternating, because
// this machine runs at two speeds minute to minute: two separate runs prove nothing.
// Each figure is the best of its runs. Nothing here is asserted; the tests do that.
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Natural;

bool quick = args.Contains("quick");
var t = typeof(ApFloat);
var forced = t.GetField("ForcedRoute", BindingFlags.NonPublic | BindingFlags.Static)!;
var routeType = t.GetNestedType("Route", BindingFlags.NonPublic)!;
object auto = Enum.Parse(routeType, "Auto"), exact = Enum.Parse(routeType, "Exact");
var inv = CultureInfo.InvariantCulture;
ApFloat Parse(string s, int p) => ApFloat.Parse(s, p, RoundingMode.ToNearestEven, inv);

double Time(object route, Action a)
{
    forced.SetValue(null, route);      // thread-static: applies to this thread
    var sw = Stopwatch.StartNew();
    a();
    sw.Stop();
    forced.SetValue(null, auto);
    return sw.Elapsed.TotalSeconds;
}

string Show(double s) => s < 0.001 ? $"{s * 1e6:0} us" : s < 1 ? $"{s * 1e3:0.0} ms" : $"{s:0.00} s";
string Best(List<double> l) => l.Count == 0 ? "-" : Show(l.Min());

foreach (object r in new[] { exact, auto }) Time(r, () => Parse("1e5000", 53).ToString());   // JIT both routes

Console.WriteLine("Huge exponents: the exact route against the interval engine");
foreach (int p in new[] { 53, 256 })
{
    foreach (string text in new[] { "1e10000", "1e30000", "1e100000", "1e10000000", "1e1000000000000000000" })
    {
        long n = long.Parse(text[2..]);
        ApFloat x = Parse(text, p);
        // The exact route grows with the square of n: twice up to 1e30000, once at 1e100000
        // (53 bits only: that pair alone is 20 s), never past it.
        int exactRuns = quick ? 0 : n <= 30_000 ? 2 : n <= 100_000 && p == 53 ? 1 : 0;
        List<double> pe = [], pi = [], fe = [], fi = [];
        for (int i = 0; i < Math.Max(exactRuns, 3); i++)
        {
            if (i < exactRuns) pe.Add(Time(exact, () => Parse(text, p)));
            pi.Add(Time(auto, () => Parse(text, p)));
            if (i < exactRuns) fe.Add(Time(exact, () => x.ToString()));
            fi.Add(Time(auto, () => x.ToString()));
        }
        Console.WriteLine($"{p,4} bits | {text,-22} | Parse exact {Best(pe),9} | intervals {Best(pi),9} | ToString() exact {Best(fe),9} | intervals {Best(fi),9}");
    }
}

Console.WriteLine();
Console.WriteLine("Long digit strings: ApInt's decimal loop in, double dabble out (both quadratic)");
var rng = new Random(1);
foreach (int digits in new[] { 1_000, 10_000, 30_000 })
{
    var sb = new StringBuilder("1");
    for (int i = 1; i < digits; i++) sb.Append((char)('0' + rng.Next(10)));
    string text = $"{sb}e-{digits + 5000}";
    double parse = Time(auto, () => Parse(text, 53));
    ApFloat big = Parse("1e" + digits, 53);
    double f0 = Time(auto, () => big.ToString("F0", inv));
    Console.WriteLine($"{digits,6} digits | Parse of that many digits {Show(parse),9} | ToString(\"F0\") of 1e{digits} {Show(f0),9}");
}

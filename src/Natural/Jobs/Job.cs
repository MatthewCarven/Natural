using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Natural.Jobs;

// Long work the caller holds and runs a slice at a time, like a stream it reads from: it asks
// for up to so much work, or so much time, and gets control back in between. Nothing here
// knows about numbers. A job's work is an iterator: it does a slice, yield-returns how much
// it just did, and sets the result before it ends. The compiler keeps the loop's state
// between slices, so the resumable code reads like the one-shot code.
//
// Work is counted in units the body chooses, and counting them is what makes a budget mean
// the same thing on any machine: a time budget can't, since the same work takes different
// times from one minute to the next. Natural's text counts a unit per 32-bit limb step.

/// <summary>Starts jobs. See <see cref="Job{T}"/>.</summary>
public static class Job
{
    /// <summary>
    /// A job whose work is <paramref name="body"/>: an iterator that does a slice of the work,
    /// yield-returns the units it just did (zero or more), and sets
    /// <see cref="JobContext{T}.Result"/> before it ends. It may set
    /// <see cref="JobContext{T}.ExpectedWork"/> for <see cref="Job{T}.Progress"/>. Nothing runs
    /// until the first <see cref="Job{T}.Continue(long)"/>.
    /// </summary>
    public static Job<T> Start<T>(Func<JobContext<T>, IEnumerable<long>> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Job<T>(body);
    }

    /// <summary>
    /// As <see cref="Start{T}"/>, for a job that also hands back a certificate: what the result
    /// rests on, set with the result before the body ends.
    /// </summary>
    public static Job<T, TCertificate> Start<T, TCertificate>(Func<JobContext<T, TCertificate>, IEnumerable<long>> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Job<T, TCertificate>(body);
    }
}

/// <summary>
/// Long work, run a slice at a time. <see cref="Continue(long)"/> does up to a budget of work
/// and returns whether the job has finished; then <see cref="Result"/> is the answer.
/// Stopping is just not calling Continue again (and <see cref="Dispose"/> lets go of the work
/// at once). Not thread-safe: one call at a time, though the calls may come from different
/// threads. If the work throws, Continue throws it, and so does every later call.
/// </summary>
public class Job<T> : IDisposable
{
    /// <summary>Progress is held at or below this until the job finishes, whatever the estimate says.</summary>
    private const double MostBeforeTheEnd = 0.99;

    private readonly JobContext<T> _context;
    private Func<IEnumerable<long>>? _start;       // until the first step
    private IEnumerator<long>? _steps;             // while running
    private ExceptionDispatchInfo? _fault;
    private bool _running, _disposed;

    internal Job(Func<JobContext<T>, IEnumerable<long>> body) : this(new JobContext<T>(), body) { }

    private protected Job(JobContext<T> context, Func<JobContext<T>, IEnumerable<long>> body)
    {
        _context = context;
        _start = () => body(context);
    }

    /// <summary>Units of work done so far: the sum of what the steps have yielded.</summary>
    public long WorkDone { get; private set; }

    /// <summary>Steps taken so far: each is one slice of the body, between two yields.</summary>
    public long Steps { get; private set; }

    /// <summary>True once the body has ended and its result exists.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// 0 to 1: <see cref="WorkDone"/> over the body's <see cref="JobContext{T}.ExpectedWork"/>.
    /// It never goes down, stays at most 0.99 until the job finishes (an estimate can be short),
    /// and is exactly 1 once it has. With no estimate it stays 0 until the end.
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>The answer. Throws until <see cref="IsComplete"/>, or the work's own exception if it failed.</summary>
    public T Result
    {
        get
        {
            ThrowUnlessComplete();
            return _context.Result;
        }
    }

    /// <summary>
    /// Does up to <paramref name="units"/> of work: it takes steps while the work done in this
    /// call is less than the budget, so it may go over by at most the last step. 0 does nothing.
    /// Returns whether the job has finished.
    /// </summary>
    public bool Continue(long units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        Enter();
        try
        {
            long target = WorkDone > long.MaxValue - units ? long.MaxValue : WorkDone + units;
            while (WorkDone < target && Step()) { }
        }
        finally
        {
            _running = false;
        }
        return IsComplete;
    }

    /// <summary>
    /// Works for about <paramref name="time"/>: it takes steps until that much time has passed,
    /// so it may go over by the last step. Returns whether the job has finished.
    /// </summary>
    public bool Continue(TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(time, TimeSpan.Zero);
        Enter();
        try
        {
            long start = Stopwatch.GetTimestamp();
            while (Stopwatch.GetElapsedTime(start) < time && Step()) { }
        }
        finally
        {
            _running = false;
        }
        return IsComplete;
    }

    /// <summary>Runs the job to the end and returns its result.</summary>
    public T RunToEnd()
    {
        Continue(long.MaxValue);
        return Result;
    }

    /// <summary>Lets go of the work. Later calls to Continue throw; a finished job's result stays readable.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _start = null;
        _steps?.Dispose();
        _steps = null;
        GC.SuppressFinalize(this);
    }

    private protected void ThrowUnlessComplete()
    {
        _fault?.Throw();
        if (!IsComplete) throw new InvalidOperationException("The job hasn't finished: call Continue until it returns true.");
    }

    private void Enter()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _fault?.Throw();
        if (_running) throw new InvalidOperationException("The job is already running: Continue was called from inside its own work, or from two threads at once.");
        _running = true;
    }

    /// <summary>One step of the body. False once the job has finished.</summary>
    private bool Step()
    {
        if (IsComplete) return false;
        IEnumerator<long>? steps = _steps;
        bool more;
        try
        {
            if (steps is null)
            {
                _steps = steps = _start!().GetEnumerator();
                _start = null;
            }
            more = steps.MoveNext();
            if (more && steps.Current < 0)
                throw new InvalidOperationException($"A step reported {steps.Current} units of work; it can't be negative.");
            if (!more && _context.Missing is string missing)
                throw new InvalidOperationException($"The job's work ended without setting {missing}.");
        }
        catch (Exception e)
        {
            _fault = ExceptionDispatchInfo.Capture(e);
            _steps?.Dispose();
            _steps = null;
            throw;
        }

        if (!more)
        {
            steps.Dispose();
            _steps = null;
            IsComplete = true;
            Progress = 1;
            return false;
        }

        WorkDone += steps.Current;
        Steps++;
        long expected = _context.ExpectedWork;
        if (expected > 0) Progress = Math.Max(Progress, Math.Min((double)WorkDone / expected, MostBeforeTheEnd));
        return true;
    }
}

/// <summary>A job that also hands back a certificate: what its result rests on.</summary>
public sealed class Job<T, TCertificate> : Job<T>
{
    private readonly JobContext<T, TCertificate> _context;

    internal Job(Func<JobContext<T, TCertificate>, IEnumerable<long>> body) : this(new JobContext<T, TCertificate>(), body) { }

    private Job(JobContext<T, TCertificate> context, Func<JobContext<T, TCertificate>, IEnumerable<long>> body)
        : base(context, _ => body(context)) => _context = context;

    /// <summary>What the result rests on. Throws until the job is complete, as <see cref="Job{T}.Result"/> does.</summary>
    public TCertificate Certificate
    {
        get
        {
            ThrowUnlessComplete();
            return _context.Certificate;
        }
    }
}

/// <summary>What a job's work sees: where it puts its result, and its estimate of the work.</summary>
public class JobContext<T>
{
    private T _result = default!;

    internal JobContext() { }

    /// <summary>The answer. The work must set it before it ends.</summary>
    public T Result
    {
        get => _result;
        set
        {
            _result = value;
            HasResult = true;
        }
    }

    internal bool HasResult { get; private set; }

    /// <summary>
    /// The total work expected, in the units the steps yield: an estimate, which the work may
    /// revise at any time as it learns more. 0, the default, means no estimate.
    /// </summary>
    public long ExpectedWork { get; set; }

    /// <summary>What the work still has to set before it ends, or null.</summary>
    internal virtual string? Missing => HasResult ? null : "a result";
}

/// <summary>What the work of a job with a certificate sees.</summary>
public sealed class JobContext<T, TCertificate> : JobContext<T>
{
    private TCertificate _certificate = default!;

    internal JobContext() { }

    /// <summary>What the result rests on. The work must set it before it ends.</summary>
    public TCertificate Certificate
    {
        get => _certificate;
        set
        {
            _certificate = value;
            HasCertificate = true;
        }
    }

    private bool HasCertificate { get; set; }

    internal override string? Missing => HasCertificate ? base.Missing : "a certificate";
}

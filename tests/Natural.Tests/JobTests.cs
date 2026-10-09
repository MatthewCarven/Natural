using System.Diagnostics;
using Natural.Jobs;

namespace Natural.Tests;

/// <summary>
/// Natural.Jobs on its own, with no numbers: budgets, progress, results, faults and disposal,
/// on small counting jobs whose every step is known.
/// </summary>
public class JobTests
{
    /// <summary>Counts to n in steps of <paramref name="step"/> units, the last one shorter.</summary>
    private static IEnumerable<long> Count(JobContext<long> context, long n, long step)
    {
        context.ExpectedWork = n;
        long done = 0;
        while (done < n)
        {
            long take = Math.Min(step, n - done);
            done += take;
            yield return take;
        }
        context.Result = done;
    }

    [Fact]
    public void NothingRunsUntilTheFirstContinue()
    {
        bool ran = false;
        IEnumerable<long> Body(JobContext<int> context)
        {
            ran = true;
            context.Result = 7;
            yield return 1;
        }

        Job<int> job = Job.Start<int>(Body);
        Assert.False(ran);
        Assert.False(job.Continue(0));      // a zero budget takes no step
        Assert.False(ran);
        job.Continue(1);
        Assert.True(ran);
    }

    [Fact]
    public void ContinueStopsAfterTheStepThatCrossesTheBudget()
    {
        Job<long> job = Job.Start<long>(c => Count(c, 100, 7));
        Assert.False(job.Continue(10));     // 7 is under 10, so one more step: 14
        Assert.Equal(14, job.WorkDone);
        Assert.Equal(2, job.Steps);
        Assert.False(job.Continue(1));      // any budget takes at least one step
        Assert.Equal(21, job.WorkDone);
        Assert.False(job.Continue(14));     // exactly two steps' worth: stops on the budget
        Assert.Equal(35, job.WorkDone);
        Assert.False(job.Continue(0));
        Assert.Equal(35, job.WorkDone);
        Assert.True(job.Continue(1_000));   // runs out of work before the budget
        Assert.Equal(100, job.WorkDone);
        Assert.Equal(15, job.Steps);        // 14 of 7 and a 2
        Assert.Equal(100, job.Result);
    }

    [Fact]
    public void TheEndIsOnlyReachedByAskingForMore()
    {
        // After the last yield the job hasn't finished: the work's tail (setting the result)
        // runs on the next call, which takes no further step.
        Job<long> job = Job.Start<long>(c => Count(c, 21, 7));
        Assert.False(job.Continue(21));
        Assert.Equal(21, job.WorkDone);
        Assert.False(job.IsComplete);
        Assert.Throws<InvalidOperationException>(() => job.Result);
        Assert.True(job.Continue(1));
        Assert.Equal(3, job.Steps);
        Assert.Equal(21, job.Result);
        Assert.True(job.Continue(1));       // and stays finished
        Assert.Equal(21, job.RunToEnd());
    }

    [Fact]
    public void ProgressNeverGoesDownAndIsOneOnlyAtTheEnd()
    {
        // The estimate starts at half the truth and is corrected halfway, which would pull a
        // plain ratio down from 0.99 to 0.5.
        IEnumerable<long> Body(JobContext<long> context)
        {
            context.ExpectedWork = 50;
            for (int i = 1; i <= 100; i++)
            {
                if (i == 60) context.ExpectedWork = 100;
                yield return 1;
            }
            context.Result = 100;
        }

        Job<long> job = Job.Start<long>(Body);
        double last = 0;
        Assert.Equal(0, job.Progress);
        for (int calls = 0; !job.Continue(1); calls++)
        {
            Assert.True(calls < 1000, "the job never finished");
            Assert.True(job.Progress >= last, $"Progress went from {last} to {job.Progress}");
            Assert.True(job.Progress <= 0.99, $"Progress {job.Progress} before the end");
            last = job.Progress;
        }
        Assert.Equal(0.99, last);
        Assert.Equal(1, job.Progress);
    }

    [Fact]
    public void WithoutAnEstimateProgressWaitsForTheEnd()
    {
        IEnumerable<long> Body(JobContext<long> context)
        {
            yield return 5;
            yield return 5;
            context.Result = 10;
        }

        Job<long> job = Job.Start<long>(Body);
        job.Continue(1);
        Assert.Equal(0, job.Progress);
        job.RunToEnd();
        Assert.Equal(1, job.Progress);
    }

    [Fact]
    public void AFaultIsThrownAgainOnEveryLaterCall()
    {
        var boom = new InvalidDataException("step three");
        IEnumerable<long> Body(JobContext<long> context)
        {
            yield return 1;
            yield return 1;
            throw boom;
        }

        Job<long> job = Job.Start<long>(Body);
        Assert.Same(boom, Assert.Throws<InvalidDataException>(() => job.Continue(100)));
        Assert.Equal(2, job.WorkDone);
        Assert.Same(boom, Assert.Throws<InvalidDataException>(() => job.Continue(1)));
        Assert.Same(boom, Assert.Throws<InvalidDataException>(() => job.Result));
        Assert.Same(boom, Assert.Throws<InvalidDataException>(() => job.Continue(TimeSpan.FromSeconds(1))));
        Assert.False(job.IsComplete);
    }

    [Fact]
    public void WorkThatEndsWithoutItsResultIsAFault()
    {
        IEnumerable<long> NoResult(JobContext<long> context)
        {
            yield return 1;
        }

        Job<long> job = Job.Start<long>(NoResult);
        Assert.Contains("a result", Assert.Throws<InvalidOperationException>(() => job.RunToEnd()).Message);

        IEnumerable<long> NoCertificate(JobContext<long, string> context)
        {
            context.Result = 1;
            yield return 1;
        }

        Job<long, string> certified = Job.Start<long, string>(NoCertificate);
        Assert.Contains("a certificate", Assert.Throws<InvalidOperationException>(() => certified.RunToEnd()).Message);
    }

    [Fact]
    public void TheCertificateComesWithTheResult()
    {
        IEnumerable<long> Body(JobContext<long, string> context)
        {
            yield return 3;
            context.Result = 3;
            context.Certificate = "counted to three";
        }

        Job<long, string> job = Job.Start<long, string>(Body);
        Assert.Throws<InvalidOperationException>(() => job.Certificate);
        job.RunToEnd();
        Assert.Equal("counted to three", job.Certificate);
        Assert.Equal(3, job.Result);
    }

    [Fact]
    public void NegativeWorkIsRefused()
    {
        IEnumerable<long> Body(JobContext<long> context)
        {
            yield return -1;
            context.Result = 0;
        }

        Assert.Throws<InvalidOperationException>(() => Job.Start<long>(Body).Continue(1));
    }

    [Fact]
    public void DisposeLetsGoOfTheWork()
    {
        bool cleanedUp = false;
        IEnumerable<long> Body(JobContext<long> context)
        {
            try
            {
                yield return 1;
                yield return 1;
                context.Result = 2;
            }
            finally
            {
                cleanedUp = true;
            }
        }

        Job<long> job = Job.Start<long>(Body);
        job.Continue(1);
        job.Dispose();
        Assert.True(cleanedUp);
        Assert.Throws<ObjectDisposedException>(() => job.Continue(1));
        job.Dispose();                      // twice is fine

        Job<long> finished = Job.Start<long>(c => Count(c, 10, 3));
        finished.RunToEnd();
        finished.Dispose();
        Assert.Equal(10, finished.Result);  // a result stays readable
    }

    [Fact]
    public void ContinueFromInsideTheWorkIsRefused()
    {
        Job<long>? job = null;
        IEnumerable<long> Body(JobContext<long> context)
        {
            job!.Continue(1);
            context.Result = 0;
            yield return 1;
        }

        job = Job.Start<long>(Body);
        Assert.Contains("already running", Assert.Throws<InvalidOperationException>(() => job.Continue(1)).Message);
    }

    [Fact]
    public void ATimeBudgetReturnsAndTheJobResumes()
    {
        // 400 steps of about 0.1 ms each. Only the shape is asserted, not the timing: this
        // machine runs at two speeds.
        IEnumerable<long> Body(JobContext<long> context)
        {
            context.ExpectedWork = 400;
            for (int i = 0; i < 400; i++)
            {
                long start = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < 0.1) { }
                yield return 1;
            }
            context.Result = 400;
        }

        Job<long> job = Job.Start<long>(Body);
        Assert.False(job.Continue(TimeSpan.FromMilliseconds(2)));
        Assert.InRange(job.WorkDone, 1, 399);
        Assert.False(job.Continue(TimeSpan.Zero));    // no time, no step
        int calls = 1;
        while (!job.Continue(TimeSpan.FromMilliseconds(2))) Assert.True(++calls < 10_000, "the job never finished");
        Assert.True(calls > 2, $"{calls} calls");
        Assert.Equal(400, job.Result);
    }

    [Fact]
    public async Task AJobCanBeContinuedFromOtherThreads()
    {
        Job<long> job = Job.Start<long>(c => Count(c, 1000, 3));
        for (int calls = 0; !await Task.Run(() => job.Continue(50)); calls++)
            Assert.True(calls < 1000, "the job never finished");
        Assert.Equal(1000, job.Result);
    }

    [Fact]
    public void ArgumentsAreChecked()
    {
        Assert.Throws<ArgumentNullException>(() => Job.Start<long>(null!));
        Job<long> job = Job.Start<long>(c => Count(c, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.Continue(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.Continue(TimeSpan.FromTicks(-1)));
        Assert.Equal(0, job.WorkDone);
    }

    [Fact]
    public void WorkWithNoStepsFinishesOnTheFirstStep()
    {
        IEnumerable<long> Body(JobContext<string> context)
        {
            context.Result = "done";
            yield break;
        }

        Job<string> job = Job.Start<string>(Body);
        Assert.True(job.Continue(1));
        Assert.Equal(0, job.WorkDone);
        Assert.Equal(0, job.Steps);
        Assert.Equal("done", job.Result);
    }
}

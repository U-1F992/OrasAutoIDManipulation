using System.Diagnostics;

public class TimerStopwatch : IDisposable
{
    Stopwatch stopwatch = new Stopwatch();
    Timer timer;

    public TimerStopwatch(TimerCallback callback, object? state)
    {
        timer = new Timer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
    public void Submit(TimeSpan timeout)
    {
        if (disposedValue)
        {
            throw new ObjectDisposedException("TimerStopwatch");
        }
        if (!stopwatch.IsRunning)
        {
            throw new InvalidOperationException("Stopwatch is not running.");
        }

        var actual =
            timeout == Timeout.InfiniteTimeSpan
                ? Timeout.InfiniteTimeSpan // InfiniteTimeSpanは減算できない
            : timeout < stopwatch.Elapsed
                ? TimeSpan.Zero // タイマー開始からすでにtimeout以上経過している場合は即座に実行
                : timeout - stopwatch.Elapsed;
        var result = timer.Change(actual, Timeout.InfiniteTimeSpan);
        
        if (!result)
        {
            throw new Exception("Timer was not updated successfully.");
        }
    }
    public void Start()
    {
        if (disposedValue)
        {
            throw new ObjectDisposedException("TimerStopwatch");
        }
        if (stopwatch.IsRunning)
        {
            throw new InvalidOperationException("Stopwatch is already running.");
        }

        stopwatch.Start();
    }
    public void Reset()
    {
        if (disposedValue)
        {
            throw new ObjectDisposedException("TimerStopwatch");
        }

        stopwatch.Stop();
        stopwatch.Reset();
        Submit(Timeout.InfiniteTimeSpan);
    }

    #region IDisposable
    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                timer.Dispose();
            }

            disposedValue = true;
        }
    }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

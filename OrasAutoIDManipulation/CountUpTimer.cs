public class CountUpTimer
{
    public event EventHandler Elapsed = (sender, eventArgs) => { };
    
    Object lockObject = new Object();
    TimeSpan submitted = TimeSpan.MaxValue;

    public CountUpTimer()
    {
        // 初回のみ遅延があるため、コンストラクタで捨てておく
        // イベントを参照する際の何らか？
        var task = Start();
        Submit(TimeSpan.Zero);
        task.Wait();
    }

    public async Task Start() { await Start(CancellationToken.None); }
    public async Task Start(CancellationToken cancellationToken)
    {
        var elapsed = TimeSpan.Zero;
        lock (lockObject)
        {
            submitted = TimeSpan.MaxValue;  
        }
        var task = Task.Run(() =>
        {
            var interval = 10000000 / 1000;
            var next = DateTime.Now.Ticks + interval;

            while (elapsed < submitted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (next > DateTime.Now.Ticks)
                {
                    continue;
                }
                lock (lockObject)
                {
                    elapsed += TimeSpan.FromMilliseconds(1);
                }
                next += interval;
            }
            
            Elapsed(this, EventArgs.Empty);
        
        }, cancellationToken);
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            lock (lockObject)
            {
                submitted = TimeSpan.MaxValue;   
            }
        }
    }

    public void Submit(TimeSpan timeSpan)
    {
        lock (lockObject)
        {
            submitted = timeSpan;
        }
    }
}

public class CountUpTimer
{
    public event EventHandler Tick = (sender, eventArgs) => { };
    
    Object lockObject = new Object();
    TimeSpan elapsed = TimeSpan.Zero;
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
        lock (lockObject)
        {
            elapsed = TimeSpan.Zero;
        }
        var task = Task.Run(() =>
        {
            var interval = 10000000 / 1000;
            var next = DateTime.Now.Ticks + interval;

            while (elapsed < submitted)
            {
                if (next > DateTime.Now.Ticks)
                {
                    continue;
                }
                lock (lockObject)
                {
                    elapsed = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds + 1);
                }
                next += interval;
            }
            
            Tick(this, EventArgs.Empty);
        
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
                elapsed = TimeSpan.Zero;
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

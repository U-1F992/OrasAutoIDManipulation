using System.Diagnostics;
using Hogei;

class Viewer : IDisposable
{
    Task task;
    Process process;
    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// timeout以上同じ画像が表示されていたらビュアーを再起動する
    /// </summary>
    /// <param name="preview"></param>
    /// <param name="path"></param>
    /// <param name="timeout"></param>
    public Viewer(Preview preview, string path, TimeSpan timeout)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException();
        }
        process = Process.Start(path);
        var processName = process.ProcessName;
        
        var cancellationToken = cancellationTokenSource.Token;
        task = Task.Run(() =>
        {
            var stopwatch = new Stopwatch();
            var previous = preview.CurrentFrame;

            while (!cancellationToken.IsCancellationRequested)
            {
                using var current = preview.CurrentFrame;
                var similarEnough = previous.Contains(current, 0.999999);
                
                if (similarEnough && !stopwatch.IsRunning)
                {
                    stopwatch.Start();
                }
                if (!similarEnough && stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                    stopwatch.Reset();
                }

                if (stopwatch.Elapsed > timeout)
                {
                    Console.WriteLine("Similar for {0}, restart viewer", timeout);

                    process.Kill();
                    process.WaitForExit();
                    process = Process.Start(path);

                    stopwatch.Stop();
                    stopwatch.Reset();
                }

                previous.Dispose();
                previous = current.Clone();
            }
            previous.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            
        }, cancellationToken);
    }
    #region IDisposable
    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                cancellationTokenSource.Cancel();
                process.Kill();
                process.WaitForExit();
                process.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~Hoge()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
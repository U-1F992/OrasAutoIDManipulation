using System.Diagnostics;
using OpenCvSharp;
using Hogei;

class Application
{
    Whale whale;
    Preview preview;
    TessConfig tessConfig = new TessConfig("C:\\Program Files\\Tesseract-OCR\\tessdata\\", "eng", "0123456789", 3, 7);
    Rect rectAroundID = new Rect(1112, 40, 112, 35);
    
    public Application(Whale whale, Preview preview)
    {
        this.whale = whale;
        this.preview = preview;
    }

    public async Task Main((ushort tid, ushort sid) targetID, int maxAdvance)
    {
        var result = (ushort)(targetID.tid + 1);

        bool finished;
        TimeSpan waitTime = TimeSpan.Zero;
        (uint Seed, int Advance) target = (0, 0);

        var stopwatch = new Stopwatch();
        using var mainTimer = new TimerStopwatch(async _ =>
        {
            Console.WriteLine("=====\nStart: {0}", DateTime.Now);
            Console.WriteLine("target: {0}", waitTime.TotalMilliseconds);
            Console.WriteLine("elapsed: {0}", stopwatch.ElapsedMilliseconds);
            await whale.RunAsync(Sequences.skipOpening_1);

            var discard = new Operation[] { }
                .Concat(Sequences.selectMale)
                .Concat(Sequences.decideName_A)
                .Concat(Sequences.discardName).ToArray();
            for (var i = 0; i < target.Advance; i++)
            {
                await whale.RunAsync(discard);
            }
            var sequence = new Operation[] { }
                .Concat(Sequences.selectMale)
                .Concat(Sequences.decideName_Kirin)
                .Concat(Sequences.confirmName)
                .Concat(Sequences.skipOpening_2)
                .Concat(Sequences.showTrainerCard).ToArray();
            await whale.RunAsync(sequence);

#if DEBUG
            using var frame = preview.CurrentFrame;
            frame.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
#endif
            finished = true;
        }, null);
        using var subTimer = new TimerStopwatch(async _ =>
        {
            // 30秒前にホーム画面に戻る
            Console.WriteLine("=====\nBack to home: {0}", DateTime.Now);
            await whale.RunAsync(Sequences.reset);
        }, null);

        do
        {
            finished = false;
            mainTimer.Reset();
            subTimer.Reset();
            stopwatch.Reset();

            mainTimer.Start();
            subTimer.Start();
            stopwatch.Start();

            DateTime startTime;
            List<uint> pivotSeeds;
            do
            {
                startTime = DateTime.Now;
                try
                {
                    pivotSeeds = await GetPivotSeeds(rectAroundID);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);
                    pivotSeeds = new List<uint>();
                }
            } while (pivotSeeds.Count != 1);
            var pivotSeed = pivotSeeds[0];

            target = await pivotSeed.GetNextInitialSeed(targetID, maxAdvance, TimeSpan.FromMilliseconds(800000));
            waitTime = TimeSpan.FromMilliseconds((target.Seed > pivotSeed ? 0 : 0x100000000 + target.Seed) - pivotSeed);

            Console.WriteLine();
            Console.WriteLine("targetSeed startsAt            advance");
            Console.WriteLine("---------- ------------------- -------");
            Console.WriteLine("{0,8:X}   {1,19} {2}", target.Seed, startTime + waitTime, target.Advance);
            Console.WriteLine();

            mainTimer.Submit(waitTime);
            subTimer.Submit(waitTime - TimeSpan.FromSeconds(30));

            while (!finished)
            {
                await Task.Delay(500);
            }

            Console.WriteLine();
            Console.WriteLine("Seed     Gap");
            Console.WriteLine("----     ---");
            result.GetGap(target.Seed, target.Advance, 500).ForEach(pair =>
            {
                Console.WriteLine("{0,8:X} {1}", pair.Seed, pair.Gap);
            });
            Console.WriteLine();

            await whale.RunAsync(Sequences.reset);

            using var currentFrame = preview.CurrentFrame;
            result = currentFrame.GetCurrentID(rectAroundID, tessConfig);

        } while (result == targetID.tid);
    }

    async Task<List<uint>> GetPivotSeeds(Rect rectAroundID)
    {
        var tids = new List<ushort>();
        var stopwatch = new Stopwatch();

        var count = 0;
        var failed = false;
        var getID = new TimerCallback(async _ =>
        {
            if (count > 3)
            {
                return;
            }

            Console.WriteLine("=====\nelapsed: {0}", stopwatch.ElapsedMilliseconds);
            await whale.RunAsync(Sequences.getID);
            ushort id = 0;
            try
            {
                using var currentFrame = preview.CurrentFrame;
                id = currentFrame.GetCurrentID(rectAroundID, tessConfig);
            }
            catch (Exception exception)
            {
                failed = true;
                Console.Error.WriteLine(exception);
            }
            Console.WriteLine("TID: {0}", id);
            tids.Add(id);

            await whale.RunAsync(Sequences.reset);
            Interlocked.Increment(ref count);
        });

        stopwatch.Start();
        using var timer = new Timer(getID, null, TimeSpan.Zero, TimeSpan.FromMinutes(3));
        await Task.Delay(TimeSpan.FromMinutes(3 * 4));

        await whale.RunAsync(new Operation[]
        {
        new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
        new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(9000))
        });

        if (failed)
        {
            await whale.RunAsync(Sequences.reset);
            throw new Exception("Failed to get ID more than once.");
        }

        var results = tids.GetPivotSeedsFromTIDs(180000, 500);
        
        Console.WriteLine();
        Console.WriteLine("Seed     Gaps");
        Console.WriteLine("----     ----");
        results.ForEach(pair =>
        {
            Console.WriteLine("{0,8:X} {1}", pair.Seed, string.Join(",", pair.Gaps));
        });
        Console.WriteLine();

        return results.Select(pair => pair.Seed).ToList();
    }
}

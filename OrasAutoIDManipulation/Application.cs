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

    public async Task Main((ushort tid, ushort sid) targetID, int tolerance, int maxAdvance)
    {
        const double fps = 59.8261;

        var result = (ushort)(targetID.tid + 1);

        bool finished;
        bool canceled;
        TimeSpan waitTime = TimeSpan.Zero;
        (uint Seed, int Advance) target = (0, 0);

        var stopwatch = new Stopwatch();
        using var mainTimer = new TimerStopwatch(async _ =>
        {
            var list = new List<int>();
            var aroundIndicator = new Rect(1280, 489, 23, 23);

            var candidates = new List<uint>();
            for (var seed = target.Seed - tolerance; seed < target.Seed + tolerance + 1; seed++)
            {
                candidates.Add((uint)seed);
            }

            Console.WriteLine("=====\nStart: {0}", DateTime.Now);
            Console.WriteLine("target: {0}", waitTime.TotalMilliseconds);
            Console.WriteLine("elapsed: {0}", stopwatch.ElapsedMilliseconds);
            await whale.RunAsync(Sequences.skipOpening_1);

            var discard = new Operation[] { }
                .Concat(Sequences.selectMale)
                .Concat(Sequences.decideName_A)
                .Concat(Sequences.discardName).ToArray();

            // 20回は針読み
            for (var i = 0; i < 20; i++)
            {
                await whale.RunAsync(new Operation[]
                {
                    // 「きみは　おとこのこ？」
                    new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                    new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(1500)),
                    // 男の子に合わせてA
                    new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                    new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(1500))
                });

                Task<Mat>? task = null;
                await Task.WhenAll
                (
                    whale.RunAsync(new Operation[]
                    {
                        // 「なまえも　おしえて　くれるかい！」
                        new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                        new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(3000)),
                        // 名前入力へ遷移
                        // 初期位置の「あ」
                        new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                        new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(1000)),
                        // 「おわり」へ移動
                        new Operation(new KeySpecifier[] { KeySpecifier.Start_Down }, TimeSpan.FromMilliseconds(1000)),
                        new Operation(new KeySpecifier[] { KeySpecifier.Start_Up }, TimeSpan.FromMilliseconds(1000)),
                    }),
                    Task.Delay(TimeSpan.FromMilliseconds(5000))
                        .ContinueWith(_ =>
                        {
                            task = preview.GetLastFrame(aroundIndicator);
                        }),
                    Task.Delay(TimeSpan.FromSeconds(500 / fps))
                        .ContinueWith(_ =>
                        {
                            whale.Run(new Operation[]
                            {
                                // 名前を決定
                                new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                                new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(7000)),
                            });
                        })
                );
                if (task == null)
                {
                    // ここに侵入したら異常なので空の画像を挿す
                    task = Task.Run(() => preview.CurrentFrame);
                }
                using var indicator = await task;
                var pos = indicator.GetPosition();
                list.Add(pos);

                await whale.RunAsync(Sequences.discardName);
            }
            Console.WriteLine(string.Join(",", list));
            
            var initialSeeds = await list.GetInitialSeeds(candidates);

            Console.WriteLine();
            Console.WriteLine("InitialSeed Gap");
            Console.WriteLine("----------- ---");
            initialSeeds.ForEach(initialSeed =>
            {
                Console.WriteLine("{0,8:X}    {1}", initialSeed, (long)initialSeed - target.Seed);
            });
            // 針読み結果に目標seedが含まれない場合
            if (!initialSeeds.Contains(target.Seed))
            {
                finished = true;
                canceled = true;
                return;
            };

            for (var i = 0; i < target.Advance - 20; i++)
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
            canceled = false;
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
                    pivotSeeds = await GetPivotSeeds(rectAroundID, tolerance);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);
                    pivotSeeds = new List<uint>();
                }
            } while (pivotSeeds.Count != 1);
            var pivotSeed = pivotSeeds[0];
            
            await whale.RunAsync(new Operation[]
            {
                new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(9000))
            });

            target = await pivotSeed.GetNextInitialSeed(targetID, 20, maxAdvance, TimeSpan.FromMilliseconds(800000));
            waitTime = TimeSpan.FromMilliseconds(target.Seed - pivotSeed);

            Console.WriteLine();
            Console.WriteLine("TargetSeed StartsAt            Advance");
            Console.WriteLine("---------- ------------------- -------");
            Console.WriteLine("{0,8:X}   {1,19} {2}", target.Seed, startTime + waitTime, target.Advance);

            mainTimer.Submit(waitTime);
            subTimer.Submit(waitTime - TimeSpan.FromSeconds(30));

            while (!finished)
            {
                await Task.Delay(500);
            }
            if (canceled)
            {
                await whale.RunAsync(Sequences.reset);
                continue;
            }

            using var currentFrame = preview.CurrentFrame;
            try
            {
                result = currentFrame.GetCurrentID(rectAroundID, tessConfig);
                Console.WriteLine();
                Console.WriteLine("Seed     Gap");
                Console.WriteLine("----     ---");
                result.GetGap(target.Seed, target.Advance, tolerance).ForEach(pair =>
                {
                    Console.WriteLine("{0,8:X} {1}", pair.Seed, pair.Gap);
                });
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
            await whale.RunAsync(Sequences.reset);
        } while (result != targetID.tid);
    }

    async Task<List<uint>> GetPivotSeeds(Rect rectAroundID, int tolerance)
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

        if (failed)
        {
            await whale.RunAsync(Sequences.reset);
            throw new Exception("Failed to get ID more than once.");
        }

        var results = tids.GetPivotSeedsFromTIDs(180000, tolerance);

        Console.WriteLine();
        Console.WriteLine("Seed     Gaps");
        Console.WriteLine("----     ----");
        results.ForEach(pair =>
        {
            Console.WriteLine("{0,8:X} {1}", pair.Seed, string.Join(",", pair.Gaps));
        });

        return results.Select(pair => pair.Seed).ToList();
    }
}

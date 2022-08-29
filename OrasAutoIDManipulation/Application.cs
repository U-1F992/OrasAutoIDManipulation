using System.Diagnostics;
using OpenCvSharp;
using Hogei;

class Application
{
    Whale whale;
    Preview preview;
    TessConfig tessConfig = new TessConfig("C:\\Program Files\\Tesseract-OCR\\tessdata\\", "eng", "0123456789", 3, 7);

    /// <summary>
    /// トレーナーカード画面のIDを囲うRect
    /// </summary>
    static readonly Rect aroundID = new Rect(1112, 40, 112, 35);
    /// <summary>
    /// 虹色の進捗表示をちょうど囲うRect
    /// </summary>
    static readonly Rect aroundIndicator = new Rect(1280, 489, 23, 23);
    /// <summary>
    /// 3DSのFPS（出典不明）
    /// </summary>
    static readonly double fps = 59.8261;
    /// <summary>
    /// 針読みに使う回数
    /// </summary>
    static readonly int observations = 12;

    public Application(Whale whale, Preview preview)
    {
        this.whale = whale;
        this.preview = preview;
    }

    public async Task Main((ushort tid, ushort sid) targetID, int tolerance, int maxAdvance)
    {
        var result = (ushort)(targetID.tid + 1);
        do
        {
            TimeSpan waitTime = TimeSpan.Zero;
            var stopwatch = new Stopwatch();

            DateTime startTime;
            long actualElapsed = 0;

            var mainTimer = new CountUpTimer();
            var subTimer = new CountUpTimer();
            var cancellationTokenSource = new CancellationTokenSource();
            var mainTask = Task.CompletedTask;
            var subTask = Task.CompletedTask;

            // 基準seedを求める
            List<uint> pivotSeeds;
            do
            {
                startTime = DateTime.Now;

                cancellationTokenSource.Dispose();
                mainTask.Dispose();
                subTask.Dispose();

                cancellationTokenSource = new();
                mainTask = mainTimer.Start(cancellationTokenSource.Token);
                subTask = subTimer.Start(cancellationTokenSource.Token);
                stopwatch.Reset();
                stopwatch.Start();
                
                var pivot = await GetPivotSeedGapsPair(aroundID, 5, 180000, tolerance);
                Console.WriteLine();
                Console.WriteLine("Seed     Gaps");
                Console.WriteLine("----     ----");
                pivot.ForEach(pair =>
                {
                    Console.WriteLine("{0,8:X} {1}", pair.Seed, string.Join(",", pair.Gaps));
                });

                pivotSeeds = pivot.Select(pair => pair.Seed).ToList();

                if (pivotSeeds.Count != 1)
                {
                    cancellationTokenSource.Cancel();
                    try { Console.WriteLine("Cancel mainTask"); await mainTask; } catch (OperationCanceledException) { }
                    try { Console.WriteLine("Cancel subTask"); await subTask; } catch (OperationCanceledException) { }
                }

            } while (pivotSeeds.Count != 1);
            var pivotSeed = pivotSeeds[0];

            // 待機して起動
            // 針読みから初期seedを求める
            (uint Seed, int Advance) target;
            var continueFlag = false;
            do
            {
                // 誤操作で待機場所がズレないように、ゲームをロードして待機する
                await whale.RunAsync(Sequences.load);

                target = await pivotSeed.GetNextInitialSeed(targetID, observations, maxAdvance, stopwatch.Elapsed + TimeSpan.FromMinutes(2));
                waitTime = TimeSpan.FromMilliseconds(target.Seed - pivotSeed); // uint型変数同士では 0x0-0xFFFFFFFF=1 みたいな計算ができるので、万が一0をまたぐ際も大丈夫
                Console.WriteLine();
                Console.WriteLine("TargetSeed StartsAt            Advance");
                Console.WriteLine("---------- --------            -------");
                Console.WriteLine("{0,8:X}   {1,19} {2}", target.Seed, startTime + waitTime, target.Advance);

                // ゲームのロードを待機
                mainTimer.Submit(waitTime);
                subTimer.Submit(waitTime - TimeSpan.FromSeconds(25));
                
                // 起動直前にホーム画面に戻る
                await subTask;
                Console.WriteLine();
                Console.WriteLine("Back to home: {0}", DateTime.Now);
                await whale.RunAsync(Sequences.reset);

                // ゲームを起動する
                await mainTask;
                cancellationTokenSource.Dispose();
                mainTask.Dispose();
                subTask.Dispose();
                await Task.WhenAll
                (
                    whale.RunAsync(Sequences.load),
                    Task.Run(() =>
                    {
                        actualElapsed = stopwatch.ElapsedMilliseconds;
                        startTime = DateTime.Now;
                        
                        cancellationTokenSource = new();
                        mainTask = mainTimer.Start(cancellationTokenSource.Token);
                        subTask = subTimer.Start(cancellationTokenSource.Token);
                        stopwatch.Reset();
                        stopwatch.Start();
                    })
                );

                Console.WriteLine();
                Console.WriteLine("TargetWaitTime ActualElapsed");
                Console.WriteLine("-------------- -------------");
                Console.WriteLine("{0,14} {1,13}", waitTime.TotalMilliseconds, actualElapsed);

                await whale.RunAsync(Sequences.skipOpening_1);

                // 針読み結果
                var indicatorResults = await GetIndicatorResults(aroundIndicator, observations);
                Console.WriteLine();
                Console.WriteLine("IndicatorResult");
                Console.WriteLine("---------------");
                Console.WriteLine(string.Join(',', indicatorResults));

                List<uint> initialSeeds;
                if (indicatorResults.Count() == 0)
                {
                    initialSeeds = new();
                }
                else
                {
                    // 初期seed候補を絞る
                    var candidates = GetInitialSeedCandidates(target.Seed, tolerance);
                    initialSeeds = await indicatorResults.GetInitialSeeds(candidates);
                    Console.WriteLine();
                    Console.WriteLine("InitialSeed Gap");
                    Console.WriteLine("----------- ---");
                    initialSeeds.ForEach(initialSeed =>
                    {
                        Console.WriteLine("{0,8:X}    {1}", initialSeed, (long)initialSeed - target.Seed);
                    });
                }

                // 針読み結果に目標seedが含まれる場合、以降の消費へ進む
                if (initialSeeds.Contains(target.Seed))
                {
                    break;
                };

                await whale.RunAsync(Sequences.reset);
                if (initialSeeds.Count() != 1)
                {
                    // 初期seedの候補が1つに絞れていなければ、基準seedから求め直し
                    continueFlag = true;
                    break;
                }
                else
                {
                    // 1つに絞れていれば、それを基準seedとして次のseedを探す
                    pivotSeed = initialSeeds.First();
                }
            } while (true);
            
            // タイマーを破棄
            cancellationTokenSource.Cancel();
            try { Console.WriteLine("Cancel mainTask"); await mainTask; } catch (OperationCanceledException) { }
            try { Console.WriteLine("Cancel subTask"); await subTask; } catch (OperationCanceledException) { }

            if (continueFlag)
            {
                // 基準seedから求め直し
                continue;
            }

            for (var i = 0; i < target.Advance - observations; i++)
            {
                await whale.RunAsync
                (
                    new Operation[] { }
                        .Concat(Sequences.selectMale)
                        .Concat(Sequences.decideName_A).ToArray()
                );

                using var tmp = preview.CurrentFrame;
                using var trim = tmp.Clone(new Rect(1231, 266, 70, 35));
                using var yes = new Mat("1231-266-70-35.png");
                if (!tmp.Contains(yes))
                {
                    Console.WriteLine("PANIC : Action required.");
                    Console.ReadLine();
                }

                await whale.RunAsync
                (
                    new Operation[] { }
                        .Concat(Sequences.discardName).ToArray()
                );
            }
            await whale.RunAsync
            (
                new Operation[] { }
                    .Concat(Sequences.selectMale)
                    .Concat(Sequences.decideName_Kirin)
                    .Concat(Sequences.confirmName)
                    .Concat(Sequences.skipOpening_2)
                    .Concat(Sequences.showTrainerCard).ToArray()
            );

            using var currentFrame = preview.CurrentFrame;
#if DEBUG
            currentFrame.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
#endif
            try
            {
                result = currentFrame.GetCurrentID(aroundID, tessConfig);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
            if (result != targetID.tid)
            {
                await whale.RunAsync(Sequences.reset);
            }

        } while (result != targetID.tid);
    }

    /// <summary>
    /// interval±tolerance(ms)間隔でn回起動して、基準seedと誤差を取得する
    /// </summary>
    /// <param name="Seed"></param>
    /// <param name="aroundID"></param>
    /// <param name="interval"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    async Task<List<(uint Seed, List<long> Gaps)>> GetPivotSeedGapsPair(Rect aroundID, int n, int interval, int tolerance)
    {
        List<ushort> tids;

        Console.WriteLine();
        Console.WriteLine("Elapsed    TID");
        Console.WriteLine("-------    ---");

        do
        {
            tids = new();

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var stopwatch = new Stopwatch();
            var count = 0;
            var getID = new TimerCallback(async _ =>
            {
                if (count > (n - 1))
                {
                    return;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                await whale.RunAsync(Sequences.getID);
                ushort id = 0;
                try
                {
                    using var currentFrame = preview.CurrentFrame;
                    id = currentFrame.GetCurrentID(aroundID, tessConfig);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);

                    count = int.MaxValue;
                    cancellationTokenSource.Cancel();
                }
                Console.WriteLine("{0,10} {1}", elapsed, id);
                tids.Add(id);

                await whale.RunAsync(Sequences.reset);
                Interlocked.Increment(ref count);
            });

            stopwatch.Start();
            using var timer = new Timer(getID, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(interval * n), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("=== Failed to get ID. ===");
                await whale.RunAsync(Sequences.reset);
            }
        } while (tids.Count() != n);

        return tids.GetPivotSeedsFromTIDs(interval, tolerance);
    }
    List<uint> GetInitialSeedCandidates(uint targetSeed, int tolerance)
    {
        var candidates = new List<uint>();
        for (var seed = targetSeed - (uint)tolerance; seed < targetSeed + (uint)tolerance + (uint)1; seed++)
        {
            candidates.Add((uint)seed);
        }
        return candidates;
    }
    /// <summary>
    /// 「きみは　おとこのこ？」画面からobservations回針読みした結果を返す
    /// </summary>
    /// <param name="aroundIndicator"></param>
    /// <param name="observations"></param>
    /// <returns></returns>
    async Task<List<int>> GetIndicatorResults(Rect aroundIndicator, int observations)
    {
        var list = new List<int>();

        // observations回は針読み
        for (var i = 0; i < observations; i++)
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

            // 30秒間針が入力されなければキャンセルする
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var waitTimeout = Task.Delay(30000, cancellationToken).ContinueWith(_ => cancellationTokenSource.Cancel());

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
                        task = preview.GetLastFrame(aroundIndicator, cancellationToken);
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
                cancellationTokenSource.Cancel();
                task = Task.Run(() => new Mat(), cancellationToken);
            }

            try
            {
                using var indicator = await task;
                cancellationTokenSource.Cancel();
                var pos = indicator.GetPosition();
                list.Add(pos);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("GetIndicatorResults canceled.");
                return new List<int>();
            }

            await whale.RunAsync(Sequences.discardName);
        }
        return list;
    }
}

using System.Diagnostics;
using OpenCvSharp;
using Hogei;

class Application
{
    readonly Whale whale;
    readonly Preview preview;
    readonly LINENotify notifier;

    /// <summary>
    /// 目標IDペア
    /// </summary>
    readonly (ushort tid, ushort sid) targetID;
    /// <summary>
    /// 許容する操作の誤差
    /// </summary>
    readonly TimeSpan tolerance = TimeSpan.FromMilliseconds(500);
    /// <summary>
    /// 許容する最大消費数
    /// </summary>
    readonly int maxAdvance;
    /// <summary>
    /// IDを見る回数
    /// </summary>
    readonly int countObserveID = 5;
    /// <summary>
    /// ID観測間にかかる時間
    /// </summary>
    readonly TimeSpan interval = TimeSpan.FromSeconds(180);
    /// <summary>
    /// 針を読む回数
    /// </summary>
    readonly int countObserveIndicator = 12;

    /// <summary>
    /// 虹色の進捗表示をちょうど囲うRect
    /// </summary>
    readonly Rect aroundIndicator = new Rect(1280, 489, 23, 23);
    /// <summary>
    /// 3DSのFPS（出典不明）
    /// </summary>
    readonly double fps = 59.8261;

    public Application(Whale whale, Preview preview, LINENotify notifier)
    {
        this.whale = whale;
        this.preview = preview;
        this.notifier = notifier;
    }

    public async Task Run((ushort tid, ushort sid) targetID)
    {
        await notifier.SendAsync("ORAS ID調整を開始しました。");

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

            mainTimer.Elapsed += (sender, eventArgs) =>
            {
                // ゲームを起動する
                actualElapsed = stopwatch.ElapsedMilliseconds;
                startTime = DateTime.Now;

                cancellationTokenSource.Dispose();
                cancellationTokenSource = new();
                mainTask = mainTimer.Start(cancellationTokenSource.Token);
                subTask = subTimer.Start(cancellationTokenSource.Token);
                stopwatch.Reset();
                stopwatch.Start();

                whale.Run(Sequences.load);
            };
            subTimer.Elapsed += (sender, eventArgs) =>
            {
                // 起動前にホームに戻る
                whale.Run(Sequences.reset);
            };

            // 基準seedを求める
            List<uint> pivotSeeds;
            do
            {
                startTime = DateTime.Now;

                cancellationTokenSource.Dispose();
                cancellationTokenSource = new();
                mainTask = mainTimer.Start(cancellationTokenSource.Token);
                subTask = subTimer.Start(cancellationTokenSource.Token);
                stopwatch.Reset();
                stopwatch.Start();

                var pivot = await GetPivotSeedGapsPair();
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
                    try { await mainTask; } catch (OperationCanceledException) { }
                    try { await subTask; } catch (OperationCanceledException) { }
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

                target = await pivotSeed.GetNextInitialSeed(targetID, countObserveIndicator, maxAdvance, stopwatch.Elapsed + TimeSpan.FromMinutes(2));
                waitTime = TimeSpan.FromMilliseconds(target.Seed - pivotSeed); // uint型変数同士では 0x0-0xFFFFFFFF=1 みたいな計算ができるので、万が一0をまたぐ際も大丈夫
                Console.WriteLine();
                Console.WriteLine("TargetSeed StartsAt            Advance");
                Console.WriteLine("---------- --------            -------");
                Console.WriteLine("{0,8:X}   {1,19} {2}", target.Seed, startTime + waitTime, target.Advance);

                // ゲームのロードを待機
                mainTimer.Submit(waitTime);
                subTimer.Submit(waitTime - TimeSpan.FromSeconds(25));

                await subTask;
                await mainTask;
                Console.WriteLine();
                Console.WriteLine("TargetWaitTime ActualElapsed");
                Console.WriteLine("-------------- -------------");
                Console.WriteLine("{0,14} {1,13}", waitTime.TotalMilliseconds, actualElapsed);

                await whale.RunAsync(Sequences.skipOpening_1);

                // 針読み結果
                var indicatorResults = await GetIndicatorResults();
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
                    var candidates = GetInitialSeedCandidates(target.Seed);
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
            try { await mainTask; } catch (OperationCanceledException) { }
            try { await subTask; } catch (OperationCanceledException) { }

            if (continueFlag)
            {
                // 基準seedから求め直し
                continue;
            }

            await notifier.SendAsync(string.Format("目標IDが出現する初期seedを引き当てました。seed:{0:X},advance:{1}", target.Seed, target.Advance));
            await AdvanceLeftover(target);
            break;

        } while (true);
    }

    /// <summary>
    /// interval±tolerance(ms)間隔でn回起動して、基準seedと誤差を取得する
    /// </summary>
    /// <param name="Seed"></param>
    /// <param name="aroundID"></param>
    /// <param name="interval"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    async Task<List<(uint Seed, List<long> Gaps)>> GetPivotSeedGapsPair()
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
                if (count > (countObserveID - 1))
                {
                    return;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                await whale.RunAsync(Sequences.getID);
                ushort id = 0;
                try
                {
                    using var currentFrame = preview.CurrentFrame;
                    id = currentFrame.GetCurrentID();
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
            using var timer = new Timer(getID, null, TimeSpan.Zero, interval);
            try
            {
                await Task.Delay(interval * countObserveID, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("=== Failed to get ID. ===");
                await whale.RunAsync(Sequences.reset);
            }
        } while (tids.Count() != countObserveID);

        return tids.GetPivotSeedsFromTIDs(interval, tolerance);
    }
    List<uint> GetInitialSeedCandidates(uint targetSeed)
    {
        var candidates = new List<uint>();
        for (var seed = targetSeed - (uint)tolerance.TotalMilliseconds; seed < targetSeed + (uint)tolerance.TotalMilliseconds + (uint)1; seed++)
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
    async Task<List<int>> GetIndicatorResults()
    {
        var list = new List<int>();

        // observations回は針読み
        for (var i = 0; i < countObserveIndicator; i++)
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
    /// <summary>
    /// 針読みに使用した分を差し引いて、消費を完了する
    /// 
    /// - 「きみは　おとこのこ？」で開始し、消費後に名前を確定、トレーナーカードを表示して終了する<br/>
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    async Task AdvanceLeftover((uint Seed, int Advance) target)
    {
        using var yes = new Mat(Path.Join(AppContext.BaseDirectory, "1231-266-70-35.png")); // fixme
        for (var i = 0; i < target.Advance - countObserveIndicator; i++)
        {
            await whale.RunAsync
            (
                new Operation[] { }
                    .Concat(Sequences.selectMale)
                    .Concat(Sequences.decideName_A).ToArray()
            );

            using var tmp = preview.CurrentFrame;
            using var trim = tmp.Clone(new Rect(1231, 266, 70, 35));
            if (!tmp.Contains(yes))
            {
                using var stream = tmp.ToStream(out var fileName);
                await notifier.SendAsync("問題が発生しました。画面を確認し、「あくん　だね？」「はい」「いいえ」まで手動で進めてください。", stream);
                Console.WriteLine("PANIC : Action required. Press Enter to continue...");
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
    }
}

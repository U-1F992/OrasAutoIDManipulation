using PokemonPRNG;

public static class PokemonSixGenRNGExtensions
{
    public static List<(uint Seed, long Gap)> GetGap(this ushort tid, uint targetSeed, int advance, int tolerance)
    {
        var ret = new List<(uint Seed, long Gap)>();
        Parallel.For(targetSeed - tolerance, targetSeed + tolerance + 1, seed =>
        {
            if (seed < 0)
            {
                seed = seed + 0x100000000;
            }

            var initialSeed = (uint)(seed & 0xFFFFFFFF);
            var tinyMT = new TinyMT(initialSeed);
            tinyMT.Advance(13 + advance);

            var pair = tinyMT.GetID();
            if (pair.tid == tid)
            {
                lock (ret)
                {
                    ret.Add((initialSeed, (long)initialSeed - targetSeed));
                }
            }
        });
        return ret;
    }
    /// <summary>
    /// interval(ms)ごとに取得した複数のTIDから、初回起動時の初期seedを検索する。
    /// </summary>
    /// <param name="tids"></param>
    /// <param name="interval"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    public static List<(uint Seed, List<long> Gaps)> GetPivotSeedsFromTIDs(this IList<ushort> tids, int interval, int tolerance)
    {
        var ret = new List<(uint Seed, List<long> Gaps)>();
        var baseDirectory = Path.Combine(AppContext.BaseDirectory, "database");

        // 各TIDが出るseedの一覧
        var databases = new List<uint[]>();
        foreach (var tid in tids)
        {
            var fileName = Path.Combine(baseDirectory, tid.ToString());
            databases.Add(File.ReadAllBytes(fileName).ToUInt32());
        }

        var pivotSeeds = databases[0];
        Parallel.ForEach(pivotSeeds, pivotSeed =>
        {
            var gaps = new List<long>();
            for (var i = 1; i < tids.Count; i++)
            {
                var center = pivotSeed + interval * i;
                var seeds = databases[i].Where(seed => center - tolerance <= seed && seed <= center + tolerance);
                if (seeds.Count() == 0)
                {
                    return;
                }

                // 他のseedもあったらバグになりそう
                gaps.Add(seeds.First() - center);
            }
            lock (ret)
            {
                ret.Add((pivotSeed, gaps));
            }
        });

        return ret;
    }
    /// <summary>
    /// pivotSeedから最も近い、狙った表裏IDが出る初期seedを求める。
    /// </summary>
    /// <param name="pivotSeed"></param>
    /// <param name="tid"></param>
    /// <param name="sid"></param>
    /// <param name="maxAdvance"></param>
    /// <param name="elapsedTime"></param>
    /// <returns></returns>
    public async static Task<(uint Seed, int Advance)> GetNextInitialSeed(this uint pivotSeed, (ushort tid, ushort sid) targetID, int minAdvance, int maxAdvance, TimeSpan elapsedTime)
    {
        return await Task.Run(() =>
        {
            var dictionary = new Dictionary<uint, int>();

            // 上5桁を順次、下3桁を平行処理で探索する
            for (int higher = 0; higher < 0x100000; higher++)
            {
                Parallel.For(0, 0x1000, lower =>
                {
                    long incremental = ((uint)higher << 12) + (uint)lower;
                    if (incremental < elapsedTime.TotalMilliseconds)
                    {
                        return;
                    }

                    uint seed = (uint)((pivotSeed + incremental) & 0xFFFFFFFF);
                    var tinyMT = new TinyMT(seed);
                    tinyMT.Advance(13);

                    for (var advance = minAdvance; advance < maxAdvance + 1; advance++)
                    {
                        if (tinyMT.GetID() != targetID)
                        {
                            continue;
                        }
                        lock (dictionary)
                        {
                            dictionary.Add(seed, advance);
                        }
                        return;
                    }
                });

                if (dictionary.Count != 0)
                {
                    break;
                }
            }
            if (dictionary.Count == 0)
            {
                throw new Exception("No such ID pair.");
            }
            return dictionary.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)).First();
        });
    }
}

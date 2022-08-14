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
    public static (uint Seed, int Advance) GetNextInitialSeed(this uint pivotSeed, ushort tid, ushort sid, int maxAdvance, TimeSpan elapsedTime)
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

                for (var advance = 0; advance < maxAdvance + 1; advance++)
                {
                    if (tinyMT.GetID() != (tid, sid))
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

            if (dictionary.Count == 0)
            {
                continue;
            }

            var closest = dictionary.OrderBy(pair => pair.Key).First();
            return (closest.Key, closest.Value);
        }
        throw new Exception("No such ID pair");
    }
    public static void Advance(this TinyMT tinyMT, int n) { tinyMT.Advance((uint)n); }
    public static void Advance(this TinyMT tinyMT, uint n)
    {
        for (var i = 0; i < n; i++)
        {
            tinyMT.Advance();
        }
    }
    public static ushort GenerateFirstTID(this TinyMT tinyMT)
    {
        tinyMT.Advance(13);
        return (ushort)(tinyMT.GetRand() & 0x0000FFFF);
    }
    public static (ushort tid, ushort sid) GetID(this TinyMT tinyMT)
    {
        uint rand = tinyMT.GetRand();
        return ((ushort)(rand & 0x0000FFFF), (ushort)((rand & 0xFFFF0000) >> 16));
    }
}

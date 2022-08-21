using PokemonPRNG;
public static class IListExtensions
{
    /// <summary>
    /// 進捗表示（500F）の結果から、初期seedの候補を求める<br/>
    /// https://twitter.com/e52301147/status/1544193587247972352<br/>
    /// ±1の誤差を許容します
    /// </summary>
    /// <param name="list"></param>
    /// <param name="candidates"></param>
    /// <returns></returns>
    public static async Task<List<uint>> GetInitialSeeds(this IList<int> list, IList<uint> candidates) { return await list.GetInitialSeeds(candidates, CancellationToken.None); }
    /// <summary>
    /// 進捗表示（500F）の結果から、初期seedの候補を求める<br/>
    /// https://twitter.com/e52301147/status/1544193587247972352<br/>
    /// ±1の誤差を許容します
    /// </summary>
    /// <param name="list"></param>
    /// <param name="candidates"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<List<uint>> GetInitialSeeds(this IList<int> list, IList<uint> candidates, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var results = new List<uint>();
            ulong n = 8;

            Parallel.ForEach(candidates, new ParallelOptions() { CancellationToken = cancellationToken }, initialSeed =>
            {
                var tinyMT = new TinyMT((uint)initialSeed);
                tinyMT.Advance(12);

                for (var i = 0; i < list.Count; i++)
                {
                    // https://github.com/wwwwwwzx/3DSRNGTool/blob/68883c3be831f4ccb2dcb5aee22d7fcc734b4aae/3DSRNGTool/Subforms/MiscRNGTool.cs#L468
                    var value = (int)((tinyMT.GetRand() * n) >> 32);
                    if (value != list.ElementAt(i)
                        && ((int)n + value - 1) % (int)n != list.ElementAt(i)
                        && (value + 1) % (int)n != list.ElementAt(i)
                    )
                    {
                        return;
                    }
                }

                lock (results)
                {
                    results.Add((uint)initialSeed);
                }
            });

            return results;
        });
    }
}

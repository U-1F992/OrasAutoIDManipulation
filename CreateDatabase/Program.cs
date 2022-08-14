using PokemonPRNG;

// 実行ファイルがあるディレクトリにdatabaseディレクトリを作り、
// 約16GBのデータベースを作成する。
//
// ファイル名: {tid}
// 内容: 名前決定をキャンセルせずに生成した場合に当該TIDが出る初期seedの一覧

// ディレクトリの上書き
var directoryName = Path.Combine(AppContext.BaseDirectory, "database");
if (Directory.Exists(directoryName))
{
    Directory.Delete(directoryName, true);
}
Directory.CreateDirectory(directoryName);

// 各ファイルをStreamの配列に割り当てる
var streams = new Stream[0x10000];
Parallel.For(0, streams.Length, i =>
{
    var fileName = Path.Combine(directoryName, i.ToString());
    streams[i] = Stream.Synchronized(File.OpenWrite(fileName));
});

// 5秒ごとに進捗を表示する
long completed = 0;
var timer = new Timer(_ =>
{
    var rate = (1.0 * completed / 0x100000000);
    Console.WriteLine("{0}, {1}", completed, rate.ToString("P"));
}, null, 0, 5000);

// すべての初期seedで生成して、ファイルに書く
Parallel.For(0x0, 0x100000000, seed =>
{
    var tid = new TinyMT((uint)seed).GenerateFirstTID();
    streams[tid].Write(BitConverter.GetBytes((uint)seed));
    Interlocked.Increment(ref completed);
});
Parallel.For(0, streams.Length, i => streams[i].Dispose());
Console.WriteLine("Database has been created at {0}", directoryName);

using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using OpenCvSharp;
using HogeiJunkyard;

Console.WriteLine("OrasAutoIDManipulation");
ushort tid = 354;
ushort sid = 28394;

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

using var serialPort = new SerialPort("COM6", 4800)
{
    Encoding = Encoding.UTF8,
    NewLine = "\r\n",
    DtrEnable = true,
    RtsEnable = true
};
serialPort.Open();
var whale = new WhaleController(serialPort, cancellationToken);

using var videoCapture = new VideoCapture(1)
{
    FrameWidth = 1920,
    FrameHeight = 1080
};
var video = new VideoCaptureWrapper(videoCapture, new Size(640, 360));

var tessConfig = new TessConfig("C:\\Program Files\\Tesseract-OCR\\tessdata\\", "eng", "0123456789", 3, 7);

await Task.Delay(1000);

LazyTimer lazyTimer;
Task mainWait;
LazyTimer lazyTimer1;
Task subWait;
DateTime startTime;
var stopwatch = new Stopwatch();
uint pivotSeed = 0;
ushort currentTID = 0;

do
{
    do
    {
        lazyTimer = new LazyTimer();
        mainWait = lazyTimer.Start();

        lazyTimer1 = new LazyTimer();
        subWait = lazyTimer1.Start();

        startTime = DateTime.Now;
        stopwatch.Reset();
        stopwatch.Start();
        try
        {
            pivotSeed = GetPivotSeed(whale, video, tessConfig, 0x66e01f63);
            break;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            continue;
        }
    } while (true);

    var next = pivotSeed.GetNextInitialSeed(tid, sid, 750, TimeSpan.FromMilliseconds(800000));
    var waitTime = TimeSpan.FromMilliseconds(
        next.Seed > pivotSeed
            ? next.Seed - pivotSeed
            : (0x100000000 + next.Seed) - pivotSeed
    );
    Console.WriteLine("=====\nNext: {0:X}\nETA: {1}\nAdvance: {2}", next.Seed, startTime + waitTime, next.Advance);

    lazyTimer.Submit(waitTime);
    lazyTimer1.Submit(waitTime - TimeSpan.FromSeconds(30));

    var backToHome = subWait.ContinueWith(_ =>
    {
        // 30秒前にホーム画面に戻る
        Console.WriteLine("=====\nBack to home: {0}", DateTime.Now);
        whale.Run(Sequences.reset).Wait();
    });
    await mainWait.ContinueWith(_ =>
    {
        Console.WriteLine("=====\nStart: {0}", DateTime.Now);
        Console.WriteLine("target: {0}", waitTime.TotalMilliseconds);
        Console.WriteLine("elapsed: {0}", stopwatch.ElapsedMilliseconds);
        whale.Run(Sequences.skipOpening_1).Wait();

        var discard = new Operation[] { }
            .Concat(Sequences.selectMale)
            .Concat(Sequences.decideName_A)
            .Concat(Sequences.discardName).ToArray();
        for (var i = 0; i < next.Advance; i++)
        {
            whale.Run(discard).Wait();
        }
        var sequence = new Operation[] { }
            .Concat(Sequences.selectMale)
            .Concat(Sequences.decideName_Kirin)
            .Concat(Sequences.confirmName)
            .Concat(Sequences.skipOpening_2)
            .Concat(Sequences.showTrainerCard).ToArray();
        whale.Run(sequence).Wait();

#if DEBUG
        using var frame = video.CurrentFrame;
        frame.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
#endif

        try
        {
            currentTID = GetCurrentID(video, new Rect(1112, 40, 112, 35), tessConfig);
            Console.WriteLine("TID: {0}", currentTID);
        }
        catch
        {
            Console.Error.WriteLine("Cannot get TID");
            currentTID = (ushort)(tid + 1); // 目標IDとは異なる値
        }
    });
    if (currentTID != tid)
    {
        try
        {
            currentTID.GetGap(next.Seed, next.Advance, 500).ForEach(pair =>
            {
                Console.WriteLine("Seed: {0:X}\tGap: {1}", pair.Seed, pair.Gap);
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
        }
        await whale.Run(Sequences.reset);
    }
    else
    {
        break;
    }
} while (true);

uint GetPivotSeed(WhaleController whale, VideoCaptureWrapper video, TessConfig tessConfig, uint pivotSeed = 0x0)
{
    var failed = false;
    var tids = new List<ushort>();
    var rectAroundID = new Rect(1112, 40, 112, 35);
    var stopwatch = new Stopwatch();

    var getID = (Task _) =>
    {
        Console.WriteLine("=====\nelapsed: {0}", stopwatch.ElapsedMilliseconds);
        whale.Run(Sequences.getID).Wait();
        ushort id = 0;
        try
        {
            id = GetCurrentID(video, rectAroundID, tessConfig);
        }
        catch (Exception exception)
        {
            failed = true;
            Console.Error.WriteLine(exception);
        }
        Console.WriteLine("TID: {0}", id);
        tids.Add(id);

        whale.Run(Sequences.reset).Wait();
    };

    stopwatch.Start();
    var tasks = Task.WhenAll
    (
        new LazyTimer(TimeSpan.FromMilliseconds(0)).Start().ContinueWith(getID),
        new LazyTimer(TimeSpan.FromMilliseconds(180000)).Start().ContinueWith(getID),
        new LazyTimer(TimeSpan.FromMilliseconds(360000)).Start().ContinueWith(getID),
        new LazyTimer(TimeSpan.FromMilliseconds(540000)).Start().ContinueWith(getID),
        new LazyTimer(TimeSpan.FromMilliseconds(720000)).Start().ContinueWith(_ =>
        {
            // 不正な入力を無視するためゲームをロードしておく
            whale.Run(new Operation[]
            {
                new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
                new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(9000))
            }).Wait();
        })
    );
    tasks.Wait();
    if (failed)
    {
        whale.Run(Sequences.reset).Wait();
        throw new Exception("Failed to get ID more than once.");
    }

    var first = tids.GetPivotSeedsFromTIDs(180000, 500).OrderBy(seedPair => Math.Abs(seedPair.Seed - pivotSeed)).First();
    Console.WriteLine("=====\npivotSeed: {0:X}\t{1}", first.Seed, string.Join(",", first.Gaps));
    return first.Seed;
}

ushort GetCurrentID(VideoCaptureWrapper video, Rect rect, TessConfig tessConfig)
{
    using var frame = video.CurrentFrame;
    using var id = frame.Clone(rect);
    using var gray = id.CvtColor(ColorConversionCodes.BGR2GRAY);
    using var binary = gray.Threshold(0, 255, ThresholdTypes.Otsu);
    using var invert = new Mat();
    Cv2.BitwiseNot(binary, invert);

    // #if DEBUG
    //     invert.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
    // #endif

    var ocr = invert.GetOCRResult(tessConfig);
    if (!ushort.TryParse(ocr, out var result))
    {
        throw new Exception("Cannot get ID from current frame.");
    }
    return result;
}

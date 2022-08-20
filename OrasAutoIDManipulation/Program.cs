using System.Diagnostics;
using OpenCvSharp;
using Hogei;

Console.WriteLine("OrasAutoIDManipulation");
ushort targetTID = 354;
ushort targetSID = 28394;
uint pivotSeed = 0x7DA0B3A0;

var resolve = (string fileName) => Path.Join(AppContext.BaseDirectory, fileName);

var databaseDir = resolve("database");
if (!Directory.Exists(databaseDir))
{
    throw new DirectoryNotFoundException(string.Format("Database not found. {0}", databaseDir));
}

using var serialPort = SerialPortFactory.FromJson(resolve("serialport.config.json"));
serialPort.Open();
var whale = new Whale(serialPort);

using var videoCapture = VideoCaptureFactory.FromJson(resolve("videocapture.config.json"));
var preview = new Preview(videoCapture, new Size(960, 540));

var tessConfig = new TessConfig("C:\\Program Files\\Tesseract-OCR\\tessdata\\", "eng", "0123456789", 3, 7);

await Task.Delay(1000);

DateTime startTime;
var stopwatch = new Stopwatch();

(uint Seed, int Advance) next = (0, 0);
TimeSpan waitTime = TimeSpan.Zero;

ushort currentTID = 0;

bool finished;
var mainTimer = new TimerStopwatch(_ =>
{
    Console.WriteLine("=====\nStart: {0}", DateTime.Now);
    Console.WriteLine("target: {0}", waitTime.TotalMilliseconds);
    Console.WriteLine("elapsed: {0}", stopwatch.ElapsedMilliseconds);
    whale.Run(Sequences.skipOpening_1);

    var discard = new Operation[] { }
        .Concat(Sequences.selectMale)
        .Concat(Sequences.decideName_A)
        .Concat(Sequences.discardName).ToArray();
    for (var i = 0; i < next.Advance; i++)
    {
        whale.Run(discard);
    }
    var sequence = new Operation[] { }
        .Concat(Sequences.selectMale)
        .Concat(Sequences.decideName_Kirin)
        .Concat(Sequences.confirmName)
        .Concat(Sequences.skipOpening_2)
        .Concat(Sequences.showTrainerCard).ToArray();
    whale.Run(sequence);

#if DEBUG
    using var frame = preview.CurrentFrame;
    frame.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
#endif

    try
    {
        using var currentFrame = preview.CurrentFrame;
        currentTID = GetCurrentID(currentFrame, new Rect(1112, 40, 112, 35), tessConfig);
        Console.WriteLine("TID: {0}", currentTID);
    }
    catch
    {
        Console.Error.WriteLine("Cannot get TID");
        currentTID = (ushort)(targetTID + 1); // 目標IDとは異なる値
    }

    finished = true;
}, null);
var subTimer = new TimerStopwatch(_ =>
{
    // 30秒前にホーム画面に戻る
    Console.WriteLine("=====\nBack to home: {0}", DateTime.Now);
    whale.Run(Sequences.reset);
}, null);

do
{
    uint currentPivotSeed = 0;
    
    do
    {
        finished = false;
        mainTimer.Reset();
        subTimer.Reset();
        stopwatch.Reset();

        mainTimer.Start();
        subTimer.Start();

        startTime = DateTime.Now;
        stopwatch.Start();
        try
        {
            currentPivotSeed = GetPivotSeed(whale, preview, tessConfig, pivotSeed);
            break;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            continue;
        }
    } while (true);

    next = currentPivotSeed.GetNextInitialSeed(targetTID, targetSID, 500, TimeSpan.FromMilliseconds(800000));
    waitTime = TimeSpan.FromMilliseconds(
        next.Seed > currentPivotSeed
            ? next.Seed - currentPivotSeed
            : (0x100000000 + next.Seed) - currentPivotSeed
    );
    Console.WriteLine("=====\nNext: {0:X}\nETA: {1}\nAdvance: {2}", next.Seed, startTime + waitTime, next.Advance);

    mainTimer.Submit(waitTime);
    subTimer.Submit(waitTime - TimeSpan.FromSeconds(30));
    while (!finished)
    {
        Thread.Sleep(500);
    }
    
    if (currentTID != targetTID)
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
        whale.Run(Sequences.reset);
    }
    else
    {
        break;
    }
} while (true);

uint GetPivotSeed(Whale whale, Preview preview, TessConfig tessConfig, uint pivotSeed = 0x0)
{
    var tids = new List<ushort>();
    var rectAroundID = new Rect(1112, 40, 112, 35);
    var stopwatch = new Stopwatch();

    var count = 0;
    var failed = false;
    var getID = new TimerCallback(_ =>
    {
        if (count > 3)
        {
            return;
        }

        Console.WriteLine("=====\nelapsed: {0}", stopwatch.ElapsedMilliseconds);
        whale.Run(Sequences.getID);
        ushort id = 0;
        try
        {
            using var currentFrame = preview.CurrentFrame;
            id = GetCurrentID(currentFrame, rectAroundID, tessConfig);
        }
        catch (Exception exception)
        {
            failed = true;
            Console.Error.WriteLine(exception);
        }
        Console.WriteLine("TID: {0}", id);
        tids.Add(id);

        whale.Run(Sequences.reset);
        Interlocked.Increment(ref count);
    });

    stopwatch.Start();
    using var timer = new Timer(getID, null, TimeSpan.Zero, TimeSpan.FromMinutes(3));
    Thread.Sleep(TimeSpan.FromMinutes(3 * 4));

    whale.Run(new Operation[]
    {
        new Operation(new KeySpecifier[] { KeySpecifier.A_Down }, TimeSpan.FromMilliseconds(200)),
        new Operation(new KeySpecifier[] { KeySpecifier.A_Up }, TimeSpan.FromMilliseconds(9000))
    });

    if (failed)
    {
        whale.Run(Sequences.reset);
        throw new Exception("Failed to get ID more than once.");
    }

    var first = tids.GetPivotSeedsFromTIDs(180000, 500).OrderBy(seedPair => Math.Abs(seedPair.Seed - pivotSeed)).First();
    Console.WriteLine("=====\npivotSeed: {0:X}\t{1}", first.Seed, string.Join(",", first.Gaps));
    return first.Seed;
}

ushort GetCurrentID(Mat mat, Rect rect, TessConfig tessConfig)
{
    using var id = mat.Clone(rect);
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
        mat.SaveImage("failed" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
        throw new Exception("Cannot get ID from current frame.");
    }
    return result;
}

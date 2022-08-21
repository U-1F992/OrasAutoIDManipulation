using OpenCvSharp;
using PokemonPRNG;
using Hogei;

var resolve = (string fileName) => Path.Join(AppContext.BaseDirectory, fileName);

using var serialPort = SerialPortFactory.FromJson(resolve("serialport.config.json"));
serialPort.Open();
var whale = new Whale(serialPort);

using var videoCapture = VideoCaptureFactory.FromJson(resolve("videocapture.config.json"));
var preview = new Preview(videoCapture, new Size(960, 540));

//(await new HourglassSearch(whale, preview).GetInitialSeeds(, , CancellationToken.None)).ForEach(seed => Console.WriteLine("{0,8:X}", seed));

var list = await (new int[] { 7, 1, 4, 4, 3, 6, 3, 7, 0, 5 }).GetInitialSeeds(new uint[] { 0x11111111, 0x10111111, 0x11111101, 0x11011111 });
list.ForEach(seed => Console.WriteLine("{0,8:X}", seed));

var aroundHourglass = new Rect(1280, 489, 23, 23);
var pos = (await preview.GetLastFrame(aroundHourglass)).GetPosition();
Console.WriteLine(pos);




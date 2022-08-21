using OpenCvSharp;
using Hogei;

Console.WriteLine("OrasAutoIDManipulation");

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

await Task.Delay(1000);
await new Application(whale, preview).Main((354, 28394), 5000);

using OpenCvSharp;
using Hogei;

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
var preview = new Preview(videoCapture);//, new Size(960, 540)
using var viewer = new Viewer(preview, @"C:\Users\mukai\AppData\Local\n3DSview_ver701_r5\x64\n3DS_view(x64).exe", TimeSpan.FromSeconds(20));

var notifier = new LINENotify(Environment.GetEnvironmentVariable("LINENOTIFY_TOKEN", EnvironmentVariableTarget.User)!);

Console.WriteLine("OrasAutoIDManipulation");
await Task.Delay(1000);
await new Application(whale, preview, notifier).Run((354, 28394));

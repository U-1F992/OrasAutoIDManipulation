using OpenCvSharp;
using Hogei;

public static class MatExtensions
{
    static readonly TessConfig tessConfig = new TessConfig("C:\\Program Files\\Tesseract-OCR\\tessdata\\", "eng", "0123456789", 3, 7);
    static readonly Rect aroundID = new Rect(1112, 40, 112, 35);
    public static ushort GetCurrentID(this Mat mat)
    {
        using var id = mat.Clone(aroundID);
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
}

using OpenCvSharp;
using Hogei;

public static class MatExtensions
{
    public static ushort GetCurrentID(this Mat mat, Rect rect, TessConfig tessConfig)
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
}

using OpenCvSharp;

public static class MatAnalysisExtensions
{
    /// <summary>
    /// RGBのユークリッド距離による色差の平均<br/>
    /// 色が近いと0に近付く
    /// </summary>
    /// <param name="mat1"></param>
    /// <param name="mat2"></param>
    /// <returns></returns>
    public static double CompareColor(this Mat mat1, Mat mat2)
    {
        if (mat1.Width != mat2.Width || mat1.Height != mat2.Height)
        {
            throw new Exception("Attempted to compare images of different sizes.");
        }
        var total = 0.0;
        var height = mat1.Height;
        var width = mat1.Width;
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var vec1 = mat1.At<Vec3b>(row, col);
                var vec2 = mat2.At<Vec3b>(row, col);

                var d0 = (vec1.Item0 / 255.0) - (vec2.Item0 / 255.0);
                var d1 = (vec1.Item1 / 255.0) - (vec2.Item1 / 255.0);
                var d2 = (vec1.Item2 / 255.0) - (vec2.Item2 / 255.0);
                var diff = (Math.Pow(d0, 2) + Math.Pow(d1, 2) + Math.Pow(d2, 2)) / 3.0;
                total += diff;
            }
        }
        return total / (height * width);
    }
    /// <summary>
    /// 進捗表示（虹色のくるくる）画像から、赤の位置を検出する
    /// </summary>
    /// <param name="mat"></param>
    /// <returns></returns>
    public static int GetPosition(this Mat mat)
    {
        var paths = Directory.GetFiles(Path.Join(AppContext.BaseDirectory, "masks"))
                        .Where(path => Path.GetExtension(path) == ".png");
        if (paths.Count() != 8)
        {
            throw new Exception("Mask images must exist from 0.png to 7.png.");
        }
        var masks = paths.Select(path => new Mat(path)).ToList();
        var size = masks.First().Size();
        using var resized = mat.Resize(size);

        // ピンク0xc72a74との差の平均を算出する
        using var reference = new Mat(size, mat.Type(), Scalar.FromRgb(0xc7, 0x2a, 0x74));
        // オレンジ0xa86020もわずかに加味する
        using var reference2 = new Mat(size, mat.Type(), Scalar.FromRgb(0xa8, 0x60, 0x20));

        var rateOfRed = masks.Select((mask, index) =>
        {
            using var masked = new Mat();
            Cv2.BitwiseAnd(resized, mask, masked);

            using var ref_masked = new Mat();
            Cv2.BitwiseAnd(reference, mask, ref_masked);

            using var ref2_masked = new Mat();
            Cv2.BitwiseAnd(reference2, mask, ref2_masked);

            var rate = masked.CompareColor(ref_masked) * 0.95 + masked.CompareColor(ref_masked) * 0.05;
            Console.WriteLine("{0}, {1}", index, rate);

// #if DEBUG
//             masked.SaveImage(index + ".png");
// #endif

            return rate;
        }).ToList();

        masks.ForEach(mask => mask.Dispose());
        return rateOfRed.IndexOf(rateOfRed.Min());
    }
}

using OpenCvSharp;
using Hogei;

public static class PreviewExtensions
{
    /// <summary>
    /// 進捗表示（虹色のくるくる）が消える最後のフレームを取得する<br/>
    /// 名前入力画面で開始する
    /// </summary>
    /// <param name="preview"></param>
    /// <param name="rect"></param>
    /// <returns></returns>
    public static async Task<Mat> GetLastFrame(this Preview preview, Rect rect, CancellationToken cancellationToken = default)
    {
        if (rect.Width != rect.Height)
        {
            throw new Exception("Parameter rect must represent a square.");
        }
        const double threshold = 0.005;

        using var tmp = preview.CurrentFrame;
        var type = tmp.Type();

        using var tmpClone = tmp.Clone(new Rect(rect.X - 1, rect.Y - 1, 1, 1));
        var vec3b = tmpClone.At<Vec3b>(0, 0);
        var color = Scalar.FromVec3b(vec3b);

        // 中心の画素を見て、周囲の緑色に近ければ表示されておらず、遠ければ表示中と判定する
        //  _________
        // |   ___   |
        // |  |   |  |
        // |  |___|  |
        // |_________|
        //
        // 中心のイメージ、9等分した真ん中

        var side = (int)Math.Round(rect.Width / 3.0, MidpointRounding.AwayFromZero);
        var center = new Rect(rect.X + side, rect.Y + side, side, side);
        using var source = new Mat(side, side, type, color);

        using var innerCts = new CancellationTokenSource();
        var innerCt = innerCts.Token;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCt, cancellationToken);
        var linkedCt = linkedCts.Token;

        bool show = false;
#if DEBUG
        show = true;
#endif
        
        var task = show ? Task.Run(() =>
        {
            using var window = new Window();
            while (true)
            {
                linkedCt.ThrowIfCancellationRequested();

                using var currentFrame = preview.CurrentFrame;
                using var trimmed = currentFrame.Clone(rect);
                using var toShow = trimmed.Resize(new Size(rect.Size.Width * 5, rect.Size.Height * 5));

                window.ShowImage(toShow);
                Cv2.WaitKey(1);
            }
        }, linkedCt) : Task.CompletedTask;

        // 表示されるまで
        using var appears = await Task.Run(() =>
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var currentFrame = preview.CurrentFrame;
                using var mat = currentFrame.Clone(center);

                if (source.CompareColor(mat) > threshold)
                {
#if DEBUG
                    Console.WriteLine("appears!");
#endif
                    return mat.Clone();
                }
            }
        }, cancellationToken);

        // 消えるまで
        var ret = await Task.Run(() =>
        {
            var ret = new Mat(rect.Height, rect.Width, type, Scalar.Black);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var currentFrame = preview.CurrentFrame;
                using var mat = currentFrame.Clone(center);

                if (appears.CompareColor(mat) > threshold)
                {
#if DEBUG
                    Console.WriteLine("disappears!");
#endif
                    return ret.Clone();
                }
                ret = currentFrame.Clone(rect);
            }
        }, cancellationToken);

        // 小窓を消す
        innerCts.Cancel();
        try { await task; } catch (OperationCanceledException) { }
#if DEBUG
        ret.SaveImage(DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");
#endif
        return ret.Clone();
    }
}

using Hogei;

public static class Sequences
{
    static KeySpecifier[] Key_A_Down = new KeySpecifier[] { KeySpecifier.A_Down };
    static KeySpecifier[] Key_A_Up = new KeySpecifier[] { KeySpecifier.A_Up };
    static KeySpecifier[] Key_Up_Down = new KeySpecifier[] { KeySpecifier.Up_Down };
    static KeySpecifier[] Key_Up_Up = new KeySpecifier[] { KeySpecifier.Up_Up };
    static KeySpecifier[] Key_Down_Down = new KeySpecifier[] { KeySpecifier.Down_Down };
    static KeySpecifier[] Key_Down_Up = new KeySpecifier[] { KeySpecifier.Down_Up };
    static KeySpecifier[] Key_Right_Down = new KeySpecifier[] { KeySpecifier.Right_Down };
    static KeySpecifier[] Key_Right_Up = new KeySpecifier[] { KeySpecifier.Right_Up };
    static KeySpecifier[] Key_Home_Down = new KeySpecifier[] { KeySpecifier.L_Down, KeySpecifier.R_Down, KeySpecifier.Z_Down, KeySpecifier.Start_Down };
    static KeySpecifier[] Key_Home_Up = new KeySpecifier[] { KeySpecifier.L_Up, KeySpecifier.R_Up, KeySpecifier.Z_Up, KeySpecifier.Start_Up };
    static TimeSpan standardDuration = TimeSpan.FromMilliseconds(200);

    public static Operation[] load = new Operation[]
    {
        // 起動
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(10000)),
    };
    public static Operation[] skipOpening_1 = new Operation[]
    {
        // 言語設定
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(5000)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(5000)),
        // タイトル
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(11500)),
        // 博士のモノローグ
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(59000)),
        // 「ところで......」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
    };
    public static Operation[] selectMale = new Operation[]
    {
        // 「きみは　おとこのこ？」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        // 男の子に合わせてA
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        // 「なまえも　おしえて　くれるかい！」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(3000)),
        // 名前入力へ遷移
    };
    public static Operation[] selectFemale = new Operation[]
    {
        // 「きみは　おとこのこ？」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        // 女の子に合わせてA
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1500)),
        // 「なまえも　おしえて　くれるかい！」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(3000)),
        // 名前入力へ遷移
    };
    public static Operation[] decideName_A = new Operation[]
    {
        // 初期位置の「あ」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(1000)),
        // 「おわり」へ移動して名前決定
        new Operation(new KeySpecifier[] { KeySpecifier.Start_Down }, TimeSpan.FromMilliseconds(1000)),
        new Operation(new KeySpecifier[] { KeySpecifier.Start_Up }, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(7000)),
    };
    public static Operation[] decideName_Kirin = new Operation[]
    {
        // 「あ」->（かな）->「カナ」
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(750)),
        // 「カナ」->「キ」
        new Operation(Key_Up_Down, standardDuration),
        new Operation(Key_Up_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Up_Down, standardDuration),
        new Operation(Key_Up_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Up_Down, standardDuration),
        new Operation(Key_Up_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Up_Down, standardDuration),
        new Operation(Key_Up_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(750)),
        // 「キ」->「リ」
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(750)),
        // 「リ」->（　）->「ン」
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_Down_Down, standardDuration),
        new Operation(Key_Down_Up, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(750)),

        // 「おわり」へ移動して名前決定
        new Operation(new KeySpecifier[] { KeySpecifier.Start_Down }, TimeSpan.FromMilliseconds(1000)),
        new Operation(new KeySpecifier[] { KeySpecifier.Start_Up }, TimeSpan.FromMilliseconds(500)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(7000)),
    };
    public static Operation[] confirmName = new Operation[]
    {
        // 「〇〇くん/ちゃん　だね？」>「はい」
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(2000)),
    };
    public static Operation[] discardName = new Operation[]
    {
        // 「〇〇くん/ちゃん　だね？」>「いいえ」
        new Operation(new KeySpecifier[] { KeySpecifier.B_Down }, standardDuration),
        new Operation(new KeySpecifier[] { KeySpecifier.B_Up }, TimeSpan.FromMilliseconds(2000)),
        // 「きみは　おとこのこ？」に戻る
    };
    public static Operation[] skipOpening_2 = new Operation[]
    {
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(2000)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(2000)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(32000)),
    };
    public static Operation[] showTrainerCard = new Operation[]
    {
        new Operation(new KeySpecifier[] { KeySpecifier.X_Down }, standardDuration),
        new Operation(new KeySpecifier[] { KeySpecifier.X_Up }, TimeSpan.FromMilliseconds(3000)),
        new Operation(Key_Right_Down, standardDuration),
        new Operation(Key_Right_Up, TimeSpan.FromMilliseconds(2000)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(5000))
    };
    public static Operation[] reset = new Operation[]
    {
        new Operation(Key_Home_Down, TimeSpan.FromMilliseconds(2000)),
        new Operation(Key_Home_Up, TimeSpan.FromMilliseconds(4000)),
        new Operation(new KeySpecifier[] { KeySpecifier.X_Down }, standardDuration),
        new Operation(new KeySpecifier[] { KeySpecifier.X_Up }, TimeSpan.FromMilliseconds(1000)),
        new Operation(Key_A_Down, standardDuration),
        new Operation(Key_A_Up, TimeSpan.FromMilliseconds(5000)),
    };
    public static Operation[] getID = new Operation[] {}
        .Concat(load)
        .Concat(skipOpening_1)
        .Concat(selectMale)
        .Concat(decideName_A)
        .Concat(confirmName)
        .Concat(skipOpening_2)
        .Concat(showTrainerCard).ToArray();
}

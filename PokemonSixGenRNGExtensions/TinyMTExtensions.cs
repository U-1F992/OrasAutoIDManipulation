using PokemonPRNG;
public static class TinyMTExtensions
{
    public static void Advance(this TinyMT tinyMT, int n) { tinyMT.Advance((uint)n); }
    public static void Advance(this TinyMT tinyMT, uint n)
    {
        for (var i = 0; i < n; i++)
        {
            tinyMT.Advance();
        }
    }
    public static ushort GenerateFirstTID(this TinyMT tinyMT)
    {
        tinyMT.Advance(13);
        return (ushort)(tinyMT.GetRand() & 0x0000FFFF);
    }
    public static (ushort tid, ushort sid) GetID(this TinyMT tinyMT)
    {
        uint rand = tinyMT.GetRand();
        return ((ushort)(rand & 0x0000FFFF), (ushort)((rand & 0xFFFF0000) >> 16));
    }
}

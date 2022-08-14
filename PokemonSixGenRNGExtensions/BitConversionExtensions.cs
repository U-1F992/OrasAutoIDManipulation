public static class BitConversionExtensions
{
    public static uint[] ToUInt32(this byte[] bytes)
    {
        var size = sizeof(uint);
        if (bytes.Length % size != 0)
        {
            throw new Exception("Cannot convert byte[] to uint[] because the index is not divisible.");
        }

        var length = bytes.Length / size;
        var dest = new uint[length];
        Parallel.For(0, length, i => dest[i] = BitConverter.ToUInt32(bytes, i * size));
        return dest;
    }
}

using System.IO;
using SevenZip.Compression;

namespace dumpsc
{
    class LZMA
    {
        public static byte[] Decompress(byte[] inputBytes)
        {
            byte[] result;

            using(LZMACoder coder = new LZMACoder())
            using (MemoryStream input = new MemoryStream(inputBytes))
            using (MemoryStream output = new MemoryStream())
            {
                coder.Decompress(input, output);

                result = output.ToArray();
            }

            return result;
        }
    }
}

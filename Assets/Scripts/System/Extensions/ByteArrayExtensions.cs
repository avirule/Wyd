#region

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Extensions
{
    public static class ByteArrayExtensions
    {
        public static byte[] Compress(this byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(output, CompressionLevel.Optimal))
                {
                    compressionStream.Write(data, 0, data.Length);
                    compressionStream.Close();
                }

                return output.ToArray();
            }
        }

        public static async Task<byte[]> CompressAsync(this byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(output, CompressionLevel.Optimal))
                {
                    await compressionStream.WriteAsync(data, 0, data.Length);
                }

                return output.ToArray();
            }
        }

        public static byte[] Decompress(this byte[] data)
        {
            using (MemoryStream output = new MemoryStream(data))
            {
                using (DeflateStream compressionStream = new DeflateStream(output, CompressionMode.Decompress))
                {
                    compressionStream.CopyTo(output);
                    compressionStream.Close();
                }

                return output.ToArray();
            }
        }

        public static async Task<byte[]> DecompressAsync(this byte[] data)
        {
            using (MemoryStream output = new MemoryStream(data))
            {
                using (DeflateStream compressionStream = new DeflateStream(output, CompressionMode.Decompress))
                {
                    await compressionStream.CopyToAsync(output);
                    compressionStream.Close();
                }

                return output.ToArray();
            }
        }
    }
}

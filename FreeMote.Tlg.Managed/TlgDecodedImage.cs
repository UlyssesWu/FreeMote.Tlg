using System;
using System.IO;

namespace FreeMote.Tlg.Managed
{
    /// <summary>
    /// 解码后的位图数据（内部统一为 BGRA32）。
    /// </summary>
    public sealed class TlgDecodedImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Bgra32 { get; }

        internal TlgDecodedImage(int width, int height, byte[] bgra32)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (bgra32 == null)
            {
                throw new ArgumentNullException(nameof(bgra32));
            }

            if (bgra32.Length != width * height * 4)
            {
                throw new ArgumentException("Invalid BGRA buffer length.", nameof(bgra32));
            }

            Width = width;
            Height = height;
            Bgra32 = bgra32;
        }

        public void SaveAsBmp(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            using (var fs = File.Create(filePath))
            {
                SaveAsBmp(fs);
            }
        }

        public void SaveAsBmp(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new ArgumentException("Stream must be writable.", nameof(stream));
            }

            var rowBytes = Width * 4;
            var imageBytes = rowBytes * Height;
            var fileSize = 14 + 40 + imageBytes;

            using (var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
            {
                // 输出 32-bit BI_RGB BMP（底朝上行序）。
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(fileSize);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write(14 + 40);

                bw.Write(40);
                bw.Write(Width);
                bw.Write(Height);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(0);
                bw.Write(imageBytes);
                bw.Write(2835);
                bw.Write(2835);
                bw.Write(0);
                bw.Write(0);

                for (var y = Height - 1; y >= 0; y--)
                {
                    bw.Write(Bgra32, y * rowBytes, rowBytes);
                }
            }
        }

        public void SaveAsPng(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            using (var fs = File.Create(filePath))
            {
                SaveAsPng(fs);
            }
        }

        public void SaveAsPng(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new ArgumentException("Stream must be writable.", nameof(stream));
            }

            var scanlineSize = 1 + Width * 4;
            var raw = new byte[scanlineSize * Height];
            for (var y = 0; y < Height; y++)
            {
                var src = y * Width * 4;
                var dst = y * scanlineSize;
                raw[dst] = 0;
                dst++;
                for (var x = 0; x < Width; x++)
                {
                    var b = Bgra32[src++];
                    var g = Bgra32[src++];
                    var r = Bgra32[src++];
                    var a = Bgra32[src++];
                    // PNG 使用 RGBA 顺序，这里把内部 BGRA 转成 RGBA。
                    raw[dst++] = r;
                    raw[dst++] = g;
                    raw[dst++] = b;
                    raw[dst++] = a;
                }
            }

            var zlib = PngZlibNoCompression(raw);

            using (var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
            {
                bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                var ihdr = new byte[13];
                WriteBigEndian(ihdr, 0, (uint)Width);
                WriteBigEndian(ihdr, 4, (uint)Height);
                ihdr[8] = 8;
                ihdr[9] = 6;
                ihdr[10] = 0;
                ihdr[11] = 0;
                ihdr[12] = 0;
                WritePngChunk(bw, "IHDR", ihdr);
                WritePngChunk(bw, "IDAT", zlib);
                WritePngChunk(bw, "IEND", new byte[0]);
            }
        }

        private static byte[] PngZlibNoCompression(byte[] raw)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, true))
            {
                // 采用 zlib 存储块（不压缩），便于最小依赖导出 PNG。
                bw.Write((byte)0x78);
                bw.Write((byte)0x01);

                var offset = 0;
                while (offset < raw.Length)
                {
                    var chunk = Math.Min(65535, raw.Length - offset);
                    var finalBlock = offset + chunk >= raw.Length;
                    bw.Write((byte)(finalBlock ? 0x01 : 0x00));
                    bw.Write((ushort)chunk);
                    bw.Write((ushort)(~chunk));
                    bw.Write(raw, offset, chunk);
                    offset += chunk;
                }

                var adler = Adler32(raw);
                bw.Write((byte)((adler >> 24) & 0xFF));
                bw.Write((byte)((adler >> 16) & 0xFF));
                bw.Write((byte)((adler >> 8) & 0xFF));
                bw.Write((byte)(adler & 0xFF));
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            uint s1 = 1;
            uint s2 = 0;
            for (var i = 0; i < data.Length; i++)
            {
                s1 = (s1 + data[i]) % mod;
                s2 = (s2 + s1) % mod;
            }

            return (s2 << 16) | s1;
        }

        private static void WritePngChunk(BinaryWriter bw, string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            WriteBigEndian(bw, (uint)data.Length);
            bw.Write(typeBytes);
            bw.Write(data);

            var crcBuffer = new byte[typeBytes.Length + data.Length];
            Buffer.BlockCopy(typeBytes, 0, crcBuffer, 0, typeBytes.Length);
            if (data.Length > 0)
            {
                Buffer.BlockCopy(data, 0, crcBuffer, typeBytes.Length, data.Length);
            }

            WriteBigEndian(bw, Crc32(crcBuffer));
        }

        private static uint Crc32(byte[] data)
        {
            const uint poly = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;
            for (var i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (var k = 0; k < 8; k++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ poly;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return ~crc;
        }

        private static void WriteBigEndian(byte[] target, int offset, uint value)
        {
            target[offset + 0] = (byte)((value >> 24) & 0xFF);
            target[offset + 1] = (byte)((value >> 16) & 0xFF);
            target[offset + 2] = (byte)((value >> 8) & 0xFF);
            target[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteBigEndian(BinaryWriter bw, uint value)
        {
            bw.Write((byte)((value >> 24) & 0xFF));
            bw.Write((byte)((value >> 16) & 0xFF));
            bw.Write((byte)((value >> 8) & 0xFF));
            bw.Write((byte)(value & 0xFF));
        }
    }
}

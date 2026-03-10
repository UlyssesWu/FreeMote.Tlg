using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

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

            using (var bitmap = CreateBitmap())
            {
                bitmap.Save(stream, ImageFormat.Bmp);
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

            using (var bitmap = CreateBitmap())
            {
                bitmap.Save(stream, ImageFormat.Png);
            }
        }

        private Bitmap CreateBitmap()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, Width, Height);
            BitmapData bitmapData = null;
            try
            {
                bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var rowBytes = Width * 4;
                var stride = bitmapData.Stride;
                for (var y = 0; y < Height; y++)
                {
                    var dstOffset = stride >= 0
                        ? y * stride
                        : (Height - 1 - y) * (-stride);
                    Marshal.Copy(Bgra32, y * rowBytes, IntPtr.Add(bitmapData.Scan0, dstOffset), rowBytes);
                }

                bitmap.UnlockBits(bitmapData);
                bitmapData = null;
                return bitmap;
            }
            catch
            {
                if (bitmapData != null)
                {
                    bitmap.UnlockBits(bitmapData);
                }

                bitmap.Dispose();
                throw;
            }
        }
    }
}

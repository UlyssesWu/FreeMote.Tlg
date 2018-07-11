using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FreeMote.Tlg;

namespace FreeMote.Tlg.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            MemoryTest();
            
            var target = File.Exists("test.tlg") ? "test.tlg" : "NewGame5.tlg";
            TlgImageConverter converter = new TlgImageConverter();
            var original = File.ReadAllBytes(target);
            byte[] converted = null;
            using (var fs = File.Open(target, FileMode.Open))
            {
                using (var br = new BinaryReader(fs))
                {
                    var bmp = converter.Read(br);
                    converted = bmp.ToTlg6();
                    if (converted == null)
                    {
                        Console.WriteLine("Conversion failed.");
                    }
                    //else
                    //{
                    //    Console.WriteLine($"Totally equal: {(original.SequenceEqual(converted) ? "Yes" : "No")}");
                    //}
                }
            }

            if (converted != null)
            {
                using (var ms = new MemoryStream(converted))
                {
                    using (var br = new BinaryReader(ms))
                    {
                        var bmp = converter.Read(br);
                        bmp.Save("output.png", ImageFormat.Png);
                    }
                }
            }

            Console.WriteLine($"IsTLG: {(TlgNative.CheckTlg(original) ? "Yes" : "No")}");
            if (TlgNative.GetInfoTlg(original, out int w, out int h, out int v))
            {
                Console.WriteLine($"TLGv{v} Size: {w} x {h}");
            }
            var bmp2 = TlgNative.ToBitmap(original, out _);
            bmp2.Save("output2.png", ImageFormat.Png);

            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        static void MemoryTest()
        {
            var target = File.Exists("test.tlg") ? "test.tlg" : "NewGame5.tlg";
            TlgImageConverter converter = new TlgImageConverter();
            var original = File.ReadAllBytes(target);
            Console.WriteLine("TLG bytes loaded.");
            Console.ReadLine();

            using (var ms = new MemoryStream(original))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    Bitmap b = converter.Read(br);
                    b.Dispose();
                }
            }

            Console.WriteLine("Managed done.");
            Console.ReadLine();
            GC.Collect();

            using (TlgLoader ldr = new TlgLoader(original))
            {
                Bitmap b = ldr.Bitmap;
                b.Dispose();
            }

            Console.WriteLine("NativeLoader done.");
            Console.ReadLine();
            GC.Collect();

            Bitmap b2 = TlgNative.ToBitmap(original, out _);
            b2.Dispose();
            Console.WriteLine("NativeCopy done.");
            Console.ReadLine();
            GC.Collect();

            Console.WriteLine("All done.");
            Console.ReadLine();
        }
    }
}

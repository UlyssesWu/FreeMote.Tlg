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
            var target = "NewGame5.tlg";
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
            if (TlgNative.GetInfoTlg(original, out int w, out int h))
            {
                Console.WriteLine($"TLG Size: {w} x {h}");
            }
            var bmp2 = TlgNative.ToBitmap(original);
            bmp2.Save("output2.png", ImageFormat.Png);

            Console.WriteLine("Done.");
            Console.ReadLine();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using FreeMote;
using FreeMote.Tlg;

namespace TlgLib.Benchmark
{
    [MemoryDiagnoser]
    //[InProcess]
    public class TlgBenchmark
    {
        private byte[] _tlgBytes;


        [GlobalSetup]
        public void Setup()
        {
            if (File.Exists("test.tlg"))
            {
                _tlgBytes = File.ReadAllBytes("test.tlg");
                Console.WriteLine("// Using test.tlg");
            }
            else if (File.Exists("NewGame5.tlg"))
            {
                _tlgBytes = File.ReadAllBytes("NewGame5.tlg");
                Console.WriteLine("// Using NewGame5.tlg");
            }
            else
            {
                _tlgBytes = new byte[1];
                Console.WriteLine("// tlg Not Found");
            }
        }

        [Benchmark]
        public void ManagedBenchmark()
        {
            TlgImageConverter converter = new TlgImageConverter();
            using (BinaryReader br = new BinaryReader(new MemoryStream(_tlgBytes), Encoding.UTF8, false))
            {
                Bitmap b = converter.Read(br);
                int w = b.Width;
                b.Dispose();
            }

        }

        [Benchmark]
        public void NativeCopyBenchmark()
        {
            Bitmap b = TlgNative.ToBitmap(_tlgBytes, out _);
            int w = b.Width;
            b.Dispose();
        }

        [Benchmark]
        public void NativeLoaderBenchmark()
        {
            using (var ldr = new TlgLoader(_tlgBytes))
            {
                int w = ldr.Bitmap.Width;
            }
        }

    }
}

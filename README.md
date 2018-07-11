# TlgLib

.NET wrapper for `libtlg`, just for loading or saving TLG (Terrible Low-quality Graphics) format files.

## Benchmark

We ran a benchmark to compare @[morkt](https://github.com/morkt/GARbro)'s managed TLG loader (LICENSE: MIT) with this (only for loading).

``` ini

BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
Intel Core i5-6300U CPU 2.40GHz (Skylake), 1 CPU, 4 logical and 2 physical cores

```
|                Method |     Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|---------------------- |---------:|----------:|----------:|---------:|---------:|---------:|----------:|
|      ManagedBenchmark | 41.03 ms | 1.1473 ms | 1.1781 ms | 437.5000 | 437.5000 | 437.5000 | 4544462 B |
|   NativeCopyBenchmark | 31.49 ms | 0.5719 ms | 0.5070 ms |        - |        - |        - |       0 B |
| NativeLoaderBenchmark | 28.33 ms | 0.5198 ms | 0.4608 ms |        - |        - |        - |       0 B |


Managed = morkt's pure managed TLG loader;

NativeCopy = TlgNative.ToBitmap(byte[], out int, bool);

NativeLoader = new TlgLoader(byte[]).Bitmap;

## Thanks

`libtlg` comes from [tlg-wic-codec](https://github.com/krkrz/tlg-wic-codec). We have made some [fixes](https://github.com/krkrz/tlg-wic-codec/pull/1) and modifications.

We use @[morkt](https://github.com/morkt/GARbro)'s [ImageTLG](https://github.com/morkt/GARbro/blob/master/ArcFormats/KiriKiri/ImageTLG.cs) (LICENSE: MIT) in this project and [FreeMote](https://github.com/Project-AZUSA/FreeMote).

---

by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

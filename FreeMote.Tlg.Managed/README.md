# FreeMote.Tlg.Managed

主要支持 `TLGqoi` 的解码，并可导出 BMP/PNG。

由于没有搜集`TLGmux`样本，`TLGmux`相关内容完全参照 @2778995958 的[文档](https://github.com/2778995958/gal_tachie_ai/tree/main/yuzu/tlgqoi_mux_ref)实现。

## 用法

```csharp
using FreeMote.Tlg.Managed;

var image = TlgQoiCodec.Decode(@"path\to\image.tlg");
image.SaveAsPng(@"out.png");
```

对多图容器（例如 `QHDR.imageCount > 1` 的 `TLGqoi`），可批量导出：

```csharp
var outputs = TlgQoiCodec.ExportAllAsPng(
    @"path\to\container.tlg",
    @"path\to\out-dir");
```

也可以先解码全部图像，再自行处理：

```csharp
var frames = TlgQoiCodec.DecodeAll(@"path\to\container.tlg");
for (var i = 0; i < frames.Count; i++)
{
    frames[i].SaveAsBmp($@"out\frame_{i:D3}.bmp");
}
```

## 选项说明

- `ImageIndex` / `ImageCount`
  - 分别对应容器图像索引和容器图像总数（总数也作为相位周期）。
  - 两者都不填时，解码器自动推断。
- `UseReferenceIndexWhenAvailable`
  - 解码 `TLGref` 时，若未显式提供 index/count，是否优先使用 `QREF` 中的值。默认 `true`。
- `AutoPhaseWindow`
  - 自动推断时用于反推 `index` 的窗口宽度，默认 `4`。
- `MuxEntryIndex`
  - 解码 `TLGmux` 时指定 `CMUX` 条目索引。
  - 为 `null` 时自动尝试所有条目；为具体索引时只解该条目。

### Mux 指定条目示例

```csharp
var image = TlgQoiCodec.Decode(path, new TlgDecodeOptions
{
    MuxEntryIndex = 0
});
```


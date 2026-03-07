# FreeMote.Tlg.Managed

主要支持 `TLGqoi`的解码，并可导出为 BMP/PNG。（`TLGref`、`TLGmux`未经样本验证。）

## 用法

```csharp
using FreeMote.Tlg.Managed;

var image = TlgQoiCodec.Decode(@"path\to\image.tlg");
image.SaveAsBmp(@"out.bmp");
```

## DecodeOptions（显式相位参数）

`TLGqoi` 的像素重建依赖相位参数（`begin/end`）。会尝试自动指定，但如果相位不对，图像会被错误解码。

可以通过 `TlgDecodeOptions` 显式控制：

```csharp
var image = TlgQoiCodec.Decode(path, new TlgDecodeOptions
{
    PhaseBegin = 10,
    PhaseEnd = 14
});
```

### 选项说明

- `PhaseBegin` / `PhaseEnd`
  - 分别对应相位起点和相位终点（终点也作为相位周期）。
  - 两者都不填时，解码器自动推断。
- `UseReferencePhaseWhenAvailable`
  - 解码 `TLGref` 时，若未显式提供相位，是否优先使用 `QREF` 中的 `begin/end`。默认 `true`。
- `AutoPhaseWindow`
  - 自动推断时用于反推 `begin` 的窗口宽度，默认 `4`。
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

## 推荐调样本方式

1. 先用默认自动模式解码；
2. 若出现错误解码，先尝试显式相位（例如 `10/14`）；
3. 对 `TLGref`，优先检查其 `QREF` 指向的目标文件和相位参数；
4. 对 `TLGmux`，可先用自动模式；若要固定子图则设置 `MuxEntryIndex`；
5. 若在一批样本中规律性偏移，可固定 `DecodeOptions` 批量处理。


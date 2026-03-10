using System.Collections.Generic;

namespace FreeMote.Tlg.Managed
{
    /// <summary>
    /// 统一的 TLG 解析结果模型。
    /// 当前主要服务于 TLGqoi/TLGref/TLGmux 的结构化读取。
    /// </summary>
    public sealed class TlgFile
    {
        internal readonly List<TlgChunk> MutableChunks = new List<TlgChunk>();
        internal readonly List<TlgReferenceTarget> MutableReferences = new List<TlgReferenceTarget>();
        internal readonly List<TlgMuxEntry> MutableMuxEntries = new List<TlgMuxEntry>();
        internal readonly Dictionary<string, string> MutableTags = new Dictionary<string, string>();

        /// <summary>
        /// 是否为 TLG0.0 SDS 容器。
        /// </summary>
        public bool IsSdsContainer { get; internal set; }

        /// <summary>
        /// SDS 头部中的 rawLength 字段（未压缩长度提示）。
        /// </summary>
        public uint SdsRawLength { get; internal set; }

        public TlgFormatKind Format { get; internal set; }
        public byte ColorType { get; internal set; }
        public uint Width { get; internal set; }
        public uint Height { get; internal set; }

        /// <summary>
        /// QHDR 的原始关键字段。
        /// </summary>
        public TlgQoiHeader QoiHeader { get; internal set; }

        public IReadOnlyList<TlgChunk> Chunks => MutableChunks;

        public IReadOnlyList<TlgReferenceTarget> References => MutableReferences;

        public IReadOnlyList<TlgMuxEntry> MuxEntries => MutableMuxEntries;

        public IReadOnlyDictionary<string, string> Tags => MutableTags;
    }

    /// <summary>
    /// 通用 chunk 头（4 字节 Tag + 4 字节 Size）。
    /// </summary>
    public sealed class TlgChunk
    {
        public string Tag { get; internal set; }
        public uint Size { get; internal set; }
        public long PayloadOffset { get; internal set; }
    }

    /// <summary>
    /// TLGqoi 的 QHDR 内容
    /// </summary>
    public sealed class TlgQoiHeader
    {
        /// <summary>
        /// 通常用于与 QREF 对应的 fingerprint。
        /// </summary>
        public uint Fingerprint { get; internal set; }

        /// <summary>
        /// 容器内图像数量。
        /// </summary>
        public uint ImageCount { get; internal set; }

        /// <summary>
        /// 条带高度（每个 band 的行数）。
        /// </summary>
        public uint BandHeight { get; internal set; }

        /// <summary>
        /// 条带数量（band count）。
        /// </summary>
        public uint BandCount { get; internal set; }

        /// <summary>
        /// QOI 符号数量提示值。
        /// </summary>
        public ulong SymbolCountHint { get; internal set; }

        /// <summary>
        /// DTBL 的相对偏移（相对 QHDR 终止后的数据基址）。
        /// </summary>
        public ulong DtblChunkOffset { get; internal set; }

        /// <summary>
        /// RTBL 的相对偏移（相对 QHDR 终止后的数据基址）。
        /// </summary>
        public ulong RtblChunkOffset { get; internal set; }

        /// <summary>
        /// 容器数据区总长度提示值。
        /// </summary>
        public ulong ContainerDataLengthHint { get; internal set; }

    }

    /// <summary>
    /// TLGref 中 QREF chunk 的目标信息。
    /// </summary>
    public sealed class TlgReferenceTarget
    {
        public uint Fingerprint { get; internal set; }

        /// <summary>
        /// 容器内图像索引（0-based）。
        /// </summary>
        public int ImageIndex { get; internal set; }

        /// <summary>
        /// 容器内图像总数。
        /// </summary>
        public int ImageCount { get; internal set; }
        public uint PathByteLength { get; internal set; }
        public string Path { get; internal set; }
    }

    /// <summary>
    /// TLGmux 的 CMUX 条目。
    /// 命名依据：SliceLayer 的 fetchPartialInfo/loadPartialImage 使用模式
    /// </summary>
    public sealed class TlgMuxEntry
    {
        /// <summary>
        /// partial 区域 X
        /// </summary>
        public uint PartialX { get; internal set; }

        /// <summary>
        /// partial 区域 Y
        /// </summary>
        public uint PartialY { get; internal set; }

        /// <summary>
        /// partial 宽度
        /// </summary>
        public uint PartialWidth { get; internal set; }

        /// <summary>
        /// partial 高度
        /// </summary>
        public uint PartialHeight { get; internal set; }

        /// <summary>
        /// 子流相对偏移（原始 64 位值）
        /// 相对 CMUX 区末尾（终止 chunk 后）计算。
        /// </summary>
        public ulong RelativeStreamOffsetRaw { get; internal set; }

        public long RelativeStreamOffset
        {
            get { return unchecked((long)RelativeStreamOffsetRaw); }
        }
    }
}

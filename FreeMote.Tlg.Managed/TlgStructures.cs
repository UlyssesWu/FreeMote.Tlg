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
        /// 常作为相位提示值（实践中常用于推导 begin/end）。
        /// </summary>
        public uint PhaseEndHint { get; internal set; }

        /// <summary>
        /// 分段高度（每个 section 的行数）。
        /// </summary>
        public uint SectionHeight { get; internal set; }

        /// <summary>
        /// section 数量。
        /// </summary>
        public uint SectionCount { get; internal set; }

        /// <summary>
        /// (Unknown) QHDR +0x10
        /// </summary>
        public ulong Unknown10H { get; internal set; }

        /// <summary>
        /// DTBL 的相对偏移（相对 QHDR 终止后的数据基址）。
        /// </summary>
        public ulong DtblOffset { get; internal set; }

        /// <summary>
        /// RTBL 的相对偏移（相对 QHDR 终止后的数据基址）。
        /// </summary>
        public ulong RtblOffset { get; internal set; }

        /// <summary>
        /// 数据区长度提示值（未完全确认）。
        /// </summary>
        public ulong DataLengthHint { get; internal set; }

    }

    /// <summary>
    /// TLGref 中 QREF chunk 的目标信息。
    /// </summary>
    public sealed class TlgReferenceTarget
    {
        public uint Fingerprint { get; internal set; }

        /// <summary>
        /// 相位起点（phase begin）。
        /// </summary>
        public int Begin { get; internal set; }

        /// <summary>
        /// 相位周期（phase end）。
        /// </summary>
        public int End { get; internal set; }
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
        /// partial 区域 X（推断）
        /// </summary>
        public uint PartialX { get; internal set; }

        /// <summary>
        /// partial 区域 Y（推断）
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
        /// 在已验证样本中，该值通常相对 CMUX 区末尾（终止 chunk 后）计算。
        /// </summary>
        public ulong RelativeStreamOffsetRaw { get; internal set; }

        /// <summary>
        /// 将相对偏移按有符号值解释，便于做 seek。
        /// </summary>
        public long RelativeStreamOffset
        {
            get { return unchecked((long)RelativeStreamOffsetRaw); }
        }
    }
}

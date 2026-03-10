namespace FreeMote.Tlg.Managed
{
    /// <summary>
    /// TLG 解码选项（支持 TLGqoi/TLGref/TLGmux）。
    /// </summary>
    public sealed class TlgDecodeOptions
    {
        /// <summary>
        /// 显式指定容器图像索引（0-based）。
        /// </summary>
        public int? ImageIndex { get; set; }

        /// <summary>
        /// 显式指定容器图像总数（也作为周期）。
        /// </summary>
        public int? ImageCount { get; set; }

        /// <summary>
        /// 解码 TLGref 时，若未显式提供 ImageIndex/ImageCount，是否优先使用 QREF 中的 index/count。
        /// </summary>
        public bool UseReferenceIndexWhenAvailable { get; set; } = true;

        /// <summary>
        /// 自动推断 ImageIndex 时使用的窗口宽度（默认 4）。
        /// 仅在未显式给定 ImageIndex 且需要自动推断时生效。
        /// </summary>
        public int AutoPhaseWindow { get; set; } = 4;

        /// <summary>
        /// 解码 TLGmux 时要选取的 CMUX 条目索引。
        /// 为 null 时按顺序尝试各条目，返回第一个成功解码的结果。
        /// </summary>
        public int? MuxEntryIndex { get; set; }
    }
}

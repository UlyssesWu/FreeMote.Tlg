using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FreeMote.Tlg.Managed
{
    /// <summary>
    /// TLGqoi/TLGref/TLGmux 解码器
    /// 说明：
    /// 1) TLGqoi: 直接读取 QHDR、DTBL、RTBL 后解码像素。
    /// 2) TLGref: 先解析 QREF，再定位目标 qoi 并按相位参数解码。
    /// 3) TLGmux: 读取 CMUX，定位子流后递归分发到 qoi/ref/sds。
    /// </summary>
    public static class TlgQoiCodec
    {
        // 调用方未显式传 begin/end 时使用自动相位推断
        private const int AutoPhase = int.MinValue;
        // 经验上 TLGqoi 常用 4 相位窗口，实际 end 由 QHDR.PhaseEndHint 提示
        private const int DefaultPhaseWindow = 4;
        // 递归解码（mux/sds 嵌套）上限，避免异常数据导致无限递归
        private const int MaxNestedDecodeDepth = 16;

        private static readonly byte[] TlgQoiHeader = Encoding.ASCII.GetBytes("TLGqoi\0raw\x1a");
        private static readonly byte[] TlgRefHeader = Encoding.ASCII.GetBytes("TLGref\0raw\x1a");
        private static readonly byte[] TlgMuxHeader = Encoding.ASCII.GetBytes("TLGmux\0idx\x1a");
        private static readonly byte[] Tlg0SdsHeader = Encoding.ASCII.GetBytes("TLG0.0\0sds\x1a");
        private static readonly byte[] QhdrTag = Encoding.ASCII.GetBytes("QHDR");
        private static readonly byte[] QrefTag = Encoding.ASCII.GetBytes("QREF");
        private static readonly byte[] DtblTag = Encoding.ASCII.GetBytes("DTBL");
        private static readonly byte[] RtblTag = Encoding.ASCII.GetBytes("RTBL");
        private static readonly byte[] CmuxTag = Encoding.ASCII.GetBytes("CMUX");

        public static TlgDecodedImage Decode(string filePath)
        {
            return Decode(filePath, null);
        }

        public static TlgDecodedImage Decode(string filePath, TlgDecodeOptions options)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            options = options ?? new TlgDecodeOptions();
            var data = File.ReadAllBytes(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            return DecodeAny(data, directory, options, 0);
        }

        public static TlgDecodedImage DecodeQoi(byte[] data)
        {
            return DecodeQoi(data, null, null);
        }

        public static TlgDecodedImage DecodeQoi(byte[] data, TlgDecodeOptions options)
        {
            return DecodeQoi(data, options, null);
        }

        public static TlgDecodedImage DecodeQoi(byte[] data, TlgDecodeOptions options, uint? expectedFingerprint)
        {
            options = options ?? new TlgDecodeOptions();
            var begin = options.PhaseBegin ?? AutoPhase;
            var end = options.PhaseEnd ?? AutoPhase;
            var autoPhaseWindow = NormalizeAutoPhaseWindow(options.AutoPhaseWindow);
            return DecodeQoiCore(data, begin, end, expectedFingerprint, autoPhaseWindow);
        }

        public static TlgDecodedImage DecodeQoi(byte[] data, int begin, int end, uint? expectedFingerprint)
        {
            return DecodeQoiCore(data, begin, end, expectedFingerprint, DefaultPhaseWindow);
        }

        /// <summary>
        /// 统一入口：按头部自动分派 qoi/ref/mux/sds。
        /// </summary>
        private static TlgDecodedImage DecodeAny(byte[] data, string directory, TlgDecodeOptions options, int depth)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (depth > MaxNestedDecodeDepth)
            {
                throw new InvalidDataException("TLG nested decode depth is too large.");
            }

            options = options ?? new TlgDecodeOptions();
            var phaseBegin = options.PhaseBegin ?? AutoPhase;
            var phaseEnd = options.PhaseEnd ?? AutoPhase;
            var autoPhaseWindow = NormalizeAutoPhaseWindow(options.AutoPhaseWindow);

            if (HasPrefix(data, TlgRefHeader))
            {
                var reference = ParseReference(data);
                var qoiPath = ResolveReferencePath(directory, reference);
                var qoiData = File.ReadAllBytes(qoiPath);

                // 未显式指定相位时，TLGref 默认优先采用 QREF 的 begin/end。
                if (options.UseReferencePhaseWhenAvailable)
                {
                    if (!options.PhaseBegin.HasValue)
                    {
                        phaseBegin = reference.Begin;
                    }

                    if (!options.PhaseEnd.HasValue)
                    {
                        phaseEnd = reference.End;
                    }
                }

                return DecodeQoiCore(qoiData, phaseBegin, phaseEnd, reference.Fingerprint, autoPhaseWindow);
            }

            if (HasPrefix(data, TlgQoiHeader))
            {
                return DecodeQoiCore(data, phaseBegin, phaseEnd, null, autoPhaseWindow);
            }

            if (HasPrefix(data, TlgMuxHeader))
            {
                return DecodeMux(data, directory, options, depth);
            }

            if (HasPrefix(data, Tlg0SdsHeader))
            {
                return DecodeSds(data, directory, options, depth);
            }

            throw new InvalidDataException("Unsupported TLG stream. Expected qoi/ref/mux/sds.");
        }

        private static TlgDecodedImage DecodeSds(byte[] data, string directory, TlgDecodeOptions options, int depth)
        {
            if (data.Length < 15)
            {
                throw new InvalidDataException("Invalid TLG0 SDS header.");
            }

            var rawLength32 = ReadUInt32(data, 11);
            if (rawLength32 > int.MaxValue)
            {
                throw new InvalidDataException("TLG0 SDS raw length is too large.");
            }

            var rawLength = (int)rawLength32;
            var rawStart = 15;
            var rawEnd = rawStart + rawLength;
            if (rawEnd < rawStart || rawEnd > data.Length)
            {
                throw new InvalidDataException("Invalid TLG0 SDS raw range.");
            }

            var raw = Slice(data, rawStart, rawLength);
            return DecodeAny(raw, directory, options, depth + 1);
        }

        private static TlgDecodedImage DecodeMux(byte[] data, string directory, TlgDecodeOptions options, int depth)
        {
            if (data.Length < 20)
            {
                throw new InvalidDataException("Invalid TLGmux header.");
            }

            var pos = 20;
            var entries = new List<MuxEntryInfo>();
            while (pos + 8 <= data.Length)
            {
                var tag = Slice(data, pos, 4);
                var size = (int)ReadUInt32(data, pos + 4);
                pos += 8;

                if (size < 0 || pos + size > data.Length)
                {
                    throw new InvalidDataException("Invalid TLGmux chunk size.");
                }

                if (IsTerminator(tag, size))
                {
                    break;
                }

                if (ByteEquals(tag, CmuxTag))
                {
                    ParseCmuxEntries(data, pos, size, entries);
                }

                pos += size;
            }

            if (entries.Count == 0)
            {
                throw new InvalidDataException("CMUX entries were not found.");
            }

            // 按原始实现，CMUX 偏移通常以“当前流位置（终止 chunk 之后）”为基准。
            var muxBaseOffset = pos;

            if (options.MuxEntryIndex.HasValue)
            {
                var index = options.MuxEntryIndex.Value;
                if (index < 0 || index >= entries.Count)
                {
                    throw new InvalidDataException("MuxEntryIndex is out of range.");
                }

                return DecodeMuxEntry(data, directory, options, depth, muxBaseOffset, entries[index]);
            }

            Exception firstError = null;
            for (var i = 0; i < entries.Count; i++)
            {
                try
                {
                    return DecodeMuxEntry(data, directory, options, depth, muxBaseOffset, entries[i]);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is FileNotFoundException)
                {
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                }
            }

            throw new InvalidDataException("Failed to decode all CMUX entries.", firstError);
        }

        private static void ParseCmuxEntries(byte[] data, int payloadPos, int payloadSize, List<MuxEntryInfo> entries)
        {
            if (payloadSize < 4)
            {
                throw new InvalidDataException("CMUX payload is too small.");
            }

            var payloadEnd = payloadPos + payloadSize;
            var count = ReadUInt32(data, payloadPos);
            if (count > int.MaxValue)
            {
                throw new InvalidDataException("CMUX entry count is too large.");
            }

            var entryPos = payloadPos + 4;
            for (var i = 0; i < (int)count; i++)
            {
                if (entryPos + 24 > payloadEnd)
                {
                    throw new InvalidDataException("CMUX entry exceeds payload range.");
                }

                entries.Add(new MuxEntryInfo
                {
                    RelativeStreamOffset = unchecked((long)ReadUInt64(data, entryPos + 16))
                });

                entryPos += 24;
            }
        }

        private static TlgDecodedImage DecodeMuxEntry(
            byte[] muxData,
            string directory,
            TlgDecodeOptions options,
            int depth,
            int muxBaseOffset,
            MuxEntryInfo entry)
        {
            var targetOffset = checked(muxBaseOffset + entry.RelativeStreamOffset);
            if (targetOffset < 0 || targetOffset >= muxData.Length)
            {
                throw new InvalidDataException("CMUX entry offset is out of range.");
            }

            var nestedOffset = (int)targetOffset;
            var nestedData = Slice(muxData, nestedOffset, muxData.Length - nestedOffset);
            return DecodeAny(nestedData, directory, options, depth + 1);
        }

        private static TlgDecodedImage DecodeQoiCore(
            byte[] data,
            int begin,
            int end,
            uint? expectedFingerprint,
            int autoPhaseWindow)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!HasPrefix(data, TlgQoiHeader))
            {
                throw new InvalidDataException("Not a TLGqoi stream.");
            }

            var colorType = data[11];
            if (colorType != 3 && colorType != 4)
            {
                throw new InvalidDataException("Unsupported TLGqoi color type.");
            }

            var width32 = ReadUInt32(data, 12);
            var height32 = ReadUInt32(data, 16);
            if (width32 == 0 || height32 == 0 || width32 > int.MaxValue || height32 > int.MaxValue)
            {
                throw new InvalidDataException("Invalid image size.");
            }
            var width = (int)width32;
            var height = (int)height32;

            var pos = 20;
            byte[] qhdrPayload = null;
            while (pos + 8 <= data.Length)
            {
                // 通用 chunk 头：Tag(4) + Size(4)
                var tag = Slice(data, pos, 4);
                var size = (int)ReadUInt32(data, pos + 4);
                pos += 8;

                if (size < 0 || pos + size > data.Length)
                {
                    throw new InvalidDataException("Invalid chunk size.");
                }

                if (IsTerminator(tag, size))
                {
                    break;
                }

                if (ByteEquals(tag, QhdrTag))
                {
                    if (size < 48)
                    {
                        throw new InvalidDataException("Invalid QHDR size.");
                    }

                    qhdrPayload = Slice(data, pos, 48);
                }

                pos += size;
            }

            if (qhdrPayload == null)
            {
                throw new InvalidDataException("QHDR chunk not found.");
            }

            var fingerprint = ReadUInt32(qhdrPayload, 0);
            if (expectedFingerprint.HasValue && expectedFingerprint.Value != fingerprint)
            {
                throw new InvalidDataException("QREF fingerprint does not match target TLGqoi.");
            }

            var phaseEndHint = (int)ReadUInt32(qhdrPayload, 4);
            var sectionHeight = (int)ReadUInt32(qhdrPayload, 8);
            var sectionCount = (int)ReadUInt32(qhdrPayload, 12);
            if (sectionHeight <= 0 || sectionCount <= 0)
            {
                throw new InvalidDataException("Unsupported TLGqoi section metadata.");
            }

            var dtblOffset = ReadUInt64(qhdrPayload, 24);
            var rtblOffset = ReadUInt64(qhdrPayload, 32);
            var baseDataPos = (long)pos;
            var dtblPos = checked(baseDataPos + (long)dtblOffset);
            var rtblPos = checked(baseDataPos + (long)rtblOffset);

            var dtbl = ParseVarintTable(data, dtblPos, DtblTag);
            var rtbl = ParseVarintTable(data, rtblPos, RtblTag);
            var dtblValues = ChooseVarintTableValues(dtbl, sectionCount, true);
            var rtblValues = ChooseVarintTableValues(rtbl, sectionCount, false);

            if (!IsValidDtbl(dtblValues, sectionCount))
            {
                throw new InvalidDataException("Invalid DTBL contents.");
            }

            if (!IsValidRtbl(rtblValues, sectionCount))
            {
                throw new InvalidDataException("Invalid RTBL contents.");
            }

            ResolvePhase(phaseEndHint, ref begin, ref end, autoPhaseWindow);

            var dtOffsets = new ulong[sectionCount];
            var dtCounters = new ulong[sectionCount];
            var rtOffsets = new ulong[sectionCount];
            for (var i = 0; i < sectionCount; i++)
            {
                dtOffsets[i] = dtblValues[1 + i * 2];
                dtCounters[i] = dtblValues[2 + i * 2];
                rtOffsets[i] = rtblValues[1 + i];
            }

            var pixels = new uint[checked(width * height)];
            ulong pos1 = (ulong)baseDataPos;
            ulong pos2 = (ulong)rtbl.DataEnd;

            for (var section = 0; section < sectionCount; section++)
            {
                var yStart = section * sectionHeight;
                if (yStart >= height)
                {
                    break;
                }

                var lines = sectionHeight;
                if (yStart + lines > height)
                {
                    lines = height - yStart;
                }

                var worker = new SectionWorker(data, pos1, pos2, dtCounters[section], begin, end, width, yStart, lines);
                worker.DecodeInto(pixels);

                // 每个 section 的两路输入都按表中偏移推进。
                pos1 = checked(pos1 + dtOffsets[section]);
                pos2 = checked(pos2 + rtOffsets[section]);
            }

            var bgra = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                var o = i * 4;
                bgra[o + 0] = (byte)(c & 0xFF);
                bgra[o + 1] = (byte)((c >> 8) & 0xFF);
                bgra[o + 2] = (byte)((c >> 16) & 0xFF);
                bgra[o + 3] = (byte)((c >> 24) & 0xFF);
            }

            return new TlgDecodedImage(width, height, bgra);
        }

        private static void ResolvePhase(int phaseEndHint, ref int begin, ref int end, int autoPhaseWindow)
        {
            // 调用方显式指定则直接使用。
            if (begin >= 0 && end > begin)
            {
                return;
            }

            var window = NormalizeAutoPhaseWindow(autoPhaseWindow);

            // 仅指定 end 时，按窗口反推 begin。
            if (begin < 0 && end > 0)
            {
                begin = end - window;
                if (begin < 0)
                {
                    begin = 0;
                }

                if (end <= begin)
                {
                    end = begin + 1;
                }

                return;
            }

            // 仅指定 begin 时，按窗口推导 end。
            if (begin >= 0 && end <= 0)
            {
                end = begin + window;
                if (end <= begin)
                {
                    end = begin + 1;
                }

                return;
            }

            // 自动模式下，优先按 QHDR 提示推断相位窗口。
            if (phaseEndHint > 0)
            {
                end = phaseEndHint;
                begin = end - window;
                if (begin < 0)
                {
                    begin = 0;
                }

                if (end <= begin)
                {
                    end = begin + 1;
                }

                return;
            }

            begin = 0;
            end = 1;
        }

        private static int NormalizeAutoPhaseWindow(int window)
        {
            return window > 0 ? window : DefaultPhaseWindow;
        }

        private static string ResolveReferencePath(string directory, TlgReferenceInfo reference)
        {
            var direct = reference.Path;
            if (!Path.IsPathRooted(direct))
            {
                direct = Path.Combine(directory, direct);
            }

            if (File.Exists(direct))
            {
                return direct;
            }

            foreach (var candidate in Directory.GetFiles(directory))
            {
                if (TryReadQoiFingerprint(candidate, out var fp) && fp == reference.Fingerprint)
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Cannot locate TLGqoi file referenced by TLGref.", reference.Path);
        }

        private static bool TryReadQoiFingerprint(string path, out uint fingerprint)
        {
            fingerprint = 0;
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs, Encoding.ASCII, true))
                {
                    var header = br.ReadBytes(11);
                    if (!ByteEquals(header, TlgQoiHeader))
                    {
                        return false;
                    }

                    fs.Position = 28;
                    fingerprint = br.ReadUInt32();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static TlgReferenceInfo ParseReference(byte[] data)
        {
            if (!HasPrefix(data, TlgRefHeader))
            {
                throw new InvalidDataException("Not a TLGref stream.");
            }

            var pos = 20;
            while (pos + 8 <= data.Length)
            {
                var tag = Slice(data, pos, 4);
                var size = (int)ReadUInt32(data, pos + 4);
                pos += 8;

                if (size < 0 || pos + size > data.Length)
                {
                    throw new InvalidDataException("Invalid TLGref chunk size.");
                }

                if (IsTerminator(tag, size))
                {
                    break;
                }

                if (ByteEquals(tag, QrefTag))
                {
                    if (size < 16)
                    {
                        throw new InvalidDataException("Invalid QREF size.");
                    }

                    var payload = Slice(data, pos, size);
                    var fingerprint = ReadUInt32(payload, 0);
                    var begin = ReadInt32(payload, 4);
                    var end = ReadInt32(payload, 8);
                    var pathByteLen = (int)ReadUInt32(payload, 12);
                    if (pathByteLen < 0 || 16 + pathByteLen > payload.Length)
                    {
                        throw new InvalidDataException("Invalid QREF path length.");
                    }

                    var pathBytes = Slice(payload, 16, pathByteLen);
                    var path = Encoding.Unicode.GetString(pathBytes, 0, pathBytes.Length);
                    var zero = path.IndexOf('\0');
                    if (zero >= 0)
                    {
                        path = path.Substring(0, zero);
                    }

                    return new TlgReferenceInfo
                    {
                        Fingerprint = fingerprint,
                        Begin = begin,
                        End = end,
                        Path = path
                    };
                }

                pos += size;
            }

            throw new InvalidDataException("QREF chunk was not found in TLGref.");
        }

        private static VarintTable ParseVarintTable(byte[] data, long tablePos, byte[] expectedTag)
        {
            if (tablePos < 0 || tablePos + 8 > data.Length)
            {
                throw new InvalidDataException("Invalid table offset.");
            }

            var pos = (int)tablePos;
            var tag = Slice(data, pos, 4);
            if (!ByteEquals(tag, expectedTag))
            {
                throw new InvalidDataException("Missing expected table marker.");
            }

            var payloadLen = (int)ReadUInt32(data, pos + 4);
            var payloadStart = pos + 8;
            var payloadEnd = payloadStart + payloadLen;
            if (payloadLen < 0 || payloadEnd > data.Length)
            {
                throw new InvalidDataException("Table payload is out of range.");
            }

            var rawValues = ReadVarints(data, payloadStart, payloadLen);
            var expandedValues = ExpandVarintsWithBackReference(rawValues, out var hasBackReference);
            return new VarintTable
            {
                RawValues = rawValues,
                ExpandedValues = expandedValues,
                HasBackReference = hasBackReference,
                DataEnd = payloadEnd
            };
        }

        private static List<ulong> ChooseVarintTableValues(VarintTable table, int sectionCount, bool isDtbl)
        {
            // 没有触发回指时直接走原始序列（与旧实现一致）。
            if (!table.HasBackReference)
            {
                return table.RawValues;
            }

            // 若触发了回指，优先使用展开序列；但若展开结果不满足表约束，回退到原始序列。
            var expandedValid = isDtbl
                ? IsValidDtbl(table.ExpandedValues, sectionCount)
                : IsValidRtbl(table.ExpandedValues, sectionCount);
            if (expandedValid)
            {
                return table.ExpandedValues;
            }

            return table.RawValues;
        }

        private static bool IsValidDtbl(List<ulong> values, int sectionCount)
        {
            return values.Count >= 1 + sectionCount * 2 && values[0] >= (ulong)(sectionCount * 2);
        }

        private static bool IsValidRtbl(List<ulong> values, int sectionCount)
        {
            return values.Count >= 1 + sectionCount && values[0] >= (ulong)sectionCount;
        }

        private static List<ulong> ReadVarints(byte[] data, int start, int length)
        {
            // 与原始实现一致：7-bit continuation varint。
            var values = new List<ulong>();
            var end = start + length;
            var pos = start;
            while (pos < end)
            {
                ulong value = 0;
                var shift = 0;
                while (true)
                {
                    if (pos >= end)
                    {
                        return values;
                    }

                    var b = data[pos++];
                    value |= ((ulong)(b & 0x7F)) << shift;
                    if (b < 0x80)
                    {
                        break;
                    }

                    shift += 7;
                    if (shift >= 64)
                    {
                        throw new InvalidDataException("Malformed varint sequence.");
                    }
                }

                values.Add(value);
            }

            return values;
        }

        private static List<ulong> ExpandVarintsWithBackReference(List<ulong> source, out bool hasBackReference)
        {
            // - 若读到的值落在“当前输出向量地址区间”中，则复制该位置已有项；
            // - 否则按字面值写入。
            //
            // 第 i 项的“逻辑地址”定义为 i*8，因此回指条件为：
            //   value % 8 == 0 && value/8 < currentCount
            // 可覆盖“表内回指展开”行为，同时保持跨平台稳定性。
            var expanded = new List<ulong>(source.Count);
            hasBackReference = false;
            for (var i = 0; i < source.Count; i++)
            {
                var value = source[i];
                if (TryResolveVarintBackReference(value, expanded, out var resolved))
                {
                    expanded.Add(resolved);
                    hasBackReference = true;
                }
                else
                {
                    expanded.Add(value);
                }
            }

            return expanded;
        }

        private static bool TryResolveVarintBackReference(ulong value, List<ulong> expanded, out ulong resolved)
        {
            resolved = 0;
            if ((value & 0x7UL) != 0)
            {
                return false;
            }

            var index = value >> 3;
            if (index > int.MaxValue || index >= (ulong)expanded.Count)
            {
                return false;
            }

            resolved = expanded[(int)index];
            return true;
        }

        private static bool IsTerminator(byte[] tag, int size)
        {
            return size == 0 && tag.Length == 4 && tag[0] == 0 && tag[1] == 0 && tag[2] == 0 && tag[3] == 0;
        }

        private static bool HasPrefix(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ByteEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] Slice(byte[] data, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(data, offset, result, 0, count);
            return result;
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset]
                        | (data[offset + 1] << 8)
                        | (data[offset + 2] << 16)
                        | (data[offset + 3] << 24));
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return unchecked((int)ReadUInt32(data, offset));
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            var lo = ReadUInt32(data, offset);
            var hi = ReadUInt32(data, offset + 4);
            return ((ulong)hi << 32) | lo;
        }

        private sealed class VarintTable
        {
            public List<ulong> RawValues { get; set; }
            public List<ulong> ExpandedValues { get; set; }
            public bool HasBackReference { get; set; }
            public int DataEnd { get; set; }
        }

        private sealed class TlgReferenceInfo
        {
            public uint Fingerprint { get; set; }
            public int Begin { get; set; }
            public int End { get; set; }
            public string Path { get; set; }
        }

        private sealed class MuxEntryInfo
        {
            public long RelativeStreamOffset { get; set; }
        }

        private sealed class SectionWorker
        {
            // _codePos: 主码流（颜色 token）位置
            // _ctrlPos: 控制流（RLE 扩展长度）位置
            private readonly byte[] _data;
            private readonly int _width;
            private readonly int _yStart;
            private readonly int _lines;
            private readonly int _phaseBegin;
            private readonly int _phasePeriod;
            private readonly ulong _initialCounter;

            private readonly uint[] _cache = new uint[64];
            private uint _lastColor;
            private int _runLeft;

            private ulong _codePos;
            private ulong _ctrlPos;
            private ulong _counter;
            private bool _counterLoaded;

            private int _ctrlBufferIndex;
            private readonly byte[][] _ctrlBuffers = new byte[2][];
            private readonly int[] _ctrlLengths = new int[2];
            private int _ctrlReadPos;
            private byte[] _previousControlBlock = new byte[0];

            public SectionWorker(
                byte[] data,
                ulong codePos,
                ulong ctrlPos,
                ulong initialCounter,
                int phaseBegin,
                int phasePeriod,
                int width,
                int yStart,
                int lines)
            {
                _data = data;
                _codePos = codePos;
                _ctrlPos = ctrlPos;
                _initialCounter = initialCounter;
                _phaseBegin = phaseBegin;
                _phasePeriod = phasePeriod;
                _width = width;
                _yStart = yStart;
                _lines = lines;
                ResetColorState();
            }

            public void DecodeInto(uint[] pixels)
            {
                for (var y = 0; y < _lines; y++)
                {
                    var dst = (_yStart + y) * _width;
                    var phase = _phaseBegin;
                    // 每行按相位周期驱动，只有落在相位命中的样本才真正写像素。
                    var total = checked(_width * _phasePeriod);
                    for (var i = 0; i < total; i++)
                    {
                        if (_runLeft == 0)
                        {
                            if (_counter == 0 && !ResetChunk())
                            {
                                throw new InvalidDataException("Failed to reset TLGqoi section decoder.");
                            }

                            _counter--;

                            if (!TryParseToken(out var tokenRun, out var color))
                            {
                                throw new InvalidDataException("Corrupted TLGqoi token stream.");
                            }

                            if (!TryReadControlVarint(out var extra))
                            {
                                throw new InvalidDataException("Corrupted TLGqoi control stream.");
                            }

                            if (extra > int.MaxValue)
                            {
                                throw new InvalidDataException("Control varint is too large.");
                            }

                            _runLeft = checked(tokenRun + (int)extra);
                            _lastColor = color;
                        }

                        _runLeft--;
                        if (phase > 0)
                        {
                            phase--;
                        }
                        else
                        {
                            phase = _phasePeriod - 1;
                            pixels[dst++] = _lastColor;
                        }
                    }
                }
            }

            private bool ResetChunk()
            {
                // 每个 chunk 以两个固定 token 开场：
                // run=1,color=0x00000000 和 run=1,color=0xFF000000
                ResetColorState();
                if (!TryParseToken(out var run1, out var c1))
                {
                    return false;
                }

                if (!TryParseToken(out var run2, out var c2))
                {
                    return false;
                }

                if (!_counterLoaded)
                {
                    _counter = _initialCounter;
                    _counterLoaded = true;
                }
                else
                {
                    _counter = 0;
                }

                _runLeft = 0;
                if (!TryReadControlVarint(out var startRun) || startRun > int.MaxValue)
                {
                    return false;
                }

                _runLeft = (int)startRun;
                return run1 == 1 && run2 == 1 && c1 == 0 && c2 == 0xFF000000 && _counter != 0;
            }

            private void ResetColorState()
            {
                Array.Clear(_cache, 0, _cache.Length);
                _lastColor = 0xFF000000u;
            }

            private bool TryParseToken(out int run, out uint color)
            {
                run = 0;
                color = 0;
                if (_codePos >= (ulong)_data.Length)
                {
                    return false;
                }

                var b0 = _data[(int)_codePos++];
                int tokenType;
                int tokenLength;
                if (b0 == 0xFF)
                {
                    // QOI_OP_RGBA
                    tokenType = 10;
                    tokenLength = 5;
                }
                else if (b0 == 0xFE)
                {
                    // QOI_OP_RGB
                    tokenType = 8;
                    tokenLength = 4;
                }
                else
                {
                    // 00:INDEX, 01:DIFF, 10:LUMA, 11:RUN
                    switch (b0 >> 6)
                    {
                        case 0:
                            tokenType = 2;
                            break;
                        case 1:
                            tokenType = 4;
                            break;
                        case 2:
                            tokenType = 6;
                            break;
                        default:
                            tokenType = 1;
                            break;
                    }
                    tokenLength = tokenType == 6 ? 2 : 1;
                }

                if (_codePos + (ulong)(tokenLength - 1) > (ulong)_data.Length)
                {
                    return false;
                }

                var b1 = tokenLength > 1 ? _data[(int)_codePos++] : (byte)0;
                var b2 = tokenLength > 2 ? _data[(int)_codePos++] : (byte)0;
                var b3 = tokenLength > 3 ? _data[(int)_codePos++] : (byte)0;
                var b4 = tokenLength > 4 ? _data[(int)_codePos++] : (byte)0;

                var prev = _lastColor;
                var pb = (int)(prev & 0xFF);
                var pg = (int)((prev >> 8) & 0xFF);
                var pr = (int)((prev >> 16) & 0xFF);
                var pa = (int)((prev >> 24) & 0xFF);

                switch (tokenType)
                {
                    case 1:
                        run = (b0 & 0x3F) + 1;
                        color = prev;
                        _lastColor = color;
                        return true;

                    case 2:
                        color = _cache[b0 & 0x3F];
                        run = 1;
                        break;

                    case 4:
                    {
                        var nb = (pb + (b0 & 0x03) - 2) & 0xFF;
                        var ng = (pg + ((b0 >> 2) & 0x03) - 2) & 0xFF;
                        var nr = (pr + ((b0 >> 4) & 0x03) - 2) & 0xFF;
                        color = (uint)(nb | (ng << 8) | (nr << 16) | (pa << 24));
                        run = 1;
                        break;
                    }

                    case 6:
                    {
                        var dg = (b0 & 0x3F) - 32;
                        var nb = (pb + dg + ((b1 & 0x0F) - 8)) & 0xFF;
                        var ng = (pg + dg) & 0xFF;
                        var nr = (pr + dg + (((b1 >> 4) & 0x0F) - 8)) & 0xFF;
                        color = (uint)(nb | (ng << 8) | (nr << 16) | (pa << 24));
                        run = 1;
                        break;
                    }

                    case 8:
                        color = (uint)(b3 | (b2 << 8) | (b1 << 16) | (pa << 24));
                        run = 1;
                        break;

                    case 10:
                        color = (uint)(b3 | (b2 << 8) | (b1 << 16) | (b4 << 24));
                        run = 1;
                        break;
                }

                _cache[HashColor(color)] = color;
                _lastColor = color;
                return true;
            }

            private static int HashColor(uint color)
            {
                var b = (int)(color & 0xFF);
                var g = (int)((color >> 8) & 0xFF);
                var r = (int)((color >> 16) & 0xFF);
                var a = (int)((color >> 24) & 0xFF);
                return (7 * b + 5 * g + 3 * r + 11 * a) & 0x3F;
            }

            private bool TryReadControlVarint(out ulong value)
            {
                value = 0;
                var shift = 0;
                while (true)
                {
                    if (_ctrlReadPos >= _ctrlLengths[_ctrlBufferIndex])
                    {
                        if (!TryRefillControlBlock())
                        {
                            return false;
                        }
                    }

                    var b = _ctrlBuffers[_ctrlBufferIndex][_ctrlReadPos++];
                    value |= ((ulong)(b & 0x7F)) << shift;
                    if (b < 0x80)
                    {
                        return true;
                    }

                    shift += 7;
                    if (shift >= 64)
                    {
                        return false;
                    }
                }
            }

            private bool TryRefillControlBlock()
            {
                if (_ctrlPos + 4 > (ulong)_data.Length)
                {
                    return false;
                }

                var header = ReadUInt32(_data, (int)_ctrlPos);
                _ctrlPos += 4;
                if (header == 0)
                {
                    return false;
                }

                var compressedLength = (int)(header >> 16);
                var outputLength = (int)(header & 0x7FFF);
                if (outputLength == 0)
                {
                    outputLength = 0x8000;
                }

                // bit15=1 表示本块解压时要带上一块字典。
                var useDictionary = (header & 0x8000) != 0;
                if (_ctrlPos + (ulong)compressedLength > (ulong)_data.Length)
                {
                    return false;
                }

                var compressed = new byte[compressedLength];
                Buffer.BlockCopy(_data, (int)_ctrlPos, compressed, 0, compressedLength);
                _ctrlPos += (ulong)compressedLength;

                var dictionary = useDictionary ? _previousControlBlock : new byte[0];
                var block = DecodeLz4Block(compressed, outputLength, dictionary);
                _previousControlBlock = block;

                _ctrlBufferIndex ^= 1;
                _ctrlBuffers[_ctrlBufferIndex] = block;
                _ctrlLengths[_ctrlBufferIndex] = block.Length;
                _ctrlReadPos = 0;
                return true;
            }

            private static byte[] DecodeLz4Block(byte[] source, int outputLength, byte[] dictionary)
            {
                // 这里实现的是 LZ4 block 解码（非 frame），并支持外部字典续接。
                var dictionaryLength = dictionary.Length;
                var buffer = new byte[dictionaryLength + outputLength];
                if (dictionaryLength > 0)
                {
                    Buffer.BlockCopy(dictionary, 0, buffer, 0, dictionaryLength);
                }

                var src = 0;
                var dst = dictionaryLength;
                while (src < source.Length)
                {
                    var token = source[src++];

                    var literalLength = token >> 4;
                    if (literalLength == 15)
                    {
                        while (true)
                        {
                            if (src >= source.Length)
                            {
                                throw new InvalidDataException("Invalid LZ4 literal length.");
                            }

                            var b = source[src++];
                            literalLength += b;
                            if (b != 255)
                            {
                                break;
                            }
                        }
                    }

                    if (src + literalLength > source.Length || dst + literalLength > buffer.Length)
                    {
                        throw new InvalidDataException("Invalid LZ4 literal copy.");
                    }

                    Buffer.BlockCopy(source, src, buffer, dst, literalLength);
                    src += literalLength;
                    dst += literalLength;

                    if (src >= source.Length)
                    {
                        break;
                    }

                    if (src + 2 > source.Length)
                    {
                        throw new InvalidDataException("Invalid LZ4 offset.");
                    }

                    var offset = source[src] | (source[src + 1] << 8);
                    src += 2;
                    if (offset == 0 || offset > dst)
                    {
                        throw new InvalidDataException("Invalid LZ4 match offset.");
                    }

                    var matchLength = token & 0x0F;
                    if (matchLength == 15)
                    {
                        while (true)
                        {
                            if (src >= source.Length)
                            {
                                throw new InvalidDataException("Invalid LZ4 match length.");
                            }

                            var b = source[src++];
                            matchLength += b;
                            if (b != 255)
                            {
                                break;
                            }
                        }
                    }

                    matchLength += 4;
                    if (dst + matchLength > buffer.Length)
                    {
                        throw new InvalidDataException("Invalid LZ4 match copy.");
                    }

                    var match = dst - offset;
                    for (var i = 0; i < matchLength; i++)
                    {
                        buffer[dst++] = buffer[match++];
                    }
                }

                if (dst != dictionaryLength + outputLength)
                {
                    throw new InvalidDataException("LZ4 decoded size mismatch.");
                }

                var output = new byte[outputLength];
                Buffer.BlockCopy(buffer, dictionaryLength, output, 0, outputLength);
                return output;
            }
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FreeMote.Tlg.Managed
{
    public static class TlgParser
    {
        private static readonly byte[] Tlg0SdsHeader = Encoding.ASCII.GetBytes("TLG0.0\0sds\x1a");
        private static readonly byte[] Tlg5RawHeader = Encoding.ASCII.GetBytes("TLG5.0\0raw\x1a");
        private static readonly byte[] Tlg6RawHeader = Encoding.ASCII.GetBytes("TLG6.0\0raw\x1a");
        private static readonly byte[] TlgQoiRawHeader = Encoding.ASCII.GetBytes("TLGqoi\0raw\x1a");
        private static readonly byte[] TlgRefRawHeader = Encoding.ASCII.GetBytes("TLGref\0raw\x1a");
        private static readonly byte[] TlgMuxIdxHeader = Encoding.ASCII.GetBytes("TLGmux\0idx\x1a");

        public static TlgFile Parse(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            using (var fs = File.OpenRead(filePath))
            {
                return Parse(fs);
            }
        }

        public static TlgFile Parse(Stream stream, bool leaveOpen = true)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException("The stream must be readable.", nameof(stream));
            }

            Stream working = stream;
            var ownsWorking = false;

            if (!stream.CanSeek)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                working = ms;
                ownsWorking = true;
            }

            try
            {
                return ParseCore(working);
            }
            finally
            {
                if (ownsWorking || !leaveOpen)
                {
                    working.Dispose();
                }
            }
        }

        private static TlgFile ParseCore(Stream stream)
        {
            stream.Position = 0;
            using (var br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                var header = ReadExact(br, 11);
                if (Matches(header, Tlg0SdsHeader))
                {
                    return ParseSds(br);
                }

                return ParseRaw(br, header, stream.Length);
            }
        }

        private static TlgFile ParseSds(BinaryReader br)
        {
            var stream = br.BaseStream;
            var file = new TlgFile
            {
                IsSdsContainer = true,
                SdsRawLength = br.ReadUInt32()
            };

            var rawStart = stream.Position;
            var rawEnd = rawStart + file.SdsRawLength;
            if (rawEnd < rawStart || rawEnd > stream.Length)
            {
                throw new InvalidDataException("Invalid TLG0 SDS raw length.");
            }

            var innerHeader = ReadExact(br, 11);
            var rawFile = ParseRaw(br, innerHeader, rawEnd);

            file.Format = rawFile.Format;
            file.ColorType = rawFile.ColorType;
            file.Width = rawFile.Width;
            file.Height = rawFile.Height;
            file.QoiHeader = rawFile.QoiHeader;

            foreach (var chunk in rawFile.Chunks)
            {
                file.MutableChunks.Add(chunk);
            }

            foreach (var reference in rawFile.References)
            {
                file.MutableReferences.Add(reference);
            }

            foreach (var muxEntry in rawFile.MuxEntries)
            {
                file.MutableMuxEntries.Add(muxEntry);
            }

            if (stream.Position != rawEnd)
            {
                stream.Position = rawEnd;
            }

            ParseSdsTailChunks(br, file);
            return file;
        }

        private static TlgFile ParseRaw(BinaryReader br, byte[] header, long limitPosition)
        {
            var format = DetectFormat(header);
            if (format == TlgFormatKind.Unknown)
            {
                throw new InvalidDataException("Unsupported TLG header.");
            }

            var file = new TlgFile
            {
                Format = format
            };

            file.ColorType = br.ReadByte();
            file.Width = br.ReadUInt32();
            file.Height = br.ReadUInt32();

            switch (format)
            {
                case TlgFormatKind.TlgQoi:
                case TlgFormatKind.TlgRef:
                case TlgFormatKind.TlgMux:
                    ParseRawChunks(br, file, limitPosition);
                    break;
                case TlgFormatKind.Tlg5:
                case TlgFormatKind.Tlg6:
                    // Existing TLG5/TLG6 image data starts right after this fixed header.
                    break;
                default:
                    throw new InvalidDataException("Unsupported TLG format kind.");
            }

            return file;
        }

        private static void ParseRawChunks(BinaryReader br, TlgFile file, long limitPosition)
        {
            var stream = br.BaseStream;
            while (stream.Position + 8 <= limitPosition)
            {
                var tagBytes = br.ReadBytes(4);
                if (tagBytes.Length != 4)
                {
                    throw new EndOfStreamException("Failed to read chunk tag.");
                }

                var size = br.ReadUInt32();
                if (IsChunkTerminator(tagBytes, size))
                {
                    return;
                }

                var payloadOffset = stream.Position;
                var payloadEnd = payloadOffset + size;
                if (payloadEnd < payloadOffset || payloadEnd > limitPosition)
                {
                    throw new InvalidDataException("Chunk size is out of range.");
                }

                var tag = Encoding.ASCII.GetString(tagBytes, 0, 4);
                file.MutableChunks.Add(new TlgChunk
                {
                    Tag = tag,
                    Size = size,
                    PayloadOffset = payloadOffset
                });

                ParseKnownChunk(br, file, tag, size);

                if (stream.Position != payloadEnd)
                {
                    stream.Position = payloadEnd;
                }
            }
        }

        private static void ParseKnownChunk(BinaryReader br, TlgFile file, string tag, uint size)
        {
            switch (tag)
            {
                case "QHDR":
                    file.QoiHeader = ParseQhdr(br, size);
                    break;
                case "QREF":
                    file.MutableReferences.Add(ParseQref(br, size));
                    break;
                case "CMUX":
                    ParseCmux(br, size, file);
                    break;
                case "tags":
                    ParseSdsTags(br, size, file);
                    break;
                default:
                    // Unknown chunk type, skip by caller.
                    break;
            }
        }

        private static TlgQoiHeader ParseQhdr(BinaryReader br, uint size)
        {
            if (size < 48)
            {
                throw new InvalidDataException("QHDR chunk is too small.");
            }

            var payload = ReadExact(br, 48);
            return new TlgQoiHeader
            {
                Fingerprint = BitConverter.ToUInt32(payload, 0),
                PhaseEndHint = BitConverter.ToUInt32(payload, 4),
                SectionHeight = BitConverter.ToUInt32(payload, 8),
                SectionCount = BitConverter.ToUInt32(payload, 12),
                Unknown10H = BitConverter.ToUInt64(payload, 16),
                DtblOffset = BitConverter.ToUInt64(payload, 24),
                RtblOffset = BitConverter.ToUInt64(payload, 32),
                DataLengthHint = BitConverter.ToUInt64(payload, 40)
            };
        }

        private static TlgReferenceTarget ParseQref(BinaryReader br, uint size)
        {
            if (size < 16)
            {
                throw new InvalidDataException("QREF chunk is too small.");
            }

            var payload = ReadExact(br, checked((int)size));
            var pathByteLength = BitConverter.ToUInt32(payload, 12);
            if (pathByteLength > payload.Length - 16)
            {
                throw new InvalidDataException("QREF path length is invalid.");
            }

            var pathBytes = new byte[pathByteLength];
            Buffer.BlockCopy(payload, 16, pathBytes, 0, (int)pathByteLength);
            var path = DecodeUtf16(pathBytes);

            return new TlgReferenceTarget
            {
                Fingerprint = BitConverter.ToUInt32(payload, 0),
                Begin = BitConverter.ToInt32(payload, 4),
                End = BitConverter.ToInt32(payload, 8),
                PathByteLength = pathByteLength,
                Path = path
            };
        }

        private static void ParseCmux(BinaryReader br, uint size, TlgFile file)
        {
            if (size < 4)
            {
                throw new InvalidDataException("CMUX chunk is too small.");
            }

            var payload = ReadExact(br, checked((int)size));
            var count = BitConverter.ToInt32(payload, 0);
            if (count < 0)
            {
                throw new InvalidDataException("CMUX entry count is invalid.");
            }

            var offset = 4;
            for (var i = 0; i < count && offset + 24 <= payload.Length; i++)
            {
                file.MutableMuxEntries.Add(new TlgMuxEntry
                {
                    PartialX = BitConverter.ToUInt32(payload, offset),
                    PartialY = BitConverter.ToUInt32(payload, offset + 4),
                    PartialWidth = BitConverter.ToUInt32(payload, offset + 8),
                    PartialHeight = BitConverter.ToUInt32(payload, offset + 12),
                    RelativeStreamOffsetRaw = BitConverter.ToUInt64(payload, offset + 16)
                });

                offset += 24;
            }
        }

        private static void ParseSdsTailChunks(BinaryReader br, TlgFile file)
        {
            var stream = br.BaseStream;
            while (stream.Position + 8 <= stream.Length)
            {
                var tagBytes = br.ReadBytes(4);
                if (tagBytes.Length != 4)
                {
                    return;
                }

                var size = br.ReadUInt32();
                if (IsChunkTerminator(tagBytes, size))
                {
                    return;
                }

                var payloadOffset = stream.Position;
                var payloadEnd = payloadOffset + size;
                if (payloadEnd < payloadOffset || payloadEnd > stream.Length)
                {
                    throw new InvalidDataException("SDS chunk size is out of range.");
                }

                var tag = Encoding.ASCII.GetString(tagBytes, 0, 4);
                if (tag == "tags")
                {
                    ParseSdsTags(br, size, file);
                }
                else
                {
                    stream.Position = payloadEnd;
                }
            }
        }

        private static void ParseSdsTags(BinaryReader br, uint size, TlgFile file)
        {
            var payload = ReadExact(br, checked((int)size));
            var index = 0;

            while (index < payload.Length)
            {
                var nameLength = ReadDecimalLength(payload, ref index);
                RequireDelimiter(payload, ref index, ':');
                RequireBounds(payload, index, nameLength);
                var name = Encoding.UTF8.GetString(payload, index, nameLength);
                index += nameLength;

                RequireDelimiter(payload, ref index, '=');
                var valueLength = ReadDecimalLength(payload, ref index);
                RequireDelimiter(payload, ref index, ':');
                RequireBounds(payload, index, valueLength);
                var value = Encoding.UTF8.GetString(payload, index, valueLength);
                index += valueLength;

                RequireDelimiter(payload, ref index, ',');
                file.MutableTags[name] = value;
            }
        }

        private static int ReadDecimalLength(byte[] data, ref int index)
        {
            if (index >= data.Length || data[index] < '0' || data[index] > '9')
            {
                throw new InvalidDataException("Malformed SDS tag data.");
            }

            var start = index;
            while (index < data.Length && data[index] >= '0' && data[index] <= '9')
            {
                index++;
            }

            var number = Encoding.ASCII.GetString(data, start, index - start);
            return int.Parse(number, NumberStyles.None, CultureInfo.InvariantCulture);
        }

        private static void RequireDelimiter(byte[] data, ref int index, char delimiter)
        {
            if (index >= data.Length || data[index] != (byte)delimiter)
            {
                throw new InvalidDataException("Malformed SDS tag data.");
            }

            index++;
        }

        private static void RequireBounds(byte[] data, int index, int count)
        {
            if (count < 0 || index < 0 || index + count > data.Length)
            {
                throw new InvalidDataException("Malformed SDS tag data.");
            }
        }

        private static string DecodeUtf16(byte[] pathBytes)
        {
            if (pathBytes.Length == 0)
            {
                return string.Empty;
            }

            // QREF path is UTF-16LE and is usually zero-terminated.
            var safeLength = pathBytes.Length & ~1;
            var path = Encoding.Unicode.GetString(pathBytes, 0, safeLength);
            var nullIndex = path.IndexOf('\0');
            return nullIndex >= 0 ? path.Substring(0, nullIndex) : path;
        }

        private static byte[] ReadExact(BinaryReader br, int count)
        {
            var data = br.ReadBytes(count);
            if (data.Length != count)
            {
                throw new EndOfStreamException("Unexpected end of stream.");
            }

            return data;
        }

        private static bool Matches(byte[] value, byte[] expected)
        {
            if (value.Length != expected.Length)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsChunkTerminator(byte[] tag, uint size)
        {
            return tag[0] == 0 && tag[1] == 0 && tag[2] == 0 && tag[3] == 0 && size == 0;
        }

        private static TlgFormatKind DetectFormat(byte[] header)
        {
            if (Matches(header, Tlg5RawHeader))
            {
                return TlgFormatKind.Tlg5;
            }

            if (Matches(header, Tlg6RawHeader))
            {
                return TlgFormatKind.Tlg6;
            }

            if (Matches(header, TlgQoiRawHeader))
            {
                return TlgFormatKind.TlgQoi;
            }

            if (Matches(header, TlgRefRawHeader))
            {
                return TlgFormatKind.TlgRef;
            }

            if (Matches(header, TlgMuxIdxHeader))
            {
                return TlgFormatKind.TlgMux;
            }

            return TlgFormatKind.Unknown;
        }
    }
}

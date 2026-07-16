
using ImageMagick;
using System.Text;

namespace BadgeFed.Services;

public class ImageService
{
    public static void ModifyImageForPageShare(string filePath, uint width = 672, uint height = 352)
    {
        // the image will be centered, not modified in aspect, put in a transparent canvas of 672x352, and the rest will be filled with transparent
        // we need to detect the aspect of the image, to decide if we set the max width to 672 or height to 352, usually are square 1:1

        using (var image = new MagickImage(filePath))
        {
            var aspect = (double)image.Width / image.Height;

            if (aspect > 1)
            {
                // landscape
                image.Resize(width, 0);
            }
            else
            {
                // portrait
                image.Resize(0, height);
            }

            image.BackgroundColor = MagickColors.Transparent;
            image.Extent(width, height, Gravity.Center, MagickColors.Transparent);

            var newFilePath = Path.Combine(
                Path.GetDirectoryName(filePath),
                Path.GetFileNameWithoutExtension(filePath) + "-share.png"
            );

            image.Format = MagickFormat.Png;
            image.Write(newFilePath);
        }
    }

    // The keyword mandated by the Open Badges baking specification.
    private const string OpenBadgeKeyword = "openbadges";

    // Legacy/alternate keyword we also read for backwards compatibility.
    private const string LegacyOpenBadgeKeyword = "openbadge";

    public static string EmbedOpenBadgeMetadata(string sourceImagePath, string openBadgeJson, string? outputPath = null)
    {
        if (outputPath == null)
        {
            var directory = Path.GetDirectoryName(sourceImagePath) ?? Directory.GetCurrentDirectory();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceImagePath);
            outputPath = Path.Combine(directory, $"{fileNameWithoutExt}-with-metadata.png");
        }

        // Step 1: Normalize the source to clean PNG bytes with ImageMagick.
        // This guarantees a well-formed PNG (single IDAT stream) and strips any
        // pre-existing metadata profiles that could trigger "chunk after IDAT" warnings.
        byte[] pngBytes;
        using (var image = new MagickImage(sourceImagePath))
        {
            image.Format = MagickFormat.Png;
            image.RemoveProfile("exif");
            image.RemoveProfile("xmp");
            image.RemoveProfile("iptc");
            pngBytes = image.ToByteArray();
        }

        // Step 2: Bake the assertion as an iTXt chunk with keyword "openbadges",
        // inserted BEFORE the IDAT chunk, as required by the Open Badges baking spec.
        var baked = PngBaker.Bake(pngBytes, OpenBadgeKeyword, openBadgeJson);

        File.WriteAllBytes(outputPath, baked);

        return outputPath;
    }

    public static string? ExtractOpenBadgeMetadata(string imagePath)
    {
        var pngBytes = File.ReadAllBytes(imagePath);
        return PngBaker.ExtractText(pngBytes, OpenBadgeKeyword)
            ?? PngBaker.ExtractText(pngBytes, LegacyOpenBadgeKeyword);
    }

    public static bool HasOpenBadgeMetadata(string imagePath)
    {
        try
        {
            return !string.IsNullOrEmpty(ExtractOpenBadgeMetadata(imagePath));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Minimal PNG chunk reader/writer that "bakes" an Open Badge assertion into a PNG
    /// according to the Open Badges baking specification: an uncompressed iTXt chunk
    /// with keyword "openbadges" placed before the first IDAT chunk.
    /// </summary>
    private static class PngBaker
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly uint[] CrcTable = BuildCrcTable();

        private sealed class PngChunk
        {
            public string Type = string.Empty;
            public byte[] Data = Array.Empty<byte>();
        }

        public static byte[] Bake(byte[] png, string keyword, string text)
        {
            var chunks = ReadChunks(png);

            // Remove any existing text chunks carrying this (or the legacy) keyword to avoid duplicates.
            chunks.RemoveAll(c => IsTextChunk(c.Type)
                && (KeywordEquals(c, keyword) || KeywordEquals(c, LegacyOpenBadgeKeyword)));

            var itxt = CreateITxtChunk(keyword, text);

            // Insert before the first IDAT. Fall back to before IEND if no IDAT is found.
            int insertIndex = chunks.FindIndex(c => c.Type == "IDAT");
            if (insertIndex < 0)
            {
                insertIndex = chunks.FindIndex(c => c.Type == "IEND");
                if (insertIndex < 0) insertIndex = chunks.Count;
            }
            chunks.Insert(insertIndex, itxt);

            return WriteChunks(chunks);
        }

        public static string? ExtractText(byte[] png, string keyword)
        {
            List<PngChunk> chunks;
            try
            {
                chunks = ReadChunks(png);
            }
            catch
            {
                return null;
            }

            var chunk = chunks.FirstOrDefault(c => IsTextChunk(c.Type) && KeywordEquals(c, keyword));
            return chunk == null ? null : DecodeTextChunk(chunk);
        }

        private static bool IsTextChunk(string type) => type is "iTXt" or "tEXt" or "zTXt";

        private static List<PngChunk> ReadChunks(byte[] png)
        {
            if (png.Length < 8 || !png.Take(8).SequenceEqual(PngSignature))
                throw new InvalidDataException("Not a valid PNG file.");

            var chunks = new List<PngChunk>();
            int pos = 8;

            while (pos + 8 <= png.Length)
            {
                int length = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
                if (length < 0 || pos + 12 + length > png.Length)
                    break;

                string type = Encoding.ASCII.GetString(png, pos + 4, 4);
                var data = new byte[length];
                Array.Copy(png, pos + 8, data, 0, length);

                chunks.Add(new PngChunk { Type = type, Data = data });

                pos += 12 + length; // 4 length + 4 type + data + 4 CRC

                if (type == "IEND")
                    break;
            }

            return chunks;
        }

        private static byte[] WriteChunks(List<PngChunk> chunks)
        {
            using var ms = new MemoryStream();
            ms.Write(PngSignature, 0, PngSignature.Length);

            foreach (var chunk in chunks)
            {
                var typeBytes = Encoding.ASCII.GetBytes(chunk.Type);

                ms.Write(BigEndian(chunk.Data.Length), 0, 4);
                ms.Write(typeBytes, 0, 4);
                ms.Write(chunk.Data, 0, chunk.Data.Length);

                var crcInput = new byte[4 + chunk.Data.Length];
                Array.Copy(typeBytes, 0, crcInput, 0, 4);
                Array.Copy(chunk.Data, 0, crcInput, 4, chunk.Data.Length);
                ms.Write(BigEndian((int)Crc32(crcInput)), 0, 4);
            }

            return ms.ToArray();
        }

        private static PngChunk CreateITxtChunk(string keyword, string text)
        {
            var keywordBytes = Encoding.Latin1.GetBytes(keyword);
            var textBytes = Encoding.UTF8.GetBytes(text);

            using var ms = new MemoryStream();
            ms.Write(keywordBytes, 0, keywordBytes.Length);
            ms.WriteByte(0); // null separator after keyword
            ms.WriteByte(0); // compression flag: 0 = uncompressed
            ms.WriteByte(0); // compression method
            ms.WriteByte(0); // language tag (empty) + null separator
            ms.WriteByte(0); // translated keyword (empty) + null separator
            ms.Write(textBytes, 0, textBytes.Length);

            return new PngChunk { Type = "iTXt", Data = ms.ToArray() };
        }

        private static bool KeywordEquals(PngChunk chunk, string keyword)
        {
            int nullIdx = Array.IndexOf(chunk.Data, (byte)0);
            if (nullIdx < 0) nullIdx = chunk.Data.Length;
            var kw = Encoding.Latin1.GetString(chunk.Data, 0, nullIdx);
            return string.Equals(kw, keyword, StringComparison.Ordinal);
        }

        private static string? DecodeTextChunk(PngChunk chunk)
        {
            var data = chunk.Data;
            int nullIdx = Array.IndexOf(data, (byte)0);
            if (nullIdx < 0) return null;

            switch (chunk.Type)
            {
                case "tEXt":
                {
                    int start = nullIdx + 1;
                    return Encoding.Latin1.GetString(data, start, data.Length - start);
                }
                case "zTXt":
                {
                    // keyword \0 compressionMethod compressedText
                    int start = nullIdx + 2; // skip null + compression method byte
                    if (start > data.Length) return null;
                    return Inflate(data, start, data.Length - start, Encoding.Latin1);
                }
                case "iTXt":
                {
                    // keyword \0 compFlag compMethod langTag \0 translatedKeyword \0 text
                    int pos = nullIdx + 1;
                    if (pos + 2 > data.Length) return null;
                    byte compressionFlag = data[pos++];
                    pos++; // compression method

                    int langEnd = Array.IndexOf(data, (byte)0, pos);
                    if (langEnd < 0) return null;
                    pos = langEnd + 1;

                    int transEnd = Array.IndexOf(data, (byte)0, pos);
                    if (transEnd < 0) return null;
                    pos = transEnd + 1;

                    if (compressionFlag == 1)
                        return Inflate(data, pos, data.Length - pos, Encoding.UTF8);

                    return Encoding.UTF8.GetString(data, pos, data.Length - pos);
                }
                default:
                    return null;
            }
        }

        private static string? Inflate(byte[] data, int offset, int length, Encoding encoding)
        {
            try
            {
                // PNG uses zlib streams; skip the 2-byte zlib header for raw DeflateStream.
                using var input = new MemoryStream(data, offset + 2, length - 2);
                using var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return encoding.GetString(output.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private static byte[] BigEndian(int value) => new[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };

        private static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (var b in data)
                c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFF;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = ((c & 1) != 0) ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                table[n] = c;
            }
            return table;
        }
    }
}
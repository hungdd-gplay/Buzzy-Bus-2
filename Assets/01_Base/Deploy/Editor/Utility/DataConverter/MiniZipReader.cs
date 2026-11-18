#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BaseProject.EditorTools.DataConverter
{
    /// <summary>
    /// Đọc file ZIP tối giản (đủ cho .xlsx): chỉ dùng DeflateStream (System.dll) nên không cần thêm
    /// tham chiếu System.IO.Compression.dll như ZipArchive. Hỗ trợ entry Stored / Deflate, không hỗ trợ ZIP64 / mã hóa.
    /// </summary>
    public class MiniZipReader
    {
        private const uint SIGNATURE_END_CENTRAL = 0x06054b50;
        private const uint SIGNATURE_CENTRAL = 0x02014b50;
        private const uint SIGNATURE_LOCAL = 0x04034b50;

        private class EntryInfo
        {
            public ushort compressionMethod;
            public int sizeCompressed;
            public int offsetLocalHeader;
        }

        private readonly byte[] _arrayBytes;
        private readonly Dictionary<string, EntryInfo> _dictEntry = new Dictionary<string, EntryInfo>(StringComparer.OrdinalIgnoreCase);

        public MiniZipReader(string path)
        {
            using (var _stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                _arrayBytes = new byte[_stream.Length];
                int _read = 0;
                while (_read < _arrayBytes.Length)
                {
                    int _count = _stream.Read(_arrayBytes, _read, _arrayBytes.Length - _read);
                    if (_count <= 0) break;
                    _read += _count;
                }
            }
            _ReadCentralDirectory();
        }

        public bool HasEntry(string name)
        {
            return _dictEntry.ContainsKey(name);
        }

        /// <summary>Giải nén 1 entry, trả về null nếu không có.</summary>
        public byte[] GetEntryBytes(string name)
        {
            if (!_dictEntry.TryGetValue(name, out var _entry))
                return null;

            int _offset = _entry.offsetLocalHeader;
            if (_ReadUInt32(_offset) != SIGNATURE_LOCAL)
                throw new InvalidDataException("Local header lỗi: " + name);
            int _lengthName = _ReadUInt16(_offset + 26);
            int _lengthExtra = _ReadUInt16(_offset + 28);
            int _start = _offset + 30 + _lengthName + _lengthExtra;

            if (_entry.compressionMethod == 0)
            {
                var _result = new byte[_entry.sizeCompressed];
                Buffer.BlockCopy(_arrayBytes, _start, _result, 0, _entry.sizeCompressed);
                return _result;
            }
            if (_entry.compressionMethod != 8)
                throw new NotSupportedException($"Không hỗ trợ kiểu nén {_entry.compressionMethod} ({name})");

            using (var _input = new MemoryStream(_arrayBytes, _start, _entry.sizeCompressed))
            using (var _deflate = new DeflateStream(_input, CompressionMode.Decompress))
            using (var _output = new MemoryStream())
            {
                var _buffer = new byte[16384];
                int _count;
                while ((_count = _deflate.Read(_buffer, 0, _buffer.Length)) > 0)
                    _output.Write(_buffer, 0, _count);
                return _output.ToArray();
            }
        }

        private void _ReadCentralDirectory()
        {
            int _offsetEnd = -1;
            for (int i = _arrayBytes.Length - 22; i >= Math.Max(0, _arrayBytes.Length - 65557); i--)
            {
                if (_ReadUInt32(i) == SIGNATURE_END_CENTRAL)
                {
                    _offsetEnd = i;
                    break;
                }
            }
            if (_offsetEnd < 0)
                throw new InvalidDataException("Không phải file zip/xlsx hợp lệ.");

            int _countEntry = _ReadUInt16(_offsetEnd + 10);
            int _offset = (int)_ReadUInt32(_offsetEnd + 16);
            for (int i = 0; i < _countEntry; i++)
            {
                if (_ReadUInt32(_offset) != SIGNATURE_CENTRAL)
                    throw new InvalidDataException("Central directory lỗi.");
                var _entry = new EntryInfo
                {
                    compressionMethod = _ReadUInt16(_offset + 10),
                    sizeCompressed = (int)_ReadUInt32(_offset + 20),
                    offsetLocalHeader = (int)_ReadUInt32(_offset + 42),
                };
                int _lengthName = _ReadUInt16(_offset + 28);
                int _lengthExtra = _ReadUInt16(_offset + 30);
                int _lengthComment = _ReadUInt16(_offset + 32);
                string _name = Encoding.UTF8.GetString(_arrayBytes, _offset + 46, _lengthName).Replace('\\', '/');
                _dictEntry[_name] = _entry;
                _offset += 46 + _lengthName + _lengthExtra + _lengthComment;
            }
        }

        private ushort _ReadUInt16(int offset)
        {
            return (ushort)(_arrayBytes[offset] | (_arrayBytes[offset + 1] << 8));
        }

        private uint _ReadUInt32(int offset)
        {
            return (uint)(_arrayBytes[offset] | (_arrayBytes[offset + 1] << 8) | (_arrayBytes[offset + 2] << 16) | (_arrayBytes[offset + 3] << 24));
        }
    }
}
#endif

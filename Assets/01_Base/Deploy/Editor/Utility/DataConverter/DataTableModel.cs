#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace BaseProject.EditorTools.DataConverter
{
    /// <summary>Bảng dữ liệu đọc từ CSV/TSV/XLSX: dòng đầu là header.</summary>
    public class DataTableModel
    {
        public string nameSource;
        public List<string> lstHeader = new List<string>();
        public List<List<string>> lstRow = new List<List<string>>();

        public int CountColumn => lstHeader.Count;
        public int CountRow => lstRow.Count;

        public string GetCell(int indexRow, int indexColumn)
        {
            if (indexRow < 0 || indexRow >= lstRow.Count)
                return string.Empty;
            var _row = lstRow[indexRow];
            return indexColumn >= 0 && indexColumn < _row.Count ? _row[indexColumn] : string.Empty;
        }

        public int FindColumn(string nameField)
        {
            string _key = NormalizeName(nameField);
            for (int i = 0; i < lstHeader.Count; i++)
            {
                if (NormalizeName(lstHeader[i]) == _key)
                    return i;
            }
            return -1;
        }

        /// <summary>Chuẩn hóa tên để so khớp header với tên field: bỏ khoảng trắng, _, -, không phân biệt hoa thường.</summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var _chars = new List<char>(name.Length);
            foreach (char c in name)
            {
                if (c == ' ' || c == '_' || c == '-' || c == '.')
                    continue;
                _chars.Add(char.ToLowerInvariant(c));
            }
            return new string(_chars.ToArray());
        }

        /// <summary>Tạo bảng từ ma trận ô thô: dòng đầu tiên không rỗng là header, bỏ dòng trống và cột header rỗng phía sau.</summary>
        public static DataTableModel FromCells(string nameSource, List<List<string>> lstCells)
        {
            var _table = new DataTableModel { nameSource = nameSource };
            int _indexHeader = lstCells.FindIndex(r => r.Exists(c => !string.IsNullOrWhiteSpace(c)));
            if (_indexHeader < 0)
                return _table;

            var _header = lstCells[_indexHeader];
            int _countColumn = _header.Count;
            while (_countColumn > 0 && string.IsNullOrWhiteSpace(_header[_countColumn - 1]))
                _countColumn--;
            for (int i = 0; i < _countColumn; i++)
                _table.lstHeader.Add(_header[i].Trim());

            for (int r = _indexHeader + 1; r < lstCells.Count; r++)
            {
                var _row = lstCells[r];
                if (!_row.Exists(c => !string.IsNullOrWhiteSpace(c)))
                    continue;
                var _rowFixed = new List<string>(_countColumn);
                for (int c = 0; c < _countColumn; c++)
                    _rowFixed.Add(c < _row.Count ? _row[c] : string.Empty);
                // Dòng bắt đầu bằng "#" hoặc "//" coi là ghi chú
                string _first = _rowFixed.Count > 0 ? _rowFixed[0].TrimStart() : string.Empty;
                if (_first.StartsWith("#", StringComparison.Ordinal) || _first.StartsWith("//", StringComparison.Ordinal))
                    continue;
                _table.lstRow.Add(_rowFixed);
            }
            return _table;
        }
    }

    public enum ImportMode
    {
        /// <summary>Xóa list cũ, ghi toàn bộ dòng mới.</summary>
        Replace = 0,
        /// <summary>Thêm dòng mới vào cuối list.</summary>
        Append = 1,
        /// <summary>Cập nhật phần tử có cùng giá trị cột khóa, chưa có thì thêm mới.</summary>
        UpdateByKey = 2,
    }
}
#endif

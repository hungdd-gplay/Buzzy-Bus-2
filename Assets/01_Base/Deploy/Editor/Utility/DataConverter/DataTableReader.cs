#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine.Networking;

namespace BaseProject.EditorTools.DataConverter
{
    /// <summary>Đọc bảng dữ liệu từ CSV / TSV / XLSX (không cần thư viện ngoài) hoặc URL CSV (Google Sheet).</summary>
    public static class DataTableReader
    {
        private const string NS_SHEET_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NS_RELATIONSHIP_DOC = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string NS_RELATIONSHIP_PACKAGE = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static bool IsExcelFile(string path)
        {
            string _extension = Path.GetExtension(path).ToLowerInvariant();
            return _extension == ".xlsx" || _extension == ".xlsm";
        }

        /// <summary>Đọc file theo đuôi: .xlsx/.xlsm, .tsv, còn lại coi là CSV.</summary>
        public static DataTableModel ReadFile(string path, int indexSheet = 0)
        {
            if (IsExcelFile(path))
                return ReadXlsx(path, indexSheet);
            string _text = File.ReadAllText(path, Encoding.UTF8);
            char _delimiter = Path.GetExtension(path).ToLowerInvariant() == ".tsv" ? '\t' : DetectDelimiter(_text);
            return DataTableModel.FromCells(Path.GetFileName(path), ParseDelimited(_text, _delimiter));
        }

        /// <summary>Tải CSV từ URL (vd Google Sheet: File > Share > Publish to web > CSV, hoặc .../export?format=csv&gid=...).</summary>
        public static DataTableModel ReadUrl(string url)
        {
            using (var _request = UnityWebRequest.Get(url))
            {
                var _operation = _request.SendWebRequest();
                var _timeStart = DateTime.Now;
                while (!_operation.isDone)
                {
                    if ((DateTime.Now - _timeStart).TotalSeconds > 30)
                        throw new TimeoutException("Quá thời gian tải: " + url);
                    System.Threading.Thread.Sleep(20);
                }
                if (_request.result != UnityWebRequest.Result.Success)
                    throw new Exception("Tải lỗi: " + _request.error);
                string _text = _request.downloadHandler.text;
                return DataTableModel.FromCells(url, ParseDelimited(_text, DetectDelimiter(_text)));
            }
        }

        public static char DetectDelimiter(string text)
        {
            int _end = text.IndexOf('\n');
            string _line = _end > 0 ? text.Substring(0, _end) : text;
            int _countComma = 0, _countSemicolon = 0, _countTab = 0;
            foreach (char c in _line)
            {
                if (c == ',') _countComma++;
                else if (c == ';') _countSemicolon++;
                else if (c == '\t') _countTab++;
            }
            if (_countTab > _countComma && _countTab > _countSemicolon) return '\t';
            if (_countSemicolon > _countComma) return ';';
            return ',';
        }

        /// <summary>Parse CSV chuẩn RFC4180: hỗ trợ ngoặc kép, "" trong ô, xuống dòng trong ô.</summary>
        public static List<List<string>> ParseDelimited(string text, char delimiter)
        {
            var _lstRow = new List<List<string>>();
            var _row = new List<string>();
            var _cell = new StringBuilder();
            bool _isInQuote = false;
            if (text.Length > 0 && text[0] == '﻿')
                text = text.Substring(1);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (_isInQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { _cell.Append('"'); i++; }
                        else _isInQuote = false;
                    }
                    else _cell.Append(c);
                    continue;
                }

                if (c == '"') _isInQuote = true;
                else if (c == delimiter) { _row.Add(_cell.ToString()); _cell.Length = 0; }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    _row.Add(_cell.ToString()); _cell.Length = 0;
                    _lstRow.Add(_row); _row = new List<string>();
                }
                else _cell.Append(c);
            }
            if (_cell.Length > 0 || _row.Count > 0)
            {
                _row.Add(_cell.ToString());
                _lstRow.Add(_row);
            }
            return _lstRow;
        }

        // ================== XLSX ==================

        public static List<string> GetSheetNames(string path)
        {
            var _lstName = new List<string>();
            var _zip = new MiniZipReader(path);
            foreach (var _sheet in _ReadSheetInfos(_zip))
                _lstName.Add(_sheet.Key);
            return _lstName;
        }

        public static DataTableModel ReadXlsx(string path, int indexSheet)
        {
            var _zip = new MiniZipReader(path);
            {
                var _lstSheet = _ReadSheetInfos(_zip);
                if (_lstSheet.Count == 0)
                    throw new Exception("File xlsx không có sheet.");
                indexSheet = Math.Max(0, Math.Min(indexSheet, _lstSheet.Count - 1));
                var _sheetInfo = _lstSheet[indexSheet];

                var _lstSharedString = _ReadSharedStrings(_zip);
                var _entrySheet = _zip.GetEntryBytes(_sheetInfo.Value);
                if (_entrySheet == null)
                    throw new Exception("Không tìm thấy dữ liệu sheet: " + _sheetInfo.Value);

                var _xml = _LoadXml(_entrySheet);
                var _namespace = new XmlNamespaceManager(_xml.NameTable);
                _namespace.AddNamespace("x", NS_SHEET_MAIN);

                var _lstCells = new List<List<string>>();
                foreach (XmlNode _nodeRow in _xml.SelectNodes("//x:sheetData/x:row", _namespace))
                {
                    int _indexRow = _ParseInt(_GetAttribute(_nodeRow, "r"), _lstCells.Count + 1) - 1;
                    while (_lstCells.Count <= _indexRow)
                        _lstCells.Add(new List<string>());
                    var _row = _lstCells[_indexRow];

                    foreach (XmlNode _nodeCell in _nodeRow.SelectNodes("x:c", _namespace))
                    {
                        int _indexColumn = _ColumnIndexFromReference(_GetAttribute(_nodeCell, "r"), _row.Count);
                        while (_row.Count <= _indexColumn)
                            _row.Add(string.Empty);
                        _row[_indexColumn] = _ReadCellValue(_nodeCell, _namespace, _lstSharedString);
                    }
                }
                return DataTableModel.FromCells($"{Path.GetFileName(path)} [{_sheetInfo.Key}]", _lstCells);
            }
        }

        /// <summary>(tên sheet, đường dẫn entry trong zip) theo thứ tự trong workbook.</summary>
        private static List<KeyValuePair<string, string>> _ReadSheetInfos(MiniZipReader zip)
        {
            var _result = new List<KeyValuePair<string, string>>();
            var _entryWorkbook = zip.GetEntryBytes("xl/workbook.xml");
            if (_entryWorkbook == null)
                return _result;

            var _dictTarget = new Dictionary<string, string>();
            var _entryRels = zip.GetEntryBytes("xl/_rels/workbook.xml.rels");
            if (_entryRels != null)
            {
                var _xmlRels = _LoadXml(_entryRels);
                foreach (XmlNode _node in _xmlRels.GetElementsByTagName("Relationship", NS_RELATIONSHIP_PACKAGE))
                {
                    string _target = _GetAttribute(_node, "Target");
                    if (_target.StartsWith("/")) _target = _target.Substring(1);
                    else if (!_target.StartsWith("xl/")) _target = "xl/" + _target;
                    _dictTarget[_GetAttribute(_node, "Id")] = _target;
                }
            }

            var _xmlWorkbook = _LoadXml(_entryWorkbook);
            int _index = 1;
            foreach (XmlNode _node in _xmlWorkbook.GetElementsByTagName("sheet", NS_SHEET_MAIN))
            {
                string _name = _GetAttribute(_node, "name");
                var _attributeId = _node.Attributes?.GetNamedItem("id", NS_RELATIONSHIP_DOC);
                string _path = _attributeId != null && _dictTarget.TryGetValue(_attributeId.Value, out var _target)
                    ? _target
                    : $"xl/worksheets/sheet{_index}.xml";
                _result.Add(new KeyValuePair<string, string>(_name, _path));
                _index++;
            }
            return _result;
        }

        private static List<string> _ReadSharedStrings(MiniZipReader zip)
        {
            var _lst = new List<string>();
            var _entry = zip.GetEntryBytes("xl/sharedStrings.xml");
            if (_entry == null)
                return _lst;
            var _xml = _LoadXml(_entry);
            foreach (XmlNode _nodeItem in _xml.GetElementsByTagName("si", NS_SHEET_MAIN))
            {
                var _builder = new StringBuilder();
                foreach (XmlNode _nodeText in ((XmlElement)_nodeItem).GetElementsByTagName("t", NS_SHEET_MAIN))
                {
                    // bỏ phần phiên âm (rPh)
                    if (_nodeText.ParentNode != null && _nodeText.ParentNode.LocalName == "rPh")
                        continue;
                    _builder.Append(_nodeText.InnerText);
                }
                _lst.Add(_builder.ToString());
            }
            return _lst;
        }

        private static string _ReadCellValue(XmlNode nodeCell, XmlNamespaceManager namespaceManager, List<string> lstSharedString)
        {
            string _type = _GetAttribute(nodeCell, "t");
            if (_type == "inlineStr")
            {
                var _nodeInline = nodeCell.SelectSingleNode("x:is", namespaceManager);
                return _nodeInline != null ? _nodeInline.InnerText : string.Empty;
            }

            var _nodeValue = nodeCell.SelectSingleNode("x:v", namespaceManager);
            string _value = _nodeValue != null ? _nodeValue.InnerText : string.Empty;
            switch (_type)
            {
                case "s":
                    int _index = _ParseInt(_value, -1);
                    return _index >= 0 && _index < lstSharedString.Count ? lstSharedString[_index] : string.Empty;
                case "b":
                    return _value == "1" ? "true" : "false";
                default:
                    return _value;
            }
        }

        private static int _ColumnIndexFromReference(string reference, int fallback)
        {
            if (string.IsNullOrEmpty(reference))
                return fallback;
            int _column = 0;
            foreach (char c in reference)
            {
                if (c >= 'A' && c <= 'Z') _column = _column * 26 + (c - 'A' + 1);
                else if (c >= 'a' && c <= 'z') _column = _column * 26 + (c - 'a' + 1);
                else break;
            }
            return _column > 0 ? _column - 1 : fallback;
        }

        private static XmlDocument _LoadXml(byte[] arrayBytes)
        {
            var _xml = new XmlDocument();
            using (var _stream = new MemoryStream(arrayBytes))
                _xml.Load(_stream);
            return _xml;
        }

        private static string _GetAttribute(XmlNode node, string name)
        {
            var _attribute = node.Attributes?[name];
            return _attribute != null ? _attribute.Value : string.Empty;
        }

        private static int _ParseInt(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text))
                return fallback;
            int _value = 0;
            bool _isHasDigit = false;
            foreach (char c in text)
            {
                if (c < '0' || c > '9') break;
                _value = _value * 10 + (c - '0');
                _isHasDigit = true;
            }
            return _isHasDigit ? _value : fallback;
        }
    }
}
#endif

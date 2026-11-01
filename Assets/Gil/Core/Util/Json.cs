using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gil.Core.Util
{
    /// <summary>
    /// 엔진에 의존하지 않는 최소 JSON 파서/작성기.
    /// 데이터 테이블 로딩과 세이브를 Unity 밖(단위 테스트, CI)에서도 그대로 검증하기 위해 직접 구현했다.
    /// 객체는 Dictionary&lt;string, object&gt;, 배열은 List&lt;object&gt;, 숫자는 double로 표현한다.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var parser = new Parser(text);
            var value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Error("JSON 뒤에 불필요한 문자가 있습니다");
            return value;
        }

        public static string Serialize(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object value, bool pretty, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case IDictionary dict:
                {
                    sb.Append('{');
                    var first = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, pretty, depth + 1);
                        WriteString(sb, entry.Key.ToString());
                        sb.Append(pretty ? ": " : ":");
                        Write(sb, entry.Value, pretty, depth + 1);
                    }
                    if (!first) NewLine(sb, pretty, depth);
                    sb.Append('}');
                    break;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, pretty, depth + 1);
                        Write(sb, item, pretty, depth + 1);
                    }
                    if (!first) NewLine(sb, pretty, depth);
                    sb.Append(']');
                    break;
                }
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IFormattable number:
                    sb.Append(number.ToString(null, CultureInfo.InvariantCulture));
                    break;
                default:
                    WriteString(sb, value.ToString());
                    break;
            }
        }

        static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string _text;
            int _pos;

            public Parser(string text) => _text = text ?? string.Empty;

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message)
            {
                var line = 1;
                for (var i = 0; i < _pos && i < _text.Length; i++)
                    if (_text[i] == '\n') line++;
                return new FormatException($"{message} ({line}번째 줄)");
            }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    var c = _text[_pos];
                    if (char.IsWhiteSpace(c) || c == '﻿')
                    {
                        _pos++;
                    }
                    else if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
                    {
                        // 기획자가 메모를 남길 수 있도록 // 주석을 허용한다.
                        while (!AtEnd && _text[_pos] != '\n') _pos++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("값이 필요합니다");
                var c = _text[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return ParseNumber();
                        throw Error($"예상하지 못한 문자 '{c}'");
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>();
                _pos++; // {
                SkipWhitespace();
                if (Peek() == '}') { _pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("키는 문자열이어야 합니다");
                    var key = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw Error("':'가 필요합니다");
                    _pos++;
                    result[key] = ParseValue();
                    SkipWhitespace();
                    var next = Peek();
                    _pos++;
                    if (next == '}') return result;
                    if (next != ',') throw Error("',' 또는 '}'가 필요합니다");
                    SkipWhitespace();
                    if (Peek() == '}') { _pos++; return result; } // 끝의 쉼표 허용
                }
            }

            List<object> ParseArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (Peek() == ']') { _pos++; return result; }
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    var next = Peek();
                    _pos++;
                    if (next == ']') return result;
                    if (next != ',') throw Error("',' 또는 ']'가 필요합니다");
                    SkipWhitespace();
                    if (Peek() == ']') { _pos++; return result; }
                }
            }

            string ParseString()
            {
                var sb = new StringBuilder();
                _pos++; // "
                while (true)
                {
                    if (AtEnd) throw Error("문자열이 닫히지 않았습니다");
                    var c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Error("이스케이프가 끝나지 않았습니다");
                    var e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw Error("\\u 이스케이프가 짧습니다");
                            sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _pos += 4;
                            break;
                        default: throw Error($"알 수 없는 이스케이프 '\\{e}'");
                    }
                }
            }

            double ParseNumber()
            {
                var start = _pos;
                if (Peek() == '-') _pos++;
                while (!AtEnd && "0123456789.eE+-".IndexOf(_text[_pos]) >= 0) _pos++;
                var token = _text.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw Error($"숫자 형식이 잘못되었습니다: '{token}'");
                return value;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                    throw Error($"'{word}'가 필요합니다");
                _pos += word.Length;
            }

            char Peek()
            {
                if (AtEnd) throw Error("JSON이 중간에 끝났습니다");
                return _text[_pos];
            }
        }
    }

    /// <summary>파싱된 JSON 객체에서 타입 있는 값을 꺼내는 헬퍼. 누락·형식 오류를 경로와 함께 보고한다.</summary>
    public readonly struct JsonObject
    {
        readonly Dictionary<string, object> _values;
        public readonly string Path;

        public JsonObject(object value, string path)
        {
            _values = value as Dictionary<string, object>
                      ?? throw new FormatException($"{path}: 객체가 필요합니다");
            Path = path;
        }

        public bool Has(string key) => _values.ContainsKey(key) && _values[key] != null;

        public string String(string key, string fallback = null)
        {
            if (!_values.TryGetValue(key, out var v) || v == null)
            {
                if (fallback != null) return fallback;
                throw new FormatException($"{Path}.{key}: 필수 값이 없습니다");
            }
            return v as string ?? throw new FormatException($"{Path}.{key}: 문자열이 필요합니다");
        }

        public float Float(string key, float? fallback = null)
        {
            if (!_values.TryGetValue(key, out var v) || v == null)
            {
                if (fallback.HasValue) return fallback.Value;
                throw new FormatException($"{Path}.{key}: 필수 값이 없습니다");
            }
            return v is double d ? (float)d : throw new FormatException($"{Path}.{key}: 숫자가 필요합니다");
        }

        public int Int(string key, int? fallback = null)
        {
            if (!Has(key) && fallback.HasValue) return fallback.Value;
            var f = Float(key);
            var i = (int)Math.Round(f);
            if (Math.Abs(f - i) > 0.0001f) throw new FormatException($"{Path}.{key}: 정수가 필요합니다");
            return i;
        }

        public bool Bool(string key, bool fallback)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return fallback;
            return v is bool b ? b : throw new FormatException($"{Path}.{key}: true/false가 필요합니다");
        }

        public JsonObject Object(string key) => new JsonObject(
            _values.TryGetValue(key, out var v) ? v : throw new FormatException($"{Path}.{key}: 필수 값이 없습니다"),
            $"{Path}.{key}");

        public IEnumerable<JsonObject> Objects(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) yield break;
            if (!(v is List<object> list)) throw new FormatException($"{Path}.{key}: 배열이 필요합니다");
            for (var i = 0; i < list.Count; i++) yield return new JsonObject(list[i], $"{Path}.{key}[{i}]");
        }

        public IEnumerable<int> Ints(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) yield break;
            if (!(v is List<object> list)) throw new FormatException($"{Path}.{key}: 배열이 필요합니다");
            for (var i = 0; i < list.Count; i++)
                yield return list[i] is double d ? (int)Math.Round(d) : throw new FormatException($"{Path}.{key}[{i}]: 숫자가 필요합니다");
        }

        /// <summary>문자열 → 정수 맵 (예: 아이템 id → 개수).</summary>
        public IEnumerable<KeyValuePair<string, int>> IntMap(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) yield break;
            if (!(v is Dictionary<string, object> map)) throw new FormatException($"{Path}.{key}: 객체가 필요합니다");
            foreach (var pair in map)
                yield return new KeyValuePair<string, int>(pair.Key,
                    pair.Value is double d ? (int)Math.Round(d) : throw new FormatException($"{Path}.{key}.{pair.Key}: 숫자가 필요합니다"));
        }

        /// <summary>문자열 → 실수 맵 (예: 능력치 이름 → 증감량).</summary>
        public IEnumerable<KeyValuePair<string, float>> FloatMap(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) yield break;
            if (!(v is Dictionary<string, object> map)) throw new FormatException($"{Path}.{key}: 객체가 필요합니다");
            foreach (var pair in map)
                yield return new KeyValuePair<string, float>(pair.Key,
                    pair.Value is double d ? (float)d : throw new FormatException($"{Path}.{key}.{pair.Key}: 숫자가 필요합니다"));
        }

        /// <summary>이 객체의 키 목록.</summary>
        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<string> Strings(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) yield break;
            if (!(v is List<object> list)) throw new FormatException($"{Path}.{key}: 배열이 필요합니다");
            for (var i = 0; i < list.Count; i++)
                yield return list[i] as string ?? throw new FormatException($"{Path}.{key}[{i}]: 문자열이 필요합니다");
        }
    }
}

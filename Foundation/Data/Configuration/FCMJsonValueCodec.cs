using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CxVisual.Foundation.Data.Configuration
{
    internal sealed class FCMJsonProperty
    {
        public string Name;
        public int Start;
        public int End;
        public FCMJsonNode Value;
    }
    internal sealed class FCMJsonNode
    {
        public char Kind;
        public int Start;
        public int End;
        public List<FCMJsonProperty> Properties = new List<FCMJsonProperty>();
        public List<FCMJsonNode> Items = new List<FCMJsonNode>();
        public FCMJsonProperty Property(string name) { return Properties.FirstOrDefault(x => x.Name == name); }
    }

    internal sealed class FCMJsonReader
    {
        private readonly string _s;
        private int _i;
        private int _depth;
        private FCMJsonReader(string json) { _s = json; }
        public static FCMJsonNode Parse(string json)
        {
            FCMJsonReader p = new FCMJsonReader(json);
            p.Skip();
            FCMJsonNode node = p.ReadNode();
            p.Skip();
            if (p._i != json.Length) throw new FormatException("Trailing JSON content at " + p._i);
            return node;
        }
        private void Skip() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
        private char Current { get { return _i < _s.Length ? _s[_i] : '\0'; } }
        private void Expect(char c) { if (Current != c) throw new FormatException("Expected '" + c + "' at " + _i); _i++; }
        private FCMJsonNode ReadNode()
        {
            if (++_depth > 128) throw new FormatException("JSON too deeply nested");
            try
            {
                Skip();
                FCMJsonNode node = new FCMJsonNode { Kind = Current, Start = _i };
                if (Current == '{')
                {
                    _i++; Skip();
                    while (Current != '}')
                    {
                        if (Current != '"') throw new FormatException("Expected JSON key at " + _i);
                        int start = _i;
                        string key = ReadString();
                        if (node.Property(key) != null) throw new FormatException("Duplicate JSON key: " + key);
                        Skip(); Expect(':');
                        FCMJsonNode value = ReadNode();
                        node.Properties.Add(new FCMJsonProperty { Name = key, Start = start, End = value.End, Value = value });
                        Skip();
                        if (Current == '}') break;
                        Expect(','); Skip();
                        if (Current == '}') throw new FormatException("Trailing comma");
                    }
                    Expect('}');
                }
                else if (Current == '[')
                {
                    _i++; Skip();
                    while (Current != ']')
                    {
                        node.Items.Add(ReadNode()); Skip();
                        if (Current == ']') break;
                        Expect(','); Skip();
                        if (Current == ']') throw new FormatException("Trailing comma");
                    }
                    Expect(']');
                }
                else if (Current == '"') ReadString();
                else if (Current == 't') Literal("true");
                else if (Current == 'f') Literal("false");
                else if (Current == 'n') Literal("null");
                else if (Current == '-' || (Current >= '0' && Current <= '9')) Number();
                else throw new FormatException("Invalid JSON token at " + _i);
                node.End = _i;
                return node;
            }
            finally { _depth--; }
        }
        private void Literal(string literal)
        {
            if (_i + literal.Length > _s.Length || string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
                throw new FormatException("Invalid JSON literal");
            _i += literal.Length;
        }
        private void Number()
        {
            if (Current == '-') _i++;
            if (Current == '0') _i++;
            else { if (Current < '1' || Current > '9') throw new FormatException("Invalid number"); while (Current >= '0' && Current <= '9') _i++; }
            if (Current == '.') { _i++; int start = _i; while (Current >= '0' && Current <= '9') _i++; if (_i == start) throw new FormatException("Invalid fractional number"); }
            if (Current == 'e' || Current == 'E')
            { _i++; if (Current == '+' || Current == '-') _i++; int start = _i; while (Current >= '0' && Current <= '9') _i++; if (_i == start) throw new FormatException("Invalid exponent"); }
        }
        private string ReadString()
        {
            Expect('"'); StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (_i >= _s.Length) throw new FormatException("Unclosed JSON string");
                char c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c < 32) throw new FormatException("Control character in JSON string");
                if (c != '\\') { sb.Append(c); continue; }
                if (_i >= _s.Length) throw new FormatException("Unclosed JSON escape");
                char e = _s[_i++];
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
                        if (_i + 4 > _s.Length) throw new FormatException("Invalid unicode escape");
                        int code;
                        if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw new FormatException("Invalid unicode escape");
                        sb.Append((char)code); _i += 4; break;
                    default: throw new FormatException("Invalid escape");
                }
            }
        }
    }

    public static class FCMJsonValueCodec
    {
        public static string Serialize(object value) { return SerializeValue(value, 0); }
        public static T Deserialize<T>(string json) { return (T)Deserialize(json, typeof(T)); }
        public static object Deserialize(string json, Type type)
        {
            if (json == null) throw new ArgumentNullException("json");
            return ConvertNode(json, FCMJsonReader.Parse(json), type, 0);
        }
        internal static object ConvertNode(string json, FCMJsonNode node, Type type, int depth)
        {
            if (depth > 64) throw new FormatException("Config nesting exceeds 64 levels");
            Type nullable = Nullable.GetUnderlyingType(type);
            if (node.Kind == 'n')
            {
                if (type.IsValueType && nullable == null) throw new FormatException("null for non-nullable " + type.FullName);
                return null;
            }
            Type t = nullable ?? type;
            string raw = json.Substring(node.Start, node.End - node.Start);
            FCMConfigConverterAttribute converter = (FCMConfigConverterAttribute)Attribute.GetCustomAttribute(t, typeof(FCMConfigConverterAttribute));
            if (converter != null)
            {
                Type contract = typeof(IFCMConfigValueConverter<>).MakeGenericType(t);
                if (!contract.IsAssignableFrom(converter.ConverterType)) throw new InvalidOperationException("Invalid converter for " + t.FullName);
                return contract.GetMethod("Read").Invoke(Activator.CreateInstance(converter.ConverterType), new object[] { raw });
            }
            if (t == typeof(string) || t == typeof(char) || t == typeof(Guid) || t == typeof(DateTimeOffset) ||
                t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Uri) || t == typeof(Version) || t.IsEnum)
            {
                if (node.Kind != '"') throw new FormatException("Expected JSON string for " + t.Name);
                string val = Unquote(raw);
                if (t == typeof(string)) return val;
                if (t == typeof(char)) { if (val.Length != 1) throw new FormatException("char must have length 1"); return val[0]; }
                if (t == typeof(Guid)) return Guid.Parse(val);
                if (t == typeof(DateTimeOffset)) return DateTimeOffset.ParseExact(val, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
                if (t == typeof(DateTime)) return DateTime.ParseExact(val, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (t == typeof(TimeSpan)) return TimeSpan.Parse(val, CultureInfo.InvariantCulture);
                if (t == typeof(Uri)) return new Uri(val, UriKind.RelativeOrAbsolute);
                if (t == typeof(Version)) return Version.Parse(val);
                if (t.IsEnum) return Enum.Parse(t, val, false);
            }
            if (t == typeof(bool))
            {
                if (raw != "true" && raw != "false") throw new FormatException("Invalid bool");
                return raw == "true";
            }
            if (t.IsPrimitive || t == typeof(decimal))
            {
                if (node.Kind == '"' || node.Kind == '{' || node.Kind == '[') throw new FormatException("Expected numeric value");
                if (t == typeof(float)) { float f = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture); if (float.IsNaN(f) || float.IsInfinity(f)) throw new FormatException("Nonfinite float"); return f; }
                if (t == typeof(double)) { double d = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture); if (double.IsNaN(d) || double.IsInfinity(d)) throw new FormatException("Nonfinite double"); return d; }
                return Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
            }
            if (node.Kind == '[')
            {
                Type elementType;
                if (t.IsArray)
                {
                    if (t.GetArrayRank() != 1) throw new NotSupportedException("Multidimensional arrays need an explicit codec");
                    elementType = t.GetElementType();
                    Array array = Array.CreateInstance(elementType, node.Items.Count);
                    for (int i = 0; i < node.Items.Count; i++) array.SetValue(ConvertNode(json, node.Items[i], elementType, depth + 1), i);
                    return array;
                }
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    elementType = t.GetGenericArguments()[0];
                    object set = Activator.CreateInstance(t);
                    MethodInfo add = t.GetMethod("Add");
                    foreach (FCMJsonNode item in node.Items) add.Invoke(set, new object[] { ConvertNode(json, item, elementType, depth + 1) });
                    return set;
                }
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = t.GetGenericArguments()[0];
                    IList list = (IList)Activator.CreateInstance(t);
                    foreach (FCMJsonNode item in node.Items) list.Add(ConvertNode(json, item, elementType, depth + 1));
                    return list;
                }
                throw new NotSupportedException("Unsupported collection type: " + t.FullName);
            }
            if (node.Kind != '{') throw new FormatException("Expected JSON object for " + t.Name);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type[] args = t.GetGenericArguments();
                if (args[0] != typeof(string)) throw new NotSupportedException("Only string dictionary keys are supported");
                IDictionary dict = (IDictionary)Activator.CreateInstance(t);
                foreach (FCMJsonProperty prop in node.Properties) dict.Add(prop.Name, ConvertNode(json, prop.Value, args[1], depth + 1));
                return dict;
            }
            if (t.IsInterface || t.IsAbstract || !t.IsClass && !t.IsValueType)
                throw new NotSupportedException("Polymorphic config requires explicit converter: " + t.FullName);
            object instance = Activator.CreateInstance(t);
            foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(t))
            {
                FCMJsonProperty prop = node.Property(member.Name);
                if (prop == null && !string.IsNullOrEmpty(member.AliasFrom)) prop = node.Property(member.AliasFrom);
                if (prop != null) member.Property.SetValue(instance, ConvertNode(json, prop.Value, member.MemberType, depth + 1), null);
            }
            return instance;
        }
        private static void RequireSerializableComparer(object value, Type type)
        {
            if (!type.IsGenericType) return;
            Type generic = type.GetGenericTypeDefinition();
            if (generic != typeof(Dictionary<,>) && generic != typeof(HashSet<>)) return;
            Type keyType = type.GetGenericArguments()[0];
            object actual = type.GetProperty("Comparer").GetValue(value, null);
            object standard = typeof(EqualityComparer<>).MakeGenericType(keyType)
                .GetProperty("Default").GetValue(null, null);
            if (!object.Equals(actual, standard) &&
                !(keyType == typeof(string) && object.Equals(actual, StringComparer.Ordinal)))
                throw new NotSupportedException("CONFIG_COLLECTION_COMPARER_NOT_PERSISTED");
        }
        private static string SerializeValue(object value, int depth)
        {
            if (depth > 64) throw new NotSupportedException("Config nesting exceeds 64 levels");
            if (value == null) return "null";
            Type t = value.GetType();
            // JSON arrays/maps carry values, not CLR comparer identities.
            // Fail closed on unsupported custom equality semantics rather than
            // silently changing key uniqueness or set membership on restart.
            RequireSerializableComparer(value, t);
            FCMConfigConverterAttribute converter = (FCMConfigConverterAttribute)Attribute.GetCustomAttribute(t, typeof(FCMConfigConverterAttribute));
            if (converter != null)
            {
                Type contract = typeof(IFCMConfigValueConverter<>).MakeGenericType(t);
                if (!contract.IsAssignableFrom(converter.ConverterType)) throw new InvalidOperationException("Invalid converter for " + t.FullName);
                string custom = (string)contract.GetMethod("Write").Invoke(Activator.CreateInstance(converter.ConverterType), new object[] { value });
                FCMJsonReader.Parse(custom); // Require a valid single JSON token, never unchecked raw insertion.
                return custom;
            }
            if (t == typeof(string) || t == typeof(char) || t == typeof(Guid) || t == typeof(Uri) || t == typeof(Version) || t.IsEnum)
                return Quote(Convert.ToString(value, CultureInfo.InvariantCulture));
            if (t == typeof(DateTime)) return Quote(((DateTime)value).ToString("o", CultureInfo.InvariantCulture));
            if (t == typeof(DateTimeOffset)) return Quote(((DateTimeOffset)value).ToString("o", CultureInfo.InvariantCulture));
            if (t == typeof(TimeSpan)) return Quote(((TimeSpan)value).ToString("c", CultureInfo.InvariantCulture));
            if (t == typeof(bool)) return (bool)value ? "true" : "false";
            if (t == typeof(double)) { double d = (double)value; if (double.IsInfinity(d) || double.IsNaN(d)) throw new FormatException("Nonfinite double"); return d.ToString("R", CultureInfo.InvariantCulture); }
            if (t == typeof(float)) { float f = (float)value; if (float.IsInfinity(f) || float.IsNaN(f)) throw new FormatException("Nonfinite float"); return f.ToString("R", CultureInfo.InvariantCulture); }
            if (t.IsPrimitive || t == typeof(decimal)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                List<string> parts = new List<string>();
                foreach (DictionaryEntry entry in dict)
                {
                    if (!(entry.Key is string)) throw new NotSupportedException("Only string dictionary keys are supported");
                    parts.Add(Quote((string)entry.Key) + ":" + SerializeValue(entry.Value, depth + 1));
                }
                return "{" + string.Join(",", parts) + "}";
            }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                List<string> parts = new List<string>();
                foreach (object element in sequence) parts.Add(SerializeValue(element, depth + 1));
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>)) parts.Sort(StringComparer.Ordinal);
                return "[" + string.Join(",", parts) + "]";
            }
            if (t.IsInterface || t.IsAbstract) throw new NotSupportedException("Unsupported config type: " + t.FullName);
            return "{" + string.Join(",", FCMConfigSchemaBuilder.Members(t)
                .Select(x => Quote(x.Name) + ":" + SerializeValue(x.Property.GetValue(value, null), depth + 1))) + "}";
        }
        internal static string Quote(string s)
        {
            if (s == null) return "null";
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    case '\b': b.Append("\\b"); break;
                    case '\f': b.Append("\\f"); break;
                    default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4")); else b.Append(c); break;
                }
            }
            return b.Append('"').ToString();
        }
        private static string Unquote(string raw)
        {
            // Parse the isolated string with the same grammar as the main document.
            FCMJsonNode n = FCMJsonReader.Parse(raw);
            if (n.Kind != '"') throw new FormatException("Expected string");
            StringBuilder b = new StringBuilder();
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c != '\\') { b.Append(c); continue; }
                c = raw[++i];
                switch (c)
                {
                    case '"': b.Append('"'); break; case '\\': b.Append('\\'); break; case '/': b.Append('/'); break;
                    case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                    case 'u': b.Append((char)int.Parse(raw.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                }
            }
            return b.ToString();
        }
    }
}
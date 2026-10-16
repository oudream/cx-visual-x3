using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CxVisual.Foundation.Data.Configuration
{
    /// <summary>Lossless edit of unchanged JSON property text; only changed/new values are patched.</summary>
    public static class FCMOrderedJsonDocument
    {
        public static string Patch<T>(string existingJson, T original, T updated)
        {
            if (existingJson == null) throw new ArgumentNullException("existingJson");
            if (updated == null) throw new ArgumentNullException("updated");
            FCMJsonNode root = FCMJsonReader.Parse(existingJson);
            if (root.Kind != '{') throw new FormatException("A configuration file must have a JSON object root");
            return PatchNode(existingJson, root, original, updated, typeof(T), 0);
        }
        private static string PatchNode(string json, FCMJsonNode node, object oldValue, object newValue, Type type, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("Excessive config nesting");
            if (newValue == null || oldValue == null || node.Kind != '{' ||
                FCMConfigSchemaBuilder.IsScalar(type) || typeof(IEnumerable).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type))
                return Replace(json, node.Start, node.End, FCMJsonValueCodec.Serialize(newValue));

            IDictionary newDict = newValue as IDictionary;
            IDictionary oldDict = oldValue as IDictionary;
            if (newDict != null && oldDict != null)
            {
                // No old entries exist to preserve. Serializing the whole new map is
                // equivalent to inserting each new key in enumeration order, without
                // reparsing an ever-growing JSON object O(N^2) times.
                if (oldDict.Count == 0 && node.Properties.Count == 0)
                    return FCMJsonValueCodec.Serialize(newValue);
                // For a dictionary, its keys are dynamic and ARE part of the user's editable model.
                // Remove explicitly deleted keys; retain untouched keys and preserve their textual spans.
                foreach (DictionaryEntry oldEntry in oldDict)
                {
                    if (newDict.Contains(oldEntry.Key)) continue;
                    FCMJsonNode parsed = FCMJsonReader.Parse(json);
                    FCMJsonProperty candidate = parsed.Property((string)oldEntry.Key);
                    if (candidate != null) json = Remove(json, parsed, candidate.Name);
                }
                foreach (DictionaryEntry entry in newDict)
                {
                    string key = entry.Key as string;
                    if (key == null) throw new NotSupportedException("Dictionary keys must be strings");
                    // Most keys in a large persistent map are unchanged. Never reparse
                    // the entire document for these keys (also preserves their raw text).
                    bool wasPresent = oldDict.Contains(key);
                    object previous = wasPresent ? oldDict[key] : null;
                    string encoded = FCMJsonValueCodec.Serialize(entry.Value);
                    if (wasPresent && string.Equals(
                        FCMJsonValueCodec.Serialize(previous), encoded, StringComparison.Ordinal))
                        continue;
                    FCMJsonNode parsed = FCMJsonReader.Parse(json);
                    FCMJsonProperty prop = parsed.Property(key);
                    if (prop == null)
                        json = Insert(json, parsed, key, encoded, null, null, 0, null);
                    else
                        json = PatchChild(json, prop.Value, previous, entry.Value,
                            entry.Value == null ? typeof(object) : entry.Value.GetType(), depth + 1);
                }
                return json;
            }
            IList<FCMConfigMemberDescriptor> members = FCMConfigSchemaBuilder.Members(type);
            foreach (FCMConfigMemberDescriptor member in members)
            {
                object oldPart = member.Property.GetValue(oldValue, null);
                object newPart = member.Property.GetValue(newValue, null);
                FCMJsonNode parsed = FCMJsonReader.Parse(json);
                FCMJsonProperty prop = parsed.Property(member.Name);
                if (prop == null && !string.IsNullOrEmpty(member.AliasFrom))
                {
                    FCMJsonProperty oldNamed = parsed.Property(member.AliasFrom);
                    if (oldNamed != null)
                    {
                        // Explicit schema rename: keep the property's physical position and value unchanged.
                        json = Replace(json, oldNamed.Start,
                            oldNamed.Start + FCMJsonValueCodec.Quote(member.AliasFrom).Length,
                            FCMJsonValueCodec.Quote(member.Name));
                        parsed = FCMJsonReader.Parse(json);
                        prop = parsed.Property(member.Name);
                    }
                }
                if (prop == null)
                {
                    json = Insert(json, parsed, member.Name, FCMJsonValueCodec.Serialize(newPart),
                        member.AfterId, member.BeforeId, member.Order, members);
                    continue;
                }
                if (FCMJsonValueCodec.Serialize(oldPart) == FCMJsonValueCodec.Serialize(newPart)) continue;
                json = PatchChild(json, prop.Value, oldPart, newPart, member.MemberType, depth + 1);
            }
            return json;
        }
        private static string PatchChild(string document, FCMJsonNode child, object oldPart, object newPart, Type partType, int depth)
        {
            if (oldPart != null && newPart != null && child.Kind == '{' &&
                (newPart is IDictionary || !FCMConfigSchemaBuilder.IsScalar(partType)))
            {
                string snippet = document.Substring(child.Start, child.End - child.Start);
                string changed = PatchNode(snippet, FCMJsonReader.Parse(snippet), oldPart, newPart, partType, depth);
                return Replace(document, child.Start, child.End, changed);
            }
            return Replace(document, child.Start, child.End, FCMJsonValueCodec.Serialize(newPart));
        }
        private static string Replace(string value, int start, int end, string replacement)
        { return value.Substring(0, start) + replacement + value.Substring(end); }
        private static string Remove(string json, FCMJsonNode node, string key)
        {
            int index = node.Properties.FindIndex(x => x.Name == key);
            if (index < 0) return json;
            FCMJsonProperty item = node.Properties[index];
            if (node.Properties.Count == 1) return Replace(json, item.Start, item.End, "");
            if (index + 1 < node.Properties.Count)
                return Replace(json, item.Start, node.Properties[index + 1].Start, "");
            return Replace(json, node.Properties[index - 1].End, item.End, "");
        }
        private static string Insert(string json, FCMJsonNode node, string key, string encoded,
            string afterId, string beforeId, int order, IList<FCMConfigMemberDescriptor> members)
        {
            if (node.Kind != '{') throw new InvalidOperationException("Expected JSON object");
            FCMJsonProperty anchorBefore = null;
            FCMJsonProperty anchorAfter = null;
            if (members != null)
            {
                if (!string.IsNullOrEmpty(beforeId))
                {
                    FCMConfigMemberDescriptor before = members.First(x => x.MemberId == beforeId);
                    anchorBefore = node.Property(before.Name);
                }
                if (!string.IsNullOrEmpty(afterId))
                {
                    FCMConfigMemberDescriptor after = members.First(x => x.MemberId == afterId);
                    anchorAfter = node.Property(after.Name);
                }
                if (anchorAfter != null && anchorBefore != null && anchorAfter.Start >= anchorBefore.Start)
                    throw new InvalidOperationException("Existing file violates BeforeId/AfterId interval for " + key);
                if (anchorBefore == null && anchorAfter != null)
                {
                    int afterIndex = node.Properties.IndexOf(anchorAfter);
                    if (afterIndex + 1 < node.Properties.Count) anchorBefore = node.Properties[afterIndex + 1];
                }
                if (anchorBefore == null && anchorAfter == null && order > 0)
                {
                    foreach (FCMJsonProperty p in node.Properties)
                    {
                        FCMConfigMemberDescriptor known = members.FirstOrDefault(x => x.Name == p.Name);
                        if (known != null && known.Order > order && known.Order > 0) { anchorBefore = p; break; }
                    }
                }
            }
            bool multiline = json.IndexOf('\n', node.Start, node.End - node.Start) >= 0;
            string newline = json.Contains("\r\n") ? "\r\n" : "\n";
            string indent = "  ";
            if (node.Properties.Count > 0 && multiline)
            {
                int start = json.LastIndexOf('\n', node.Properties[0].Start);
                indent = json.Substring(start + 1, node.Properties[0].Start - start - 1);
                if (indent.Trim().Length > 0) indent = "  ";
            }
            string kv = FCMJsonValueCodec.Quote(key) + ": " + encoded;
            if (node.Properties.Count == 0)
            {
                string insertion = multiline ? newline + indent + kv + newline : kv;
                return Replace(json, node.Start + 1, node.Start + 1, insertion);
            }
            if (anchorBefore != null)
            {
                string prefix = kv + (multiline ? "," + newline + indent : ", ");
                return Replace(json, anchorBefore.Start, anchorBefore.Start, prefix);
            }
            FCMJsonProperty last = node.Properties[node.Properties.Count - 1];
            string tail = multiline ? "," + newline + indent + kv : ", " + kv;
            return Replace(json, last.End, last.End, tail);
        }
    }
}
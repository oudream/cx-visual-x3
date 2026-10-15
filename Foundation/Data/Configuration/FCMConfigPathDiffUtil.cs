using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CxVisual.Foundation.Data.Configuration
{
    public static class FCMConfigPathUtil
    {
        public static string FromExpression<T, TValue>(Expression<Func<T, TValue>> accessor)
        {
            if (accessor == null) throw new ArgumentNullException("accessor");
            return Build(accessor.Body);
        }
        private static string Build(Expression e)
        {
            UnaryExpression unary = e as UnaryExpression;
            if (unary != null && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
                return Build(unary.Operand);
            ParameterExpression p = e as ParameterExpression;
            if (p != null) return string.Empty;
            MemberExpression m = e as MemberExpression;
            if (m != null)
            {
                string parent = Build(m.Expression);
                return parent.Length == 0 ? m.Member.Name : parent + "." + m.Member.Name;
            }
            IndexExpression index = e as IndexExpression;
            if (index != null)
                return Build(index.Object) + Suffix(index.Arguments);
            MethodCallExpression call = e as MethodCallExpression;
            if (call != null && call.Method.Name == "get_Item")
                return Build(call.Object) + Suffix(call.Arguments);
            throw new NotSupportedException("Only member and constant-key indexer expressions are supported for config paths.");
        }
        private static string Suffix(IEnumerable<Expression> arguments)
        {
            List<string> keys = new List<string>();
            foreach (Expression arg in arguments)
            {
                // The indexer key is evaluated once when registering a subscription.
                object value = Expression.Lambda<Func<object>>(Expression.Convert(arg, typeof(object))).Compile()();
                if (value is string) keys.Add(FCMJsonValueCodec.Quote((string)value));
                else if (value is int) keys.Add(((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
                else throw new NotSupportedException("Only string and integer config indexes are supported.");
            }
            return "[" + string.Join(",", keys) + "]";
        }
    }

    public static class FCMConfigDiffUtil
    {
        public static IList<string> Diff(object before, object after)
        {
            List<string> paths = new List<string>();
            Compare(before, after, string.Empty, paths, 0);
            return paths.AsReadOnly();
        }
        private static void Compare(object a, object b, string path, IList<string> output, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("Config nesting exceeds limit");
            if (FCMJsonValueCodec.Serialize(a) == FCMJsonValueCodec.Serialize(b)) return;
            if (a == null || b == null) { output.Add(path); return; }
            Type type = b.GetType();
            if (FCMConfigSchemaBuilder.IsScalar(type)) { output.Add(path); return; }
            IDictionary left = a as IDictionary, right = b as IDictionary;
            if (left != null && right != null)
            {
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (DictionaryEntry it in left) { if (!(it.Key is string)) throw new NotSupportedException("String keys required"); keys.Add((string)it.Key); }
                foreach (DictionaryEntry it in right) { if (!(it.Key is string)) throw new NotSupportedException("String keys required"); keys.Add((string)it.Key); }
                foreach (string key in keys.OrderBy(x => x, StringComparer.Ordinal))
                {
                    string name = path + "[" + FCMJsonValueCodec.Quote(key) + "]";
                    if (!left.Contains(key) || !right.Contains(key)) output.Add(name);
                    else Compare(left[key], right[key], name, output, depth + 1);
                }
                return;
            }
            IList aList = a as IList, bList = b as IList;
            if (aList != null && bList != null)
            {
                for (int i = 0; i < Math.Max(aList.Count, bList.Count); i++)
                {
                    string name = path + "[" + i + "]";
                    if (i >= aList.Count || i >= bList.Count) output.Add(name);
                    else Compare(aList[i], bList[i], name, output, depth + 1);
                }
                return;
            }
            if (a is IEnumerable || b is IEnumerable) { output.Add(path); return; }
            foreach (FCMConfigMemberDescriptor m in FCMConfigSchemaBuilder.Members(type))
            {
                string child = string.IsNullOrEmpty(path) ? m.Name : path + "." + m.Name;
                Compare(m.Property.GetValue(a, null), m.Property.GetValue(b, null), child, output, depth + 1);
            }
        }
    }

    [Flags]
    public enum FCMConfigCodecCapabilities
    {
        None = 0, PreserveKnownOrder = 1, PreserveUnknownMembers = 2,
        PreserveTrivia = 4, ByteStableUnchangedNodes = 8, CanPatchNested = 16
    }
    public interface IFCMConfigDocumentCodec
    {
        string FormatId { get; }
        FCMConfigCodecCapabilities Capabilities { get; }
        string Patch<T>(string source, T previous, T candidate);
    }
    public sealed class FCMOrderedJsonCodec : IFCMConfigDocumentCodec
    {
        public string FormatId { get { return "json"; } }
        public FCMConfigCodecCapabilities Capabilities
        {
            get { return FCMConfigCodecCapabilities.PreserveKnownOrder |
                FCMConfigCodecCapabilities.PreserveUnknownMembers |
                FCMConfigCodecCapabilities.ByteStableUnchangedNodes |
                FCMConfigCodecCapabilities.CanPatchNested; }
        }
        public string Patch<T>(string source, T previous, T candidate)
        { return FCMOrderedJsonDocument.Patch(source, previous, candidate); }
    }
}
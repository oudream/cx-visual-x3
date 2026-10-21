using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CommonInterfaces
{
    public static class YamlHelper
    {
        public static SerializerBuilder CreateSerializer()
        {
            return new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance) // 使用驼峰命名
                .WithDefaultScalarStyle(ScalarStyle.DoubleQuoted) // 使用双引号
                .WithIndentedSequences() // 确保列表项前有正确的缩进
                ;
        }

        public static DeserializerBuilder CreateDeserializer()
        {
            return new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance) // 使用驼峰命名
                .IgnoreUnmatchedProperties() // 忽略不匹配属性
                ;
        }
    }

}

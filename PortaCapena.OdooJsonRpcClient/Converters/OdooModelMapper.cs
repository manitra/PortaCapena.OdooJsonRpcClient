﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PortaCapena.OdooJsonRpcClient.Models;

namespace PortaCapena.OdooJsonRpcClient.Converters
{
    public static class OdooModelMapper
    {
        private const string OdooModelSuffix = "OdooModel";
        private const string OdooEnumSuffix = "OdooEnum";

        public static bool ConverOdooPropertyToDotNet(Type dotnetType, JToken value, out object result)
        {
            result = null;

            switch (value.Type)
            {
                case JTokenType.Boolean when dotnetType != typeof(bool):
                    return false;

                case JTokenType.Boolean:
                    result = value.ToObject(dotnetType);
                    return true;

                case JTokenType.Integer when dotnetType == typeof(bool) || dotnetType == typeof(bool?):
                    result = value.ToObject(dotnetType);
                    return true;

                case JTokenType.Integer when dotnetType == typeof(int) || dotnetType == typeof(int?) || dotnetType == typeof(long) || dotnetType == typeof(long?):
                case JTokenType.Float:
                    result = value.ToObject(dotnetType);
                    return true;

                case JTokenType.Integer when dotnetType.IsArray && (int)value.ToObject(typeof(int)) == 0:
                    result = Activator.CreateInstance(dotnetType, 0);
                    return true;
                case JTokenType.String when dotnetType == typeof(string):
                    result = value.ToObject(dotnetType);
                    return true;

                case JTokenType.String when dotnetType == typeof(DateTime) || dotnetType == typeof(DateTime?):
                    var stringTime = value.ToObject(typeof(string)) as string;
                    result = DateTime.Parse(stringTime);
                    return true;

                case JTokenType.String when dotnetType.IsEnum:
                    result = ConvertToDotNetEnum(dotnetType, value.ToString());
                    return true;

                case JTokenType.String when dotnetType.IsGenericType && dotnetType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                                            dotnetType.GenericTypeArguments.Length == 1 && dotnetType.GenericTypeArguments[0].IsEnum:
                    var nullableType = Nullable.GetUnderlyingType(dotnetType);
                    result = ConvertToDotNetEnum(nullableType, value);
                    return true;

                case JTokenType.Array when dotnetType.IsArray:
                    if (!value.HasValues)
                    {
                        result = Activator.CreateInstance(dotnetType, 0);
                        return true;
                    }

                    result = value.ToObject(dotnetType);
                    return true;

                case JTokenType.Array when !dotnetType.IsArray:
                    if (!value.HasValues)
                        return false;

                    if (value.Count() > 2 || dotnetType != typeof(long) && dotnetType != typeof(long?) && dotnetType != typeof(int) && dotnetType != typeof(int?) || value.First.Type != JTokenType.Integer)
                        throw new Exception($"Not implemented json mapping '${value.Parent}'");

                    result = value.First.ToObject(dotnetType);
                    return true;

                default:
                    throw new Exception($"Not implemented json mapping value: '${value.Parent}' to {dotnetType.Name}");
            }
        }


        public static string GetDotNetModel(string tableName, Dictionary<string, OdooPropertyInfo> properties)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[OdooTableName(\"{tableName}\")]");
            builder.AppendLine($"[JsonConverter(typeof({nameof(OdooModelConverter)}))]");
            builder.AppendLine($"public class {ConvertOdooNameToDotNet(tableName)}{OdooModelSuffix} : IOdooModel");
            builder.AppendLine("{");

            foreach (var property in properties)
            {
                builder.AppendLine(string.Empty);
                if (!string.IsNullOrEmpty(property.Value.Relation))
                    builder.AppendLine($"// {property.Value.Relation}");
                if (property.Value.ResultRequired)
                    builder.AppendLine("// required");

                builder.AppendLine($"[JsonProperty(\"{property.Key}\")]");
                builder.AppendLine($"public {ConvertToDotNetPropertyTypeName(property)} {ConvertOdooNameToDotNet(property.Key)} {{ get; set; }}");
            }
            builder.AppendLine("}");

            var selectionsProps = properties.Where(x => x.Value.PropertyValueType == OdooValueTypeEnum.Selection).ToList();

            foreach (var property in selectionsProps)
            {
                builder.AppendLine(string.Empty);
                builder.AppendLine(string.Empty);

                if (!string.IsNullOrEmpty(property.Value.Help))
                    builder.AppendLine("// " + property.Value.Help.Replace("\n", "\n // "));

                builder.AppendLine($"[JsonConverter(typeof(StringEnumConverter))]");
                builder.AppendLine($"public enum {ConvertOdooNameToDotNet(property.Value.String)}{OdooEnumSuffix}");
                builder.AppendLine("{");

                for (int i = 0; i < property.Value.Selection.Length; i++)
                {
                    string[] item = property.Value.Selection[i];

                    if (i != 0)
                        builder.AppendLine(string.Empty);
                    builder.AppendLine($"[EnumMember(Value = \"{item[0]}\")]");
                    builder.AppendLine($"{ConvertOdooNameToDotNet(item[1])} = {i + 1},");
                }
                builder.AppendLine("}");
            }
            return builder.ToString();
        }

        public static string ConvertToDotNetPropertyTypeName(KeyValuePair<string, OdooPropertyInfo> property)
        {
            switch (property.Value.PropertyValueType)
            {
                case OdooValueTypeEnum.Binary:
                    return "string";
                case OdooValueTypeEnum.Char:
                    return "string";
                case OdooValueTypeEnum.Selection:
                    return $"{ConvertOdooNameToDotNet(property.Value.String)}{OdooEnumSuffix}{(property.Value.ResultRequired ? "" : "?")}";
                case OdooValueTypeEnum.Text:
                    return "string";
                case OdooValueTypeEnum.Html:
                    return "string";

                case OdooValueTypeEnum.Boolean:
                    return property.Value.ResultRequired ? "bool" : "bool?";

                case OdooValueTypeEnum.Monetary:
                    return property.Value.ResultRequired ? "decimal" : "decimal?";

                case OdooValueTypeEnum.Float:
                    return property.Value.ResultRequired ? "double" : "double?";
                case OdooValueTypeEnum.Integer:
                    if (property.Key.ToString().ToLower() == "id")
                        return "long";
                    return property.Value.ResultRequired ? "int" : "int?";

                case OdooValueTypeEnum.Date:
                    return property.Value.ResultRequired ? "DateTime" : "DateTime?";
                case OdooValueTypeEnum.Datetime:
                    return property.Value.ResultRequired ? "DateTime" : "DateTime?";

                case OdooValueTypeEnum.Many2One:
                    return property.Value.ResultRequired ? "long" : "long?";
                case OdooValueTypeEnum.Many2Many:
                    return "long[]";
                case OdooValueTypeEnum.One2Many:
                    return "long[]";
                case OdooValueTypeEnum.One2One:
                    return property.Value.ResultRequired ? "long" : "long?";

                case OdooValueTypeEnum.Reference:
                    return ConvertOdooNameToDotNet(property.Value.RelationField) + OdooModelSuffix;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string ConvertOdooNameToDotNet(string odooName)
        {
            var dotnetKeys = odooName.Split('.', '_', ' ', '-', ':', ',').Select(x => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x));
            return string.Join(string.Empty, dotnetKeys);
        }

        public static object ConvertToDotNetEnum(Type type, JToken value)
        {
            var field = type.GetFields()
              .Where(x => x.IsLiteral && GetOdooEnumName(x) == value.ToString())
              .FirstOrDefault();

            if (field != null)
                return Enum.Parse(type, field.Name);

            throw new ArgumentException($"Value: '{value}' not found in enum : '{type.FullName}'");
        }

        public static string GetOdooEnumName(FieldInfo fieldInfo)
        {
            var enumAttribute = Attribute.GetCustomAttributes(fieldInfo)
                .FirstOrDefault(x => x is EnumMemberAttribute) as EnumMemberAttribute;
            if (enumAttribute != null)
                return enumAttribute.Value;

            throw new ArgumentException($"Missing atrribute: '{nameof(EnumMemberAttribute)}' for enum '{fieldInfo.FieldType.Name}' - '{fieldInfo.Name}'");
        }
    }
}
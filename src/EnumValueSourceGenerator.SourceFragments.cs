using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace Soenneker.Gen.EnumValues;

public sealed partial class EnumValueSourceGenerator
{
    private static string BuildReadRawValueCode(ITypeSymbol valueType)
    {
        string typeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
                return "        if (!reader.TryGetInt32(out int rawValue)) throw new global::System.Text.Json.JsonException(\"Expected int value.\");";
            case SpecialType.System_Int64:
                return "        if (!reader.TryGetInt64(out long rawValue)) throw new global::System.Text.Json.JsonException(\"Expected long value.\");";
            case SpecialType.System_Int16:
                return "        if (!reader.TryGetInt16(out short rawValue)) throw new global::System.Text.Json.JsonException(\"Expected short value.\");";
            case SpecialType.System_Byte:
                return "        if (!reader.TryGetByte(out byte rawValue)) throw new global::System.Text.Json.JsonException(\"Expected byte value.\");";
            case SpecialType.System_SByte:
                return "        if (!reader.TryGetSByte(out sbyte rawValue)) throw new global::System.Text.Json.JsonException(\"Expected sbyte value.\");";
            case SpecialType.System_UInt16:
                return "        if (!reader.TryGetUInt16(out ushort rawValue)) throw new global::System.Text.Json.JsonException(\"Expected ushort value.\");";
            case SpecialType.System_UInt32:
                return "        if (!reader.TryGetUInt32(out uint rawValue)) throw new global::System.Text.Json.JsonException(\"Expected uint value.\");";
            case SpecialType.System_UInt64:
                return "        if (!reader.TryGetUInt64(out ulong rawValue)) throw new global::System.Text.Json.JsonException(\"Expected ulong value.\");";
            case SpecialType.System_String:
                return "        string? rawValue = reader.GetString(); if (rawValue is null) throw new global::System.Text.Json.JsonException(\"Expected string value.\");";
            case SpecialType.System_Char:
                return "        string? charText = reader.GetString(); if (string.IsNullOrEmpty(charText) || charText.Length != 1) throw new global::System.Text.Json.JsonException(\"Expected char value.\"); char rawValue = charText[0];";
            case SpecialType.System_Boolean:
                return "        if (reader.TokenType != global::System.Text.Json.JsonTokenType.True && reader.TokenType != global::System.Text.Json.JsonTokenType.False) throw new global::System.Text.Json.JsonException(\"Expected bool value.\"); bool rawValue = reader.GetBoolean();";
            default:
            {
                if (valueType.ToDisplayString() == "System.Guid")
                    return "        if (reader.TokenType != global::System.Text.Json.JsonTokenType.String) throw new global::System.Text.Json.JsonException(\"Expected Guid string value.\"); global::System.Guid rawValue = reader.GetGuid();";

                return "        " + typeName + " rawValue = global::System.Text.Json.JsonSerializer.Deserialize<" + typeName +
                       ">(ref reader, options)!;";
            }
        }
    }

    private static void AppendTypeConverterClass(StringBuilder source, INamedTypeSymbol enumType, INamedTypeSymbol valueType, string enumTypeName, string valueTypeName, string typeConverterName, bool isStringValue)
    {
        source.AppendLine("/// <summary>");
        source.Append("/// <see cref=\"global::System.ComponentModel.TypeConverter\"/> for ").Append(enumTypeName).AppendLine(" (configuration and binding).");
        source.AppendLine("/// </summary>");
        source.Append("file sealed class ").Append(typeConverterName).AppendLine(" : global::System.ComponentModel.TypeConverter");
        source.AppendLine("{");
        source.AppendLine("    public override bool CanConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Type sourceType)");
        source.AppendLine("        => sourceType == typeof(string);");
        source.AppendLine();
        source.AppendLine("    public override object? ConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object value)");
        source.AppendLine("    {");
        source.AppendLine("        if (value is not string s)");
        source.AppendLine("            throw new global::System.ArgumentException(\"Value must be a string.\", nameof(value));");
        source.AppendLine();
        if (isStringValue)
        {
            source.Append("        if (").Append(enumTypeName).AppendLine(".TryFromValue(s, out var result))");
        }
        else
        {
            source.Append(BuildTypeConverterConvertFromBody(valueType, enumTypeName));
        }
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        throw new global::System.ArgumentException(\"Unknown enum value.\", nameof(value));");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public override bool CanConvertTo(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Type? destinationType)");
        source.AppendLine("        => destinationType == typeof(string);");
        source.AppendLine();
        source.AppendLine("    public override object? ConvertTo(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object? value, global::System.Type destinationType)");
        source.AppendLine("    {");
        source.AppendLine("        if (destinationType == typeof(string) && value is ").Append(enumTypeName).AppendLine(" e)");
        source.AppendLine("            return e.ToString();");
        source.AppendLine();
        source.AppendLine("        return base.ConvertTo(context, culture, value, destinationType);");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static string BuildTypeConverterConvertFromBody(ITypeSymbol valueType, string enumTypeName)
    {
        switch (valueType.SpecialType)
        {
            case SpecialType.System_Int32:
                return "        if (global::System.Int32.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out int v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_Int64:
                return "        if (global::System.Int64.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out long v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_Int16:
                return "        if (global::System.Int16.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out short v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_Byte:
                return "        if (global::System.Byte.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out byte v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_SByte:
                return "        if (global::System.SByte.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out sbyte v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_UInt16:
                return "        if (global::System.UInt16.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out ushort v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_UInt32:
                return "        if (global::System.UInt32.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out uint v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_UInt64:
                return "        if (global::System.UInt64.TryParse(s, global::System.Globalization.NumberStyles.Integer, culture ?? global::System.Globalization.CultureInfo.InvariantCulture, out ulong v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_Boolean:
                return "        if (global::System.Boolean.TryParse(s, out bool v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
            case SpecialType.System_Char:
                return "        if (s.Length == 1 && " + enumTypeName + ".TryFromValue(s[0], out var result))\r\n";
            default:
                if (valueType.ToDisplayString() == "System.Guid")
                    return "        if (global::System.Guid.TryParse(s, out var v) && " + enumTypeName + ".TryFromValue(v, out var result))\r\n";
                return "        if (" + enumTypeName + ".TryFromValue(default!, out var result))\r\n"; // fallback that will not match
        }
    }

    private static void AppendStringSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);

        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach ((string text, string targetName) in items)
        {
            source.Append(innerIndent).Append("case \"").Append(EscapeString(text)).Append("\": result = ").Append(targetName).AppendLine("; return true;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(innerIndent).AppendLine("    result = default!;");
        source.Append(innerIndent).AppendLine("    return false;");
        source.Append(indent).AppendLine("}");
    }

    private static void AppendStringConstantSwitchBody(StringBuilder source, List<(string ConstantName, string TargetName)> items, string paramName, int indentLevel)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);

        source.Append(indent).Append("switch (").Append(paramName).AppendLine(")");
        source.Append(indent).AppendLine("{");
        foreach ((string constantName, string targetName) in items)
        {
            source.Append(innerIndent).Append("case ").Append(constantName).Append(": result = ").Append(targetName).AppendLine("; return true;");
        }
        source.Append(innerIndent).AppendLine("default:");
        source.Append(innerIndent).AppendLine("    result = default!;");
        source.Append(innerIndent).AppendLine("    return false;");
        source.Append(indent).AppendLine("}");
    }

    private static void AppendSpanSwitchBody(StringBuilder source, List<(string Text, string TargetName)> items, string inputIdentifier, int indentLevel)
    {
        string indent = new(' ', indentLevel * 4);
        string innerIndent = new(' ', (indentLevel + 1) * 4);

        source.Append(indent).Append("switch (").Append(inputIdentifier).AppendLine(")");
        source.Append(indent).AppendLine("{");

        foreach ((string text, string targetName) in items)
        {
            source.Append(innerIndent).Append("case \"").Append(EscapeString(text)).Append("\": result = ").Append(targetName).AppendLine("; return true;");
        }

        source.Append(innerIndent).AppendLine("default:");
        source.Append(innerIndent).AppendLine("    result = default!;");
        source.Append(innerIndent).AppendLine("    return false;");
        source.Append(indent).AppendLine("}");
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string EscapeChar(char value)
    {
        return value switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            _ => value.ToString()
        };
    }
}

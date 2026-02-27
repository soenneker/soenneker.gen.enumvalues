using Microsoft.CodeAnalysis;
using System.Text;
using Soenneker.Gen.EnumValues.Dtos;

namespace Soenneker.Gen.EnumValues;

public sealed partial class EnumValueSourceGenerator
{

    private static void AppendTypeDeclaration(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.Append("[global::System.Diagnostics.DebuggerDisplay(\"{Name} ({Value})\")]");
        source.AppendLine();
        source.Append("[global::System.Text.Json.Serialization.JsonConverter(typeof(").Append(ctx.StjConverterTypeName).AppendLine("))]");
        if (ctx.SupportsNewtonsoft)
            source.Append("[global::Newtonsoft.Json.JsonConverter(typeof(").Append(ctx.NewtonsoftConverterTypeName).AppendLine("))]");
        source.Append("[global::System.ComponentModel.TypeConverter(typeof(").Append(ctx.TypeConverterName).AppendLine("))]");
        source.Append(ctx.Kind == "class" ? "public sealed partial class " : "public partial struct ").Append(ctx.EnumType.Name);
        if (ctx.IsStringValue)
            source.Append(" : global::System.IEquatable<").Append(ctx.EnumTypeName).Append(">, global::System.IEquatable<string?>");
        else
            source.Append(" : global::System.IEquatable<").Append(ctx.EnumTypeName).Append(">, global::System.IEquatable<").Append(ctx.ValueTypeName).Append(">");
        source.AppendLine();
        source.AppendLine("{");
    }

    private static void AppendValueNameConstructorsAndName(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        bool useId = ctx.UseIdBacking;

        if (!ctx.HasValueProperty)
        {
            AppendXmlSummary(source, "    ", "Gets the underlying value of this instance.");
            source.Append("    public ").Append(ctx.ValueTypeName).AppendLine(" Value { get; }");
            source.AppendLine();
        }

        if (useId)
        {
            source.AppendLine("    private readonly byte _id;");
            source.AppendLine();
        }

        if (!ctx.HasValueIdConstructor)
        {
            if (useId)
            {
                source.Append("    private ").Append(ctx.EnumType.Name).Append("(").Append(ctx.ValueTypeName).Append(" value, byte id)");
                source.AppendLine();
                source.AppendLine("    {");
                source.AppendLine("        Value = value;");
                source.AppendLine("        _id = id;");
                source.AppendLine("    }");
                source.AppendLine();
            }
            else
            {
                source.Append("    private ").Append(ctx.EnumType.Name).Append("(").Append(ctx.ValueTypeName).Append(" value) => Value = value;");
                source.AppendLine();
            }
            source.AppendLine();
        }

        if (!ctx.HasNameProperty && useId)
        {
            AppendXmlSummary(source, "    ", "Gets the name of this instance.");
            source.AppendLine("    public string Name => _id switch");
            source.AppendLine("    {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                string nameConst = ctx.Instances[i].Name + "Name";
                source.Append("        ").Append(instanceId).Append(" => ").Append(nameConst).AppendLine(",");
            }
            source.AppendLine("        _ => \"\"");
            source.AppendLine("    };");
            source.AppendLine();
        }
        else if (!ctx.HasNameProperty && !useId)
        {
            AppendXmlSummary(source, "    ", "Gets the name of this instance.");
            source.AppendLine("    public string Name");
            source.Append("        => ");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                if (i > 0)
                    source.Append("     : ");
                string condition = ctx.EnumType.IsReferenceType
                    ? "global::System.Object.ReferenceEquals(this, " + ctx.Instances[i].Name + ")"
                    : "global::System.Collections.Generic.EqualityComparer<" + ctx.EnumTypeName + ">.Default.Equals(this, " + ctx.Instances[i].Name + ")";
                source.Append(condition).Append(" ? \"").Append(EscapeString(ctx.Instances[i].Name)).Append("\"");
                if (i < ctx.Instances.Count - 1)
                    source.AppendLine();
            }
            source.AppendLine();
            source.AppendLine("     : \"\";");
            source.AppendLine();
        }
    }

    private static void AppendConstantsAllAndList(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        bool useId = ctx.UseIdBacking;

        var emittedValueConstant = false;
        bool canEmitConstant = CanEmitConstant(ctx.ValueType);

        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            string valueFieldName = ctx.Instances[i].Name + "Value";
            if (!canEmitConstant || ctx.EnumType.GetMembers(valueFieldName).Length > 0)
                continue;

            emittedValueConstant = true;
            source.Append("    public const ").Append(ctx.ValueTypeName).Append(" ").Append(valueFieldName).Append(" = ").Append(ctx.Instances[i].ValueLiteral).AppendLine(";");
        }

        if (emittedValueConstant)
            source.AppendLine();

        var emittedNameConstant = false;
        if (ctx.Instances.Count > 0)
        {
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string nameFieldName = ctx.Instances[i].Name + "Name";
                if (ctx.EnumType.GetMembers(nameFieldName).Length > 0)
                    continue;
                emittedNameConstant = true;
                source.Append("    public const string ").Append(nameFieldName).Append(" = \"").Append(EscapeString(ctx.Instances[i].Name)).AppendLine("\";");
            }
            if (emittedNameConstant)
                source.AppendLine();
        }

        var emittedInstance = false;
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            if (ctx.EnumType.GetMembers(ctx.Instances[i].Name).Length > 0)
                continue;
            emittedInstance = true;
            if (useId)
            {
                byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                source.Append("    public static readonly ").Append(ctx.EnumTypeName).Append(" ").Append(ctx.Instances[i].Name).Append(" = new(").Append(ctx.Instances[i].ValueLiteral).Append(", ").Append(instanceId).AppendLine(");");
            }
            else
                source.Append("    public static readonly ").Append(ctx.EnumTypeName).Append(" ").Append(ctx.Instances[i].Name).Append(" = new(").Append(ctx.Instances[i].ValueLiteral).AppendLine(");");
        }
        if (emittedInstance)
            source.AppendLine();

        source.Append("    private static readonly ").Append(ctx.EnumTypeName).Append("[] __all = new[] { ");
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            if (i > 0) source.Append(", ");
            source.Append(ctx.Instances[i].Name);
        }
        source.AppendLine(" };");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Gets a span of all defined instances.");
        source.Append("    public static global::System.ReadOnlySpan<").Append(ctx.EnumTypeName).AppendLine("> All => __all;");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Gets a read-only list of all defined instances.");
        source.Append("    public static global::System.Collections.Generic.IReadOnlyList<").Append(ctx.EnumTypeName).AppendLine("> List => __all;");
        source.AppendLine();
    }

    private static void AppendParsingMethods(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        if (ctx.IsStringValue)
        {
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified span.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool TryFromValue(global::System.ReadOnlySpan<char> value, out ").Append(ctx.EnumTypeName).AppendLine(" result)");
            source.AppendLine("    {");
            AppendSpanFirstCharSwitchBody(source, ctx.ValueSpanItems, "value", 2);
            source.AppendLine("    }");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified string.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool TryFromValue(string? value, out ").Append(ctx.EnumTypeName).AppendLine(" result)");
            source.AppendLine("    {");
            source.AppendLine("        if (value is null) { result = default!; return false; }");
            source.AppendLine();
            AppendStringConstantSwitchBody(source, ctx.ValueItems, "value", 2);
            source.AppendLine();
            source.AppendLine("    }");
            source.AppendLine();
        }
        else
        {
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified value.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static bool TryFromValue(").Append(ctx.ValueTryFromSignature).Append(", out ").Append(ctx.EnumTypeName).AppendLine(" result)");
            source.AppendLine("    {");
            source.AppendLine("        switch (value)");
            source.AppendLine("        {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                source.Append("            case ").Append(ctx.Instances[i].ValueLiteral).Append(": result = ").Append(ctx.Instances[i].Name).AppendLine("; return true;");
            }
            source.AppendLine("            default:");
            source.AppendLine("                result = default!;");
            source.AppendLine("                return false;");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine();
        }

        AppendXmlSummary(source, "    ", "Gets the instance for the specified value, or throws if not defined.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ").Append(ctx.EnumTypeName).Append(" FromValue(").Append(ctx.ValueTypeName).AppendLine(" value)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromValue(value, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        return ThrowHelper.UnknownValue(value);");
        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Tries to parse an instance from the specified name span.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool TryFromName(global::System.ReadOnlySpan<char> name, out ").Append(ctx.EnumTypeName).AppendLine(" result)");
        source.AppendLine("    {");
        AppendSpanFirstCharSwitchBody(source, ctx.NameSpanItems, "name", 2);
        source.AppendLine("    }");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Tries to parse an instance from the specified name.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static bool TryFromName(string? name, out ").Append(ctx.EnumTypeName).AppendLine(" result)");
        source.AppendLine("    {");
        source.AppendLine("        if (name is null) { result = default!; return false; }");
        source.AppendLine();
        AppendStringConstantSwitchBody(source, ctx.NameItems, "name", 2);
        source.AppendLine();
        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Gets the instance for the specified name, or throws if not defined.");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ").Append(ctx.EnumTypeName).AppendLine(" FromName(string name)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromName(name, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        return ThrowHelper.UnknownName(name);");
        source.AppendLine("    }");
        source.AppendLine();
        AppendIsDefinedIsNameDefined(source, ctx.EnumTypeName, ctx.ValueTypeName, ctx.IsStringValue);
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Deconstructs this instance into its name and value.");
        source.Append("    public void Deconstruct(out string name, out ").Append(ctx.ValueTypeName).AppendLine(" value)");
        source.AppendLine("    {");
        source.AppendLine("        name = Name;");
        source.AppendLine("        value = Value;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendEqualityAndOperators(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        if (ctx.IsStringValue)
        {
            bool useId = ctx.UseIdBacking;
            AppendXmlSummary(source, "    ", "Implicitly converts this instance to its string value.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static implicit operator string(").Append(ctx.EnumTypeName).AppendLine(" value)");
            source.AppendLine("        => value.Value;");
            source.AppendLine();
            if (useId && ctx.EnumType.IsReferenceType)
            {
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ").Append(ctx.EnumTypeName).AppendLine(" other && _id == other._id;");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(").Append(ctx.EnumTypeName).AppendLine("? other)");
                source.AppendLine("        => other is not null && _id == other._id;");
                source.AppendLine();
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => _id;");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other) => _id switch");
                source.AppendLine("    {");
                for (var i = 0; i < ctx.Instances.Count; i++)
                {
                    byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                    string valueConst = ctx.Instances[i].Name + "Value";
                    source.Append("        ").Append(instanceId).Append(" => global::System.String.Equals(other, ").Append(valueConst).AppendLine(", global::System.StringComparison.Ordinal),");
                }
                source.AppendLine("        _ => false");
                source.AppendLine("    };");
                source.AppendLine();
                source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append("? left, ").Append(ctx.EnumTypeName).AppendLine("? right)");
                source.AppendLine("        => global::System.Object.ReferenceEquals(left, right) || (left is not null && right is not null && left._id == right._id);");
                source.AppendLine();
                source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append("? left, ").Append(ctx.EnumTypeName).AppendLine("? right)");
                source.AppendLine("        => !(left == right);");
            }
            else if (useId && !ctx.EnumType.IsReferenceType)
            {
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ").Append(ctx.EnumTypeName).AppendLine(" other && _id == other._id;");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(").Append(ctx.EnumTypeName).AppendLine(" other)");
                source.AppendLine("        => _id == other._id;");
                source.AppendLine();
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => _id;");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other) => _id switch");
                source.AppendLine("    {");
                for (var i = 0; i < ctx.Instances.Count; i++)
                {
                    byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                    string valueConst = ctx.Instances[i].Name + "Value";
                    source.Append("        ").Append(instanceId).Append(" => global::System.String.Equals(other, ").Append(valueConst).AppendLine(", global::System.StringComparison.Ordinal),");
                }
                source.AppendLine("        _ => false");
                source.AppendLine("    };");
                source.AppendLine();
                source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
                source.AppendLine("        => left._id == right._id;");
                source.AppendLine();
                source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
                source.AppendLine("        => left._id != right._id;");
            }
            else if (ctx.EnumType.IsReferenceType)
            {
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ").Append(ctx.EnumTypeName).AppendLine(" other && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => global::System.StringComparer.Ordinal.GetHashCode(Value);");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(").Append(ctx.EnumTypeName).AppendLine("? other)");
                source.AppendLine("        => other is not null && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other)");
                source.AppendLine("        => global::System.String.Equals(Value, other, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append("? left, ").Append(ctx.EnumTypeName).AppendLine("? right)");
                source.AppendLine("        => left is null ? right is null : right is not null && left.Equals(right);");
                source.AppendLine();
                source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append("? left, ").Append(ctx.EnumTypeName).AppendLine("? right)");
                source.AppendLine("        => !(left == right);");
            }
            else
            {
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ").Append(ctx.EnumTypeName).AppendLine(" other && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => global::System.StringComparer.Ordinal.GetHashCode(Value);");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(").Append(ctx.EnumTypeName).AppendLine(" other)");
                source.AppendLine("        => global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other)");
                source.AppendLine("        => global::System.String.Equals(Value, other, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
                source.AppendLine("        => left.Equals(right);");
                source.AppendLine();
                source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
                source.AppendLine("        => !left.Equals(right);");
            }
            source.AppendLine();
            source.AppendLine("    public override string ToString() => Name;");
            source.AppendLine();
        }
        else
        {
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public bool Equals(").Append(ctx.EnumTypeName).Append(ctx.EnumType.IsReferenceType ? "? " : " ");
            source.Append("other)");
            if (ctx.EnumType.IsReferenceType)
                source.AppendLine(" => other is not null && Value.Equals(other.Value);");
            else
                source.AppendLine(" => Value.Equals(other.Value);");
            source.AppendLine();
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public bool Equals(").Append(ctx.ValueTypeName).AppendLine(" other)");
            source.AppendLine("        => Value.Equals(other);");
            source.AppendLine();
            source.AppendLine("    public override bool Equals(object? obj)");
            source.Append("        => obj is ").Append(ctx.EnumTypeName).AppendLine(" other && Equals(other);");
            source.AppendLine();
            source.AppendLine("    public override int GetHashCode()");
            source.AppendLine("        => Value.GetHashCode();");
            source.AppendLine();
            source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
            source.AppendLine("        => left.Equals(right);");
            source.AppendLine();
            source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
            source.AppendLine("        => !left.Equals(right);");
            source.AppendLine();
            source.Append("    public static bool operator ==(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.ValueTypeName).AppendLine(" right)");
            source.AppendLine("        => left.Value.Equals(right);");
            source.AppendLine();
            source.Append("    public static bool operator !=(").Append(ctx.EnumTypeName).Append(" left, ").Append(ctx.ValueTypeName).AppendLine(" right)");
            source.AppendLine("        => !left.Value.Equals(right);");
            source.AppendLine();
            source.Append("    public static bool operator ==(").Append(ctx.ValueTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
            source.AppendLine("        => right.Value.Equals(left);");
            source.AppendLine();
            source.Append("    public static bool operator !=(").Append(ctx.ValueTypeName).Append(" left, ").Append(ctx.EnumTypeName).AppendLine(" right)");
            source.AppendLine("        => !right.Value.Equals(left);");
            source.AppendLine();
            source.AppendLine("    public override string ToString()");
            source.Append("        => ").Append(BuildToStringExpression(ctx.ValueType)).AppendLine(";");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Explicitly converts this instance to its underlying value.");
            source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static explicit operator ").Append(ctx.ValueTypeName).Append("(").Append(ctx.EnumTypeName).AppendLine(" value)");
            source.AppendLine("        => value.Value;");
            source.AppendLine();
        }
    }

    private static void AppendThrowHelperAndConverters(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Throws for unknown values or names.");
        source.AppendLine("/// </summary>");
        source.AppendLine("file static class ThrowHelper");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>Throws <see cref=\"global::System.ArgumentOutOfRangeException\"/> for an unknown value.</summary>");
        source.AppendLine("    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    public static ").Append(ctx.EnumTypeName).Append(" UnknownValue(").Append(ctx.ValueTypeName).AppendLine(" value)");
        source.AppendLine("        => throw new global::System.ArgumentOutOfRangeException(nameof(value), value, \"Unknown enum value.\");");
        source.AppendLine();
        source.AppendLine("    /// <summary>Throws <see cref=\"global::System.ArgumentOutOfRangeException\"/> for an unknown name.</summary>");
        source.AppendLine("    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    public static ").Append(ctx.EnumTypeName).AppendLine(" UnknownName(string name)");
        source.AppendLine("        => throw new global::System.ArgumentOutOfRangeException(nameof(name), name, \"Unknown enum name.\");");
        source.AppendLine("}");
        source.AppendLine();
        AppendTypeConverterClass(source, ctx.EnumType, ctx.ValueType, ctx.EnumTypeName, ctx.ValueTypeName, ctx.TypeConverterName, ctx.IsStringValue);
        source.AppendLine();
        AppendStjConverter(source, ctx);
        if (ctx.SupportsNewtonsoft)
            AppendNewtonsoftConverter(source, ctx);
    }

    private static void AppendStjConverter(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.AppendLine("/// <summary>");
        source.Append("/// System.Text.Json converter for ").Append(ctx.EnumTypeName).AppendLine(".");
        source.AppendLine("/// </summary>");
        source.Append("file sealed class ").Append(ctx.StjConverterTypeName).Append(" : global::System.Text.Json.Serialization.JsonConverter<").Append(ctx.EnumTypeName).AppendLine(">");
        source.AppendLine("{");
        source.Append("    public override ").Append(ctx.EnumTypeName)
            .AppendLine(" Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        if (ctx.IsStringValue)
        {
            source.AppendLine("        if (reader.TokenType != global::System.Text.Json.JsonTokenType.String)");
            source.AppendLine("            throw new global::System.Text.Json.JsonException(\"Expected string value.\");");
            source.AppendLine();

            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                source.Append("        if (reader.ValueTextEquals(").Append(ctx.Instances[i].ValueLiteral).Append("u8)) return ")
                    .Append(ctx.EnumTypeName).Append(".").Append(ctx.Instances[i].Name).AppendLine(";");
            }
        }
        else
        {
            source.Append(ctx.StjReadRawValueCode);
            source.AppendLine();
            source.Append("        if (").Append(ctx.EnumTypeName).AppendLine(".TryFromValue(rawValue, out var result))");
            source.AppendLine("            return result;");
        }
        source.AppendLine();
        source.AppendLine("        throw new global::System.Text.Json.JsonException(\"Unknown enum value.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void Write(global::System.Text.Json.Utf8JsonWriter writer, ").Append(ctx.EnumTypeName)
            .AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        source.Append("        ").Append(ctx.StjWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value")).AppendLine();
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override ").Append(ctx.EnumTypeName).AppendLine(" ReadAsPropertyName(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            source.Append("        if (reader.ValueTextEquals(\"").Append(EscapeString(ctx.Instances[i].Name)).Append("\"u8)) return ")
                .Append(ctx.EnumTypeName).Append(".").Append(ctx.Instances[i].Name).AppendLine(";");
        }
        source.AppendLine("        throw new global::System.Text.Json.JsonException(\"Unknown enum name.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void WriteAsPropertyName(global::System.Text.Json.Utf8JsonWriter writer, ").Append(ctx.EnumTypeName).AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("        => writer.WritePropertyName(value.Name);");
        source.AppendLine("}");
    }

    private static void AppendNewtonsoftConverter(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        string newtonsoftReadRawValueCode = BuildNewtonsoftReadRawValueCode(ctx.ValueType);
        string newtonsoftWriteValueCode = BuildNewtonsoftWriteValueCode(ctx.ValueType);
        string readReturnType = ctx.EnumType.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;
        string existingValueType = ctx.EnumType.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;
        string writeValueType = ctx.EnumType.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;

        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.Append("/// Newtonsoft.Json converter for ").Append(ctx.EnumTypeName).AppendLine(".");
        source.AppendLine("/// </summary>");
        source.Append("file sealed class ").Append(ctx.NewtonsoftConverterTypeName).Append(" : global::Newtonsoft.Json.JsonConverter<").Append(ctx.EnumTypeName).AppendLine(">");
        source.AppendLine("{");
        source.Append("    public override ").Append(readReturnType).Append(" ReadJson(global::Newtonsoft.Json.JsonReader reader, global::System.Type objectType, ").Append(existingValueType)
            .AppendLine(" existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)");
        source.AppendLine("    {");
        if (ctx.EnumType.IsReferenceType)
        {
            source.AppendLine("        if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)");
            source.AppendLine("            return null;");
            source.AppendLine();
        }
        else
        {
            source.AppendLine("        if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)");
            source.AppendLine("            throw new global::Newtonsoft.Json.JsonSerializationException(\"Cannot convert null to a value type enum value.\");");
            source.AppendLine();
        }
        source.Append(newtonsoftReadRawValueCode);
        source.AppendLine();
        source.Append("        if (").Append(ctx.EnumTypeName).AppendLine(".TryFromValue(rawValue, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.AppendLine("        throw new global::Newtonsoft.Json.JsonSerializationException(\"Unknown enum value.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void WriteJson(global::Newtonsoft.Json.JsonWriter writer, ").Append(writeValueType)
            .AppendLine(" value, global::Newtonsoft.Json.JsonSerializer serializer)");
        source.AppendLine("    {");
        if (ctx.EnumType.IsReferenceType)
        {
            source.AppendLine("        if (value is null)");
            source.AppendLine("        {");
            source.AppendLine("            writer.WriteNull();");
            source.AppendLine("            return;");
            source.AppendLine("        }");
            source.AppendLine();
        }
        source.Append("        ").Append(newtonsoftWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value")).AppendLine();
        source.AppendLine("    }");
        source.AppendLine("}");
    }
}

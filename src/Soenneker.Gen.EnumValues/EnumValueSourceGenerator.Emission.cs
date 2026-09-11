using System.Globalization;
using System.Text;
using Soenneker.Gen.EnumValues.Dtos;

namespace Soenneker.Gen.EnumValues;

/// <summary>
/// Represents the enum value source generator.
/// </summary>
public sealed partial class EnumValueSourceGenerator
{
    private const int _aggressiveInliningInstanceThreshold = 8;
    private const int _stjDispatchInstanceThreshold = 8;

    // CoreCLR caches invariant integer strings from 0 through 299. Keep its
    // direct formatting path for these values; an extra switch measured slower.
    private static bool IsSmallIntegerText(EnumInstance instance) =>
        uint.TryParse(instance.ValueJsonString, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) && value < 300;

    private static void AppendSizeDependentMethodImplAttribute(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        string? option = ctx.SizeDependentMethodImplOption;
        if (option == "Auto")
            option = ctx.Instances.Count <= _aggressiveInliningInstanceThreshold ? "AggressiveInlining" : "NoInlining";

        if (option is null)
            return;

        source.Append("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.")
              .Append(option)
              .AppendLine(")]");
    }

    private static void AppendTypeDeclaration(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.Append("[global::System.Diagnostics.DebuggerDisplay(\"{Name} ({Value})\")]");
        source.AppendLine();
        source.Append("[global::System.Text.Json.Serialization.JsonConverter(typeof(")
              .Append(ctx.StjConverterTypeName)
              .AppendLine("))]");
        if (ctx.SupportsNewtonsoft)
            source.Append("[global::Newtonsoft.Json.JsonConverter(typeof(")
                  .Append(ctx.NewtonsoftConverterTypeName)
                  .AppendLine("))]");
        source.Append("[global::System.ComponentModel.TypeConverter(typeof(")
              .Append(ctx.TypeConverterName)
              .AppendLine("))]");
        source.Append(ctx.Kind == "class" ? "public sealed partial class " : "public partial struct ")
              .Append(ctx.EnumTypeSimpleName);
        if (ctx.IsStringValue)
            source.Append(" : global::System.IEquatable<")
                  .Append(ctx.EnumTypeName)
                  .Append(">, global::System.IEquatable<string?>");
        else
            source.Append(" : global::System.IEquatable<")
                  .Append(ctx.EnumTypeName)
                  .Append(">, global::System.IEquatable<")
                  .Append(ctx.ValueTypeName)
                  .Append('>');
        source.AppendLine();
        source.AppendLine("{");
    }

    private static void AppendValueNameConstructorsAndName(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        bool useId = ctx.UseIdBacking;
        bool storeValue = useId && ctx.IsReferenceType;
        bool storeName = useId && ctx.IsReferenceType;
        bool valuePropertyGenerated = !ctx.HasValueProperty;

        if (valuePropertyGenerated)
        {
            AppendXmlSummary(source, "    ", "Gets the underlying value of this instance.");
            if (useId && !storeValue)
            {
                source.Append("    public ")
                      .Append(ctx.ValueTypeName)
                      .AppendLine(" Value => _id switch");
                source.AppendLine("    {");
                for (var i = 0; i < ctx.Instances.Count; i++)
                {
                    byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                    string valueConst = ctx.Instances[i].Name + "Value";
                    source.Append("        ")
                          .Append(instanceId)
                          .Append(" => ")
                          .Append(valueConst)
                          .AppendLine(",");
                }

                source.AppendLine("        _ => \"\"");
                source.AppendLine("    };");
            }
            else
            {
                source.Append("    public ")
                      .Append(ctx.ValueTypeName)
                      .AppendLine(" Value { get; }");
            }

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
                source.Append("    private ")
                      .Append(ctx.EnumTypeSimpleName)
                      .Append('(')
                      .Append(ctx.ValueTypeName)
                      .Append(" value, byte id)");
                source.AppendLine();
                source.AppendLine("    {");
                if (storeValue && valuePropertyGenerated)
                    source.AppendLine("        Value = value;");
                source.AppendLine("        _id = id;");
                if (!ctx.HasNameProperty && storeName)
                {
                    source.AppendLine("        Name = id switch");
                    source.AppendLine("        {");
                    for (var i = 0; i < ctx.Instances.Count; i++)
                    {
                        byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                        string nameConst = ctx.Instances[i].Name + "Name";
                        source.Append("            ")
                              .Append(instanceId)
                              .Append(" => ")
                              .Append(nameConst)
                              .AppendLine(",");
                    }

                    source.AppendLine("            _ => \"\"");
                    source.AppendLine("        };");
                }

                source.AppendLine("    }");
                source.AppendLine();
                AppendIdFromValueMethod(source, ctx);
                source.Append("    private ")
                      .Append(ctx.EnumTypeSimpleName)
                      .Append('(')
                      .Append(ctx.ValueTypeName)
                      .Append(" value) : this(value, __idFromValue(value)) { }");
            }
            else
            {
                source.Append("    private ")
                      .Append(ctx.EnumTypeSimpleName)
                      .Append('(')
                      .Append(ctx.ValueTypeName)
                      .Append(" value) => Value = value;");
            }

            source.AppendLine();
            source.AppendLine();
        }

        if (!ctx.HasNameProperty && useId)
        {
            AppendXmlSummary(source, "    ", "Gets the name of this instance.");
            if (storeName)
            {
                source.AppendLine("    public string Name { get; }");
            }
            else
            {
                source.AppendLine("    public string Name => _id switch");
                source.AppendLine("    {");
                for (var i = 0; i < ctx.Instances.Count; i++)
                {
                    byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                    string nameConst = ctx.Instances[i].Name + "Name";
                    source.Append("        ")
                          .Append(instanceId)
                          .Append(" => ")
                          .Append(nameConst)
                          .AppendLine(",");
                }

                source.AppendLine("        _ => \"\"");
                source.AppendLine("    };");
            }

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
                string condition = ctx.IsReferenceType
                    ? "global::System.Object.ReferenceEquals(this, " + ctx.Instances[i].Name + ")"
                    : "global::System.Collections.Generic.EqualityComparer<" + ctx.EnumTypeName + ">.Default.Equals(this, " + ctx.Instances[i].Name + ")";
                source.Append(condition)
                      .Append(" ? \"")
                      .Append(EscapeString(ctx.Instances[i].Name))
                      .Append('"');
                if (i < ctx.Instances.Count - 1)
                    source.AppendLine();
            }

            source.AppendLine();
            source.AppendLine("     : \"\";");
            source.AppendLine();
        }
    }

    private static void AppendIdFromValueMethod(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    private static byte __idFromValue(")
              .Append(ctx.ValueTypeName)
              .AppendLine(" value) => value switch");
        source.AppendLine("    {");
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
            string caseExpr = ctx.IsStringValue ? "\"" + EscapeString(ctx.Instances[i].StringValue ?? "") + "\"" : ctx.Instances[i].ValueLiteral;
            source.Append("        ")
                  .Append(caseExpr)
                  .Append(" => ")
                  .Append(instanceId)
                  .AppendLine(",");
        }

        source.AppendLine("        _ => throw new global::System.ArgumentOutOfRangeException(nameof(value), value, \"Unknown enum value: \" + value + \".\")");
        source.AppendLine("    };");
        source.AppendLine();
    }

    private static void AppendConstantsAllAndList(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        bool useId = ctx.UseIdBacking;

        var emittedValueConstant = false;
        bool canEmitConstant = ctx.CanEmitValueConstant;

        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            string valueFieldName = ctx.Instances[i].Name + "Value";
            if (!canEmitConstant || ctx.ExistingValueConstants[i])
                continue;

            emittedValueConstant = true;
            source.Append("    public const ")
                  .Append(ctx.ValueTypeName)
                  .Append(' ')
                  .Append(valueFieldName)
                  .Append(" = ")
                  .Append(ctx.Instances[i].ValueLiteral)
                  .AppendLine(";");
        }

        if (emittedValueConstant)
            source.AppendLine();

        var emittedNameConstant = false;
        if (ctx.Instances.Count > 0)
        {
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string nameFieldName = ctx.Instances[i].Name + "Name";
                if (ctx.ExistingNameConstants[i])
                    continue;
                emittedNameConstant = true;
                source.Append("    public const string ")
                      .Append(nameFieldName)
                      .Append(" = \"")
                      .Append(EscapeString(ctx.Instances[i].Name))
                      .AppendLine("\";");
            }

            if (emittedNameConstant)
                source.AppendLine();
        }

        var emittedInstance = false;
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            if (ctx.ExistingInstances[i])
                continue;
            emittedInstance = true;
            if (useId)
            {
                byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                source.Append("    public static readonly ")
                      .Append(ctx.EnumTypeName)
                      .Append(' ')
                      .Append(ctx.Instances[i].Name)
                      .Append(" = new(")
                      .Append(ctx.Instances[i].ValueLiteral)
                      .Append(", ")
                      .Append(instanceId)
                      .AppendLine(");");
            }
            else
                source.Append("    public static readonly ")
                      .Append(ctx.EnumTypeName)
                      .Append(' ')
                      .Append(ctx.Instances[i].Name)
                      .Append(" = new(")
                      .Append(ctx.Instances[i].ValueLiteral)
                      .AppendLine(");");
        }

        if (emittedInstance)
            source.AppendLine();

        source.Append("    private static readonly ")
              .Append(ctx.EnumTypeName)
              .Append("[] __values = new[] { ");
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            if (i > 0)
                source.Append(", ");
            source.Append(ctx.Instances[i].Name);
        }

        source.AppendLine(" };");
        source.AppendLine();
        source.Append("    private static readonly global::System.Collections.Generic.IReadOnlyList<")
              .Append(ctx.EnumTypeName)
              .AppendLine("> __list = __values;");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Gets a span of all defined instances.");
        source.Append("    public static global::System.ReadOnlySpan<")
              .Append(ctx.EnumTypeName)
              .AppendLine("> Values");
        source.AppendLine("    {");
        source.AppendLine(
            "        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.AppendLine("        get => __values;");
        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Gets a read-only list of all defined instances.");
        source.Append("    public static global::System.Collections.Generic.IReadOnlyList<")
              .Append(ctx.EnumTypeName)
              .AppendLine("> List");
        source.AppendLine("    {");
        source.AppendLine(
            "        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.AppendLine("        get => __list;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendParsingMethods(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        if (ctx.IsStringValue)
        {
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified span.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.Append("    public static bool TryFromValue(global::System.ReadOnlySpan<char> value, out ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" result)");
            source.AppendLine("    {");
            AppendSpanFirstCharSwitchBody(source, ctx.ValueSpanItems, "value", 2);
            source.AppendLine("    }");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified string.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.Append("    public static bool TryFromValue(string? value, out ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" result)");
            source.AppendLine("    {");
            source.AppendLine("        if (value is null) { result = default!; return false; }");
            source.AppendLine();
            AppendStringConstantSwitchBody(source, ctx.ValueItems, "value", 2);
            source.AppendLine();
        }
        else
        {
            AppendXmlSummary(source, "    ", "Tries to parse a value from the specified value.");
            AppendSizeDependentMethodImplAttribute(source, ctx);
            source.Append("    public static bool TryFromValue(")
                  .Append(ctx.ValueTryFromSignature)
                  .Append(", out ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" result)");
            source.AppendLine("    {");
            source.AppendLine("        switch (value)");
            source.AppendLine("        {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                source.Append("            case ")
                      .Append(ctx.Instances[i].ValueLiteral)
                      .Append(": result = ")
                      .Append(ctx.Instances[i].Name)
                      .AppendLine("; return true;");
            }

            source.AppendLine("            default:");
            source.AppendLine("                result = default!;");
            source.AppendLine("                return false;");
            source.AppendLine("        }");
        }

        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Gets the instance for the specified value, or throws if not defined.");
        source.AppendLine(
            "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ")
              .Append(ctx.EnumTypeName)
              .Append(" FromValue(")
              .Append(ctx.ValueTypeName)
              .AppendLine(" value)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromValue(value, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.Append("        return ")
              .Append(GetThrowHelperTypeName(ctx))
              .AppendLine(".UnknownValue(value);");
        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Tries to parse an instance from the specified name span.");
        AppendSizeDependentMethodImplAttribute(source, ctx);
        source.Append("    public static bool TryFromName(global::System.ReadOnlySpan<char> name, out ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" result)");
        source.AppendLine("    {");
        AppendSpanFirstCharSwitchBody(source, ctx.NameSpanItems, "name", 2);
        source.AppendLine("    }");
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Tries to parse an instance from the specified name.");
        AppendSizeDependentMethodImplAttribute(source, ctx);
        source.Append("    public static bool TryFromName(string? name, out ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" result)");
        source.AppendLine("    {");
        source.AppendLine("        if (name is null) { result = default!; return false; }");
        source.AppendLine();
        AppendStringConstantSwitchBody(source, ctx.NameItems, "name", 2);
        source.AppendLine("    }");
        source.AppendLine();

        AppendXmlSummary(source, "    ", "Gets the instance for the specified name, or throws if not defined.");
        source.AppendLine(
            "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" FromName(string name)");
        source.AppendLine("    {");
        source.AppendLine("        if (TryFromName(name, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.Append("        return ")
              .Append(GetThrowHelperTypeName(ctx))
              .AppendLine(".UnknownName(name);");
        source.AppendLine("    }");
        source.AppendLine();
        AppendIsDefinedIsNameDefined(source, ctx);
        source.AppendLine();
        AppendXmlSummary(source, "    ", "Deconstructs this instance into its name and value.");
        source.AppendLine(
            "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public void Deconstruct(out string name, out ")
              .Append(ctx.ValueTypeName)
              .AppendLine(" value)");
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
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static implicit operator string(")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" value)");
            source.AppendLine("        => value.Value;");
            source.AppendLine();
            if (useId && ctx.IsReferenceType)
            {
                AppendXmlSummary(source, "    ", "Determines whether the specified object is equal to this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other && _id == other._id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether the current instance is equal to another instance of the same type.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? other)");
                source.AppendLine("        => other is not null && _id == other._id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a hash code for this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => _id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether this instance's value equals the specified string (ordinal comparison).");
                AppendSizeDependentMethodImplAttribute(source, ctx);
                source.AppendLine("    public bool Equals(string? other)");
                source.AppendLine("        => global::System.String.Equals(Value, other, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are equal.");
                source.Append("    public static bool operator ==(")
                      .Append(ctx.EnumTypeName)
                      .Append("? left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? right)");
                source.AppendLine(
                    "        => global::System.Object.ReferenceEquals(left, right) || (left is not null && right is not null && left._id == right._id);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are not equal.");
                source.Append("    public static bool operator !=(")
                      .Append(ctx.EnumTypeName)
                      .Append("? left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? right)");
                source.AppendLine("        => !(left == right);");
            }
            else if (useId && !ctx.IsReferenceType)
            {
                AppendXmlSummary(source, "    ", "Determines whether the specified object is equal to this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other && _id == other._id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether the current instance is equal to another instance of the same type.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other)");
                source.AppendLine("        => _id == other._id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a hash code for this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => _id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether this instance's value equals the specified string (ordinal comparison).");
                AppendSizeDependentMethodImplAttribute(source, ctx);
                source.AppendLine("    public bool Equals(string? other) => _id switch");
                source.AppendLine("    {");
                for (var i = 0; i < ctx.Instances.Count; i++)
                {
                    byte instanceId = ctx.Instances[i].Id ?? (byte)(i + 1);
                    string valueConst = ctx.Instances[i].Name + "Value";
                    source.Append("        ")
                          .Append(instanceId)
                          .Append(" => other == ")
                          .Append(valueConst)
                          .AppendLine(",");
                }

                source.AppendLine("        _ => false");
                source.AppendLine("    };");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are equal.");
                source.Append("    public static bool operator ==(")
                      .Append(ctx.EnumTypeName)
                      .Append(" left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" right)");
                source.AppendLine("        => left._id == right._id;");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are not equal.");
                source.Append("    public static bool operator !=(")
                      .Append(ctx.EnumTypeName)
                      .Append(" left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" right)");
                source.AppendLine("        => left._id != right._id;");
            }
            else if (ctx.IsReferenceType)
            {
                AppendXmlSummary(source, "    ", "Determines whether the specified object is equal to this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a hash code for this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => global::System.StringComparer.Ordinal.GetHashCode(Value);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether the current instance is equal to another instance of the same type.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? other)");
                source.AppendLine("        => other is not null && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether this instance's value equals the specified string (ordinal comparison).");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other)");
                source.AppendLine("        => global::System.String.Equals(Value, other, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are equal.");
                source.Append("    public static bool operator ==(")
                      .Append(ctx.EnumTypeName)
                      .Append("? left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? right)");
                source.AppendLine("        => left is null ? right is null : right is not null && left.Equals(right);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are not equal.");
                source.Append("    public static bool operator !=(")
                      .Append(ctx.EnumTypeName)
                      .Append("? left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine("? right)");
                source.AppendLine("        => !(left == right);");
            }
            else
            {
                AppendXmlSummary(source, "    ", "Determines whether the specified object is equal to this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override bool Equals(object? obj)");
                source.Append("        => obj is ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other && global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a hash code for this instance.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override int GetHashCode()");
                source.AppendLine("        => global::System.StringComparer.Ordinal.GetHashCode(Value);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether the current instance is equal to another instance of the same type.");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.Append("    public bool Equals(")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" other)");
                source.AppendLine("        => global::System.String.Equals(Value, other.Value, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Indicates whether this instance's value equals the specified string (ordinal comparison).");
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public bool Equals(string? other)");
                source.AppendLine("        => global::System.String.Equals(Value, other, global::System.StringComparison.Ordinal);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are equal.");
                source.Append("    public static bool operator ==(")
                      .Append(ctx.EnumTypeName)
                      .Append(" left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" right)");
                source.AppendLine("        => left.Equals(right);");
                source.AppendLine();
                AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are not equal.");
                source.Append("    public static bool operator !=(")
                      .Append(ctx.EnumTypeName)
                      .Append(" left, ")
                      .Append(ctx.EnumTypeName)
                      .AppendLine(" right)");
                source.AppendLine("        => !left.Equals(right);");
            }

            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns the name of this instance.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.AppendLine("    public override string ToString() => Name;");
        }
        else
        {
            AppendXmlSummary(source, "    ", "Indicates whether the current instance is equal to another instance of the same type.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public bool Equals(")
                  .Append(ctx.EnumTypeName)
                  .Append(ctx.IsReferenceType ? "? " : " ");
            source.Append("other)");
            if (ctx.IsReferenceType)
                source.AppendLine(" => other is not null && Value.Equals(other.Value);");
            else
                source.AppendLine(" => Value.Equals(other.Value);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Indicates whether the current instance's value equals the specified value.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public bool Equals(")
                  .Append(ctx.ValueTypeName)
                  .AppendLine(" other)");
            source.AppendLine("        => Value.Equals(other);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Determines whether the specified object is equal to this instance.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.AppendLine("    public override bool Equals(object? obj)");
            source.Append("        => obj is ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" other && Equals(other);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a hash code for this instance.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.AppendLine("    public override int GetHashCode()");
            source.AppendLine("        => Value.GetHashCode();");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are equal.");
            source.Append("    public static bool operator ==(")
                  .Append(ctx.EnumTypeName)
                  .Append(" left, ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => left.Equals(right);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether two instances are not equal.");
            source.Append("    public static bool operator !=(")
                  .Append(ctx.EnumTypeName)
                  .Append(" left, ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => !left.Equals(right);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether an instance equals the specified value.");
            source.Append("    public static bool operator ==(")
                  .Append(ctx.EnumTypeName)
                  .Append(" left, ")
                  .Append(ctx.ValueTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => left.Value.Equals(right);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether an instance does not equal the specified value.");
            source.Append("    public static bool operator !=(")
                  .Append(ctx.EnumTypeName)
                  .Append(" left, ")
                  .Append(ctx.ValueTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => !left.Value.Equals(right);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether the specified value equals an instance.");
            source.Append("    public static bool operator ==(")
                  .Append(ctx.ValueTypeName)
                  .Append(" left, ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => right.Value.Equals(left);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns a value that indicates whether the specified value does not equal an instance.");
            source.Append("    public static bool operator !=(")
                  .Append(ctx.ValueTypeName)
                  .Append(" left, ")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" right)");
            source.AppendLine("        => !right.Value.Equals(left);");
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Returns the string representation of this instance.");
            bool emitIntegerTextConstants = false;
            if (ctx.ValueSpecialType is Microsoft.CodeAnalysis.SpecialType.System_Int32 or Microsoft.CodeAnalysis.SpecialType.System_Int64 or
                Microsoft.CodeAnalysis.SpecialType.System_UInt32 or Microsoft.CodeAnalysis.SpecialType.System_UInt64)
            {
                foreach (EnumInstance instance in ctx.Instances)
                {
                    if (!IsSmallIntegerText(instance))
                    {
                        emitIntegerTextConstants = true;
                        break;
                    }
                }
            }
            if (emitIntegerTextConstants)
            {
                AppendSizeDependentMethodImplAttribute(source, ctx);
                source.AppendLine("    public override string ToString() => Value switch");
                source.AppendLine("    {");
                foreach (EnumInstance instance in ctx.Instances)
                {
                    if (IsSmallIntegerText(instance))
                        continue;
                    // JSON and invariant integer text agree.
                    string text = instance.ValueJsonString ?? string.Empty;
                    source.Append("        ").Append(instance.ValueLiteral).Append(" => \"")
                        .Append(EscapeString(text)).AppendLine("\",");
                }
                // Capture the value so a custom getter is evaluated only once.
                source.Append("        var value => ").Append(ctx.ToStringExpression.Replace("Value.", "value.")).AppendLine();
                source.AppendLine("    };");
            }
            else
            {
                source.AppendLine(
                    "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                source.AppendLine("    public override string ToString()");
                source.Append("        => ").Append(ctx.ToStringExpression).AppendLine(";");
            }
            source.AppendLine();
            AppendXmlSummary(source, "    ", "Explicitly converts this instance to its underlying value.");
            source.AppendLine(
                "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            source.Append("    public static explicit operator ")
                  .Append(ctx.ValueTypeName)
                  .Append('(')
                  .Append(ctx.EnumTypeName)
                  .AppendLine(" value)");
            source.AppendLine("        => value.Value;");
        }

        source.AppendLine();
    }

    private static void AppendThrowHelperAndConverters(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        string throwHelperTypeName = GetThrowHelperTypeName(ctx);

        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Throws for unknown values or names.");
        source.AppendLine("/// </summary>");
        source.Append("file static class ")
              .Append(throwHelperTypeName)
              .AppendLine();
        source.AppendLine("{");
        source.AppendLine("    /// <summary>Throws <see cref=\"global::System.ArgumentOutOfRangeException\"/> for an unknown value.</summary>");
        source.AppendLine("    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    public static ")
              .Append(ctx.EnumTypeName)
              .Append(" UnknownValue(")
              .Append(ctx.ValueTypeName)
              .AppendLine(" value)");
        source.AppendLine("        => throw new global::System.ArgumentOutOfRangeException(nameof(value), value, \"Unknown enum value: \" + value + \".\");");
        source.AppendLine();
        source.AppendLine("    /// <summary>Throws <see cref=\"global::System.ArgumentOutOfRangeException\"/> for an unknown name.</summary>");
        source.AppendLine("    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    public static ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" UnknownName(string name)");
        source.AppendLine("        => throw new global::System.ArgumentOutOfRangeException(nameof(name), name, \"Unknown enum name: '\" + name + \"'.\");");
        source.AppendLine("}");
        source.AppendLine();
        AppendTypeConverterClass(source, ctx.EnumTypeName, ctx.TypeConverterName, ctx.IsStringValue, ctx.TypeConverterFromBody);
        source.AppendLine();
        AppendStjConverter(source, ctx);

        if (ctx.SupportsNewtonsoft)
            AppendNewtonsoftConverter(source, ctx);
    }

    private static string GetThrowHelperTypeName(in EnumSourceBuildContext ctx)
    {
        return "__" + ctx.EnumTypeSimpleName + "ThrowHelper";
    }

    private static void AppendStjConverter(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        source.AppendLine("/// <summary>");
        source.Append("/// System.Text.Json converter for ")
              .Append(ctx.EnumTypeName)
              .AppendLine(".");
        source.AppendLine("/// </summary>");
        source.Append("file sealed class ")
              .Append(ctx.StjConverterTypeName)
              .Append(" : global::System.Text.Json.Serialization.JsonConverter<")
              .Append(ctx.EnumTypeName)
              .AppendLine(">");
        source.AppendLine("{");
        source.Append("    public override ")
              .Append(ctx.EnumTypeName)
              .AppendLine(
                  " Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        if (ctx.IsStringValue)
        {
            source.AppendLine("        if (reader.TokenType != global::System.Text.Json.JsonTokenType.String)");
            source.AppendLine(
                "            throw new global::System.Text.Json.JsonException(\"Expected string value. Token type: \" + reader.TokenType + \".\");");
            source.AppendLine();

            AppendStjStringReadLookup(source, ctx);

            source.AppendLine("        string? rawStr = reader.GetString();");
            source.Append("        throw new global::System.Text.Json.JsonException($\"Cannot deserialize '")
                  .Append(ctx.EnumTypeName)
                  .AppendLine("': unknown value '\" + (rawStr ?? \"(null)\") + \"'.\");");
        }
        else
        {
            source.Append(ctx.StjReadRawValueCode);
            source.AppendLine();
            source.Append("        if (")
                  .Append(ctx.EnumTypeName)
                  .AppendLine(".TryFromValue(rawValue, out var result))");
            source.AppendLine("            return result;");
            source.Append("        throw new global::System.Text.Json.JsonException($\"Cannot deserialize '")
                  .Append(ctx.EnumTypeName)
                  .AppendLine("': unknown value '\" + rawValue + \"'.\");");
        }

        source.AppendLine();
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void Write(global::System.Text.Json.Utf8JsonWriter writer, ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)");
        if (ctx.IsStringValue)
        {
            source.AppendLine("    {");
            source.AppendLine("        switch (value.Value)");
            source.AppendLine("        {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string jsonStr = ctx.Instances[i].ValueJsonString ?? ctx.Instances[i].ValueLiteral;
                source.Append("            case ")
                      .Append(ctx.EnumTypeName)
                      .Append('.')
                      .Append(ctx.Instances[i].Name)
                      .Append("Value: writer.WriteStringValue(\"")
                      .Append(EscapeString(jsonStr))
                      .Append("\"u8); return;")
                      .AppendLine();
            }

            source.Append("            default: ")
                  .Append(ctx.StjWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value"))
                  .AppendLine();
            source.AppendLine("                return;");
            source.AppendLine("        }");
            source.AppendLine("    }");
        }
        else
        {
            source.AppendLine("    {");
            source.Append("        ")
                  .Append(ctx.StjWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value"))
                  .AppendLine();
            source.AppendLine("    }");
        }
        source.AppendLine();
        source.Append("    public override ")
              .Append(ctx.EnumTypeName)
              .AppendLine(
                  " ReadAsPropertyName(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        source.AppendLine("    {");
        if (ctx.IsStringValue)
        {
            AppendStjStringReadLookup(source, ctx);
        }
        else
        {
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string jsonStr = ctx.Instances[i].ValueJsonString ?? ctx.Instances[i].ValueLiteral;
                source.Append("        if (reader.ValueTextEquals(\"")
                      .Append(EscapeString(jsonStr))
                      .Append("\"u8)) return ")
                      .Append(ctx.EnumTypeName)
                      .Append('.')
                      .Append(ctx.Instances[i].Name)
                      .AppendLine(";");
            }
        }

        source.AppendLine("        string? rawPropName = reader.GetString();");
        source.Append("        throw new global::System.Text.Json.JsonException($\"Cannot deserialize '")
              .Append(ctx.EnumTypeName)
              .AppendLine("': unknown property name '\" + (rawPropName ?? \"(null)\") + \"'.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void WriteAsPropertyName(global::System.Text.Json.Utf8JsonWriter writer, ")
              .Append(ctx.EnumTypeName)
              .AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)");
        if (ctx.IsStringValue)
        {
            source.AppendLine("    {");
            source.AppendLine("        switch (value.Value)");
            source.AppendLine("        {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string jsonStr = ctx.Instances[i].ValueJsonString ?? ctx.Instances[i].ValueLiteral;
                source.Append("            case ")
                      .Append(ctx.EnumTypeName)
                      .Append('.')
                      .Append(ctx.Instances[i].Name)
                      .Append("Value: writer.WritePropertyName(\"")
                      .Append(EscapeString(jsonStr))
                      .Append("\"u8); return;")
                      .AppendLine();
            }

            source.AppendLine("            default: writer.WritePropertyName(value.Value); return;");
            source.AppendLine("        }");
            source.AppendLine("    }");
        }
        else
        {
            source.AppendLine("    {");
            source.AppendLine("        switch (value.Value)");
            source.AppendLine("        {");
            for (var i = 0; i < ctx.Instances.Count; i++)
            {
                string jsonStr = ctx.Instances[i].ValueJsonString ?? ctx.Instances[i].ValueLiteral;
                source.Append("            case ")
                      .Append(ctx.Instances[i].ValueLiteral)
                      .Append(": writer.WritePropertyName(\"")
                      .Append(EscapeString(jsonStr))
                      .Append("\"u8); return;")
                      .AppendLine();
            }

            source.Append(ctx.StjPropertyNameFallback)
                  .AppendLine();
            source.AppendLine("        }");
            source.AppendLine("    }");
        }

        if (ctx.IsStringValue && ctx.Instances.Count >= _stjDispatchInstanceThreshold)
        {
            AppendStjDecodedLookup(source, ctx);
            AppendStjUtf8Lookup(source, ctx);
        }

        source.AppendLine("}");
    }

    private static void AppendStjDecodedLookup(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        // Isolate stack allocation and decoding from the common contiguous UTF-8 path.
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        source.Append("    private static bool TryReadDecoded(ref global::System.Text.Json.Utf8JsonReader reader, out ")
              .Append(ctx.EnumTypeName).AppendLine(" result)");
        source.AppendLine("    {");
        source.AppendLine("#if NET7_0_OR_GREATER");
        source.AppendLine("        long length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;");
        source.AppendLine("        if (length <= 256)");
        source.AppendLine("        {");
        source.AppendLine("            global::System.Span<byte> decoded = stackalloc byte[256];");
        source.AppendLine("            int written = reader.CopyString(decoded);");
        source.AppendLine("            return TryReadUtf8(decoded.Slice(0, written), out result);");
        source.AppendLine("        }");
        source.AppendLine("#endif");
        // Long inputs and older runtimes use the reader's allocation-free comparison.
        // No stack allocation is sized from untrusted input.
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            source.Append("        if (reader.ValueTextEquals(").Append(ctx.Instances[i].ValueLiteral).AppendLine("u8))");
            source.AppendLine("        {");
            source.Append("            result = ").Append(ctx.EnumTypeName).Append('.').Append(ctx.Instances[i].Name).AppendLine(";");
            source.AppendLine("            return true;");
            source.AppendLine("        }");
        }
        source.AppendLine("        result = default!;");
        source.AppendLine("        return false;");
        source.AppendLine("    }");
    }

    private static int GetUtf8DispatchIndex(in EnumSourceBuildContext ctx, System.Collections.Generic.List<int> indexes, int length)
    {
        // Minimize collisions instead of always probing the first byte. Values such
        // as common-prefix-01 and common-prefix-02 otherwise become a linear scan.
        var counts = new int[256];
        int bestIndex = 0;
        int bestScore = int.MaxValue;
        for (var position = 0; position < length; position++)
        {
            System.Array.Clear(counts, 0, counts.Length);
            int score = 0;
            foreach (int index in indexes)
                score += 2 * counts[ctx.ValueUtf8Bytes[index][position]]++ + 1;
            if (score >= bestScore)
                continue;
            bestScore = score;
            bestIndex = position;
            if (score == indexes.Count)
                break;
        }
        return bestIndex;
    }

    private static void AppendStjStringReadLookup(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        if (ctx.Instances.Count < _stjDispatchInstanceThreshold)
        {
            for (var i = 0; i < ctx.Instances.Count; i++)
                AppendStjValueTextEquals(source, ctx, i, 2);

            return;
        }

        source.AppendLine("        if (reader.ValueIsEscaped || reader.HasValueSequence)");
        source.AppendLine("        {");
        source.AppendLine("            if (TryReadDecoded(ref reader, out var decodedResult)) return decodedResult;");
        source.AppendLine("        }");
        source.AppendLine("        else");
        source.AppendLine("        {");
        source.AppendLine("            global::System.ReadOnlySpan<byte> rawValue = reader.ValueSpan;");
        AppendStjUtf8Lookup(source, ctx, standalone: false);
        source.AppendLine("        }");
    }

    private static void AppendStjUtf8Lookup(StringBuilder source, in EnumSourceBuildContext ctx, bool standalone = true)
    {
        if (standalone)
        {
            source.Append("    private static bool TryReadUtf8(global::System.ReadOnlySpan<byte> rawValue, out ")
                  .Append(ctx.EnumTypeName).AppendLine(" result)");
            source.AppendLine("    {");
        }
        source.AppendLine("            switch (rawValue.Length)");
        source.AppendLine("            {");

        var lengths = new global::System.Collections.Generic.SortedDictionary<int, global::System.Collections.Generic.List<int>>();
        for (var i = 0; i < ctx.Instances.Count; i++)
        {
            int byteCount = ctx.ValueUtf8Bytes[i].Length;
            if (!lengths.TryGetValue(byteCount, out global::System.Collections.Generic.List<int>? indexes))
            {
                indexes = new global::System.Collections.Generic.List<int>();
                lengths.Add(byteCount, indexes);
            }

            indexes.Add(i);
        }

        foreach (global::System.Collections.Generic.KeyValuePair<int, global::System.Collections.Generic.List<int>> lengthGroup in lengths)
        {
            source.Append("                case ")
                  .Append(lengthGroup.Key)
                  .AppendLine(":");

            if (lengthGroup.Key == 0 || lengthGroup.Value.Count == 1)
            {
                foreach (int index in lengthGroup.Value)
                    AppendUtf8Equals(source, ctx, index, 5, standalone);
            }
            else
            {
                int dispatchIndex = GetUtf8DispatchIndex(ctx, lengthGroup.Value, lengthGroup.Key);
                source.Append("                    switch (rawValue[").Append(dispatchIndex).AppendLine("])");
                source.AppendLine("                    {");
                var firstBytes = new global::System.Collections.Generic.SortedDictionary<byte, global::System.Collections.Generic.List<int>>();
                foreach (int index in lengthGroup.Value)
                {
                    byte firstByte = ctx.ValueUtf8Bytes[index][dispatchIndex];
                    if (!firstBytes.TryGetValue(firstByte, out global::System.Collections.Generic.List<int>? indexes))
                    {
                        indexes = new global::System.Collections.Generic.List<int>();
                        firstBytes.Add(firstByte, indexes);
                    }

                    indexes.Add(index);
                }

                foreach (global::System.Collections.Generic.KeyValuePair<byte, global::System.Collections.Generic.List<int>> firstByteGroup in firstBytes)
                {
                    source.Append("                        case ")
                          .Append(firstByteGroup.Key)
                          .AppendLine(":");
                    foreach (int index in firstByteGroup.Value)
                        AppendUtf8Equals(source, ctx, index, 7, standalone);
                    source.AppendLine("                            break;");
                }

                source.AppendLine("                    }");
            }

            source.AppendLine("                    break;");
        }

        source.AppendLine("            }");
        if (standalone)
        {
            source.AppendLine("        result = default!;");
            source.AppendLine("        return false;");
            source.AppendLine("    }");
        }
    }

    private static void AppendUtf8Equals(StringBuilder source, in EnumSourceBuildContext ctx, int index, int indentLevel, bool standalone)
    {
        EnumInstance instance = ctx.Instances[index];
        source.Append(' ', indentLevel * 4).Append("if (global::System.MemoryExtensions.SequenceEqual(rawValue, ")
              .Append(instance.ValueLiteral).Append(standalone ? "u8)) { result = " : "u8)) { return ").Append(ctx.EnumTypeName).Append('.')
              .Append(instance.Name).AppendLine(standalone ? "; return true; }" : "; }");
    }

    private static void AppendStjValueTextEquals(StringBuilder source, in EnumSourceBuildContext ctx, int instanceIndex, int indentLevel)
    {
        EnumInstance instance = ctx.Instances[instanceIndex];
        source.Append(' ', indentLevel * 4)
              .Append("if (reader.ValueTextEquals(")
              .Append(instance.ValueLiteral)
              .Append("u8)) return ")
              .Append(ctx.EnumTypeName)
              .Append('.')
              .Append(instance.Name)
              .AppendLine(";");
    }

    private static void AppendNewtonsoftConverter(StringBuilder source, in EnumSourceBuildContext ctx)
    {
        string newtonsoftReadRawValueCode = ctx.NewtonsoftReadCode;
        string newtonsoftWriteValueCode = ctx.NewtonsoftWriteCode;
        string readReturnType = ctx.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;
        string existingValueType = ctx.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;
        string writeValueType = ctx.IsReferenceType ? ctx.EnumTypeName + "?" : ctx.EnumTypeName;

        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.Append("/// Newtonsoft.Json converter for ")
              .Append(ctx.EnumTypeName)
              .AppendLine(".");
        source.AppendLine("/// </summary>");
        source.Append("file sealed class ")
              .Append(ctx.NewtonsoftConverterTypeName)
              .Append(" : global::Newtonsoft.Json.JsonConverter<")
              .Append(ctx.EnumTypeName)
              .AppendLine(">");
        source.AppendLine("{");
        source.Append("    public override ")
              .Append(readReturnType)
              .Append(" ReadJson(global::Newtonsoft.Json.JsonReader reader, global::System.Type objectType, ")
              .Append(existingValueType)
              .AppendLine(" existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)");
        source.AppendLine("    {");
        if (ctx.IsReferenceType)
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
        source.Append("        if (")
              .Append(ctx.EnumTypeName)
              .AppendLine(".TryFromValue(rawValue, out var result))");
        source.AppendLine("            return result;");
        source.AppendLine();
        source.Append("        throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot deserialize '")
              .Append(ctx.EnumTypeName)
              .AppendLine("': unknown value '\" + rawValue + \"'.\");");
        source.AppendLine("    }");
        source.AppendLine();
        source.Append("    public override void WriteJson(global::Newtonsoft.Json.JsonWriter writer, ")
              .Append(writeValueType)
              .AppendLine(" value, global::Newtonsoft.Json.JsonSerializer serializer)");
        source.AppendLine("    {");
        if (ctx.IsReferenceType)
        {
            source.AppendLine("        if (value is null)");
            source.AppendLine("        {");
            source.AppendLine("            writer.WriteNull();");
            source.AppendLine("            return;");
            source.AppendLine("        }");
            source.AppendLine();
        }

        source.Append("        ")
              .Append(newtonsoftWriteValueCode.Replace("{VALUE_EXPRESSION}", "value.Value"))
              .AppendLine();
        source.AppendLine("    }");
        source.AppendLine("}");
    }
}

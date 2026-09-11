using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues.Dtos;

// Built within one transform and immutable once returned to the pipeline.
internal sealed class EnumGenerationResult : IEquatable<EnumGenerationResult>
{
    public string? HintName;
    public EnumSourceBuildContext? BuildContext;
    public List<Diagnostic>? Diagnostics;

    public void ReportDiagnostic(Diagnostic diagnostic) => (Diagnostics ??= new List<Diagnostic>()).Add(diagnostic);

    public bool Equals(EnumGenerationResult? other)
    {
        if (other is null || HintName != other.HintName || !Nullable.Equals(BuildContext, other.BuildContext))
            return false;
        int count = Diagnostics?.Count ?? 0;
        if (count != (other.Diagnostics?.Count ?? 0))
            return false;
        for (var i = 0; i < count; i++)
            if (!Diagnostics![i].Equals(other.Diagnostics![i]))
                return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is EnumGenerationResult other && Equals(other);
    public override int GetHashCode() => BuildContext?.GetHashCode() ?? 0;
}

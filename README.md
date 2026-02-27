[![](https://img.shields.io/nuget/v/soenneker.gen.enumvalues.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.gen.enumvalues/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.gen.enumvalues/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.gen.enumvalues/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.gen.enumvalues.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.gen.enumvalues/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Gen.EnumValues
### Compile-time generated enum values. Zero reflection. Zero allocations.

Generate value objects with fast lookup APIs, switch-friendly constants, and built-in JSON serialization (System.Text.Json/Newtonsoft.Json).

## Installation

```bash
dotnet add package Soenneker.Gen.EnumValues
```

## Usage

Annotate a partial type with `[EnumValue]` (defaults to `int`) or `[EnumValue<T>]`:

```csharp
using Soenneker.Gen.EnumValues;

[EnumValue]
public sealed partial class OrderStatus
{
    public static readonly OrderStatus Pending = new(1);
    public static readonly OrderStatus Completed = new(2);
}
```

```csharp
using Soenneker.Gen.EnumValues;

[EnumValue<string>]
public sealed partial class ColorCode
{
    public static readonly ColorCode Red = new("R");
    public static readonly ColorCode Blue = new("B");
}
```

The generator emits:

- `List`
- `<MemberName>Value` constants (for constant-friendly switch labels)
- `TryFromValue(TValue value, out TEnum result)`
- `FromValue(TValue value)`
- `TryFromName(string name, out TEnum result)`
- `FromName(string name)`

## Lookups

```csharp
if (OrderStatus.TryFromValue(1, out var pending))
{
    // pending == OrderStatus.Pending
}

var completed = OrderStatus.FromValue(2);

if (ColorCode.TryFromName("Red", out var red))
{
    // red == ColorCode.Red
}
```

## Switching over values

Switch labels must be compile-time constants. The generator emits `<MemberName>Value` constants so you can switch efficiently on `Value`:

```csharp
switch (orderStatus.Value)
{
    case OrderStatus.PendingValue:
        // ...
        break;
    case OrderStatus.CompletedValue:
        // ...
        break;
}
```

If your variable is already the raw value type (`int`, `string`, etc.), you can switch directly on that variable with the same constants.

## Serialization

`System.Text.Json` is always supported and the converter is applied automatically.

`Newtonsoft.Json` is also supported automatically when your project references `Newtonsoft.Json`:

```bash
dotnet add package Newtonsoft.Json
```

After that, both serializers round-trip by `Value`.

`Value` and the value constructor are generated automatically if they do not already exist.

## Composing types with `[IncludeEnumValues]`

You can reuse instances from another enum-value type by adding `[IncludeEnumValues(typeof(SourceType))]`. The generator merges your type’s own static instances with all instances from the source type. Order is deterministic: your own instances first (source order), then each included type’s instances in attribute order and source order.

**Example: source type**

```csharp
[EnumValue<string>]
public sealed partial class CommonKeyword
{
    public static readonly CommonKeyword Default = new("default");
    public static readonly CommonKeyword Auto = new("auto");
    public static readonly CommonKeyword None = new("none");
}
```

**Example: composed type**

```csharp
[EnumValue<string>]
[IncludeEnumValues(typeof(CommonKeyword))]
public sealed partial class SortDirection
{
    public static readonly SortDirection Ascending = new("asc");
    public static readonly SortDirection Descending = new("desc");
}
```

`SortDirection` then has five instances: `Ascending`, `Descending` (own), then `Default`, `Auto`, `None` (from `CommonKeyword`). The generator emits static readonly fields for included instances (e.g. `SortDirection.Default`), so `List`, `All`, `TryFromValue`, `TryFromName`, and JSON work for both own and included values.

**Requirements**

- The source type must be an enum-value type: `[EnumValue]` or `[EnumValue<T>]` with the **same** value type as the target (e.g. both `[EnumValue<string>]`).
- You can use multiple `[IncludeEnumValues(typeof(A))]`, `[IncludeEnumValues(typeof(B))]`; instances are merged in attribute order.

**Collisions**

- **Value collision:** If the same value appears in both the target and an included type, or in two included types, the generator reports an error (e.g. *Duplicate enum value 'none' in SortDirection from CommonKeyword*).
- **Name collision:** If the target (or an earlier included type) already has a member with the same name as an included instance, the generator reports an error (e.g. *Member name 'None' in SortDirection conflicts with included member from CommonKeyword*).

## Notes

- The enum type must be `partial`.
- Top-level non-generic class/struct types are supported.
- Static instances must be initialized with a compile-time constant first constructor argument.
- `<MemberName>Value` constants are emitted for const-compatible value types (for example: numeric types, `string`, `char`, `bool`).

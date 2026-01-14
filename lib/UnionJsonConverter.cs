// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentAuthenticity;

public class UnionJsonConverter<TLeft, TRight, TUnion> : JsonConverter<TUnion>
    where TLeft : class
    where TRight : class
{
    private static readonly Lazy<(MethodInfo LeftFactory, MethodInfo RightFactory, PropertyInfo LeftProp, PropertyInfo RightProp)> Accessors =
        new(CreateAccessors, isThreadSafe: true);

    public override TUnion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Preserve existing behavior of generated converters: null maps to default.
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default!;
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        var element = doc.RootElement;

        try
        {
            var left = element.Deserialize<TLeft>(options);
            if (left is not null)
            {
                return (TUnion)Accessors.Value.LeftFactory.Invoke(null, [left])!;
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            var right = element.Deserialize<TRight>(options);
            if (right is not null)
            {
                return (TUnion)Accessors.Value.RightFactory.Invoke(null, [right])!;
            }
        }
        catch (JsonException)
        {
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        var (leftFactory, rightFactory, leftProp, rightProp) = Accessors.Value;

        var left = (TLeft?)leftProp.GetValue(value);
        var right = (TRight?)rightProp.GetValue(value);

        var count = 0;
        if (left is not null) count++;
        if (right is not null) count++;

        if (count > 1)
            throw new JsonException();

        if (left is not null)
        {
            JsonSerializer.Serialize(writer, left, options);
            return;
        }

        if (right is not null)
        {
            JsonSerializer.Serialize(writer, right, options);
            return;
        }

        throw new JsonException();
    }

    private static (MethodInfo LeftFactory, MethodInfo RightFactory, PropertyInfo LeftProp, PropertyInfo RightProp) CreateAccessors()
    {
        var unionType = typeof(TUnion);

        var leftFactory = FindFromFactory(unionType, typeof(TLeft));
        var rightFactory = FindFromFactory(unionType, typeof(TRight));

        var leftProp = FindTypedProperty(unionType, typeof(TLeft));
        var rightProp = FindTypedProperty(unionType, typeof(TRight));

        return (leftFactory, rightFactory, leftProp, rightProp);
    }

    private static MethodInfo FindFromFactory(Type unionType, Type argType)
    {
        foreach (var m in unionType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (!m.Name.StartsWith("From", StringComparison.Ordinal))
                continue;

            if (m.ReturnType != unionType)
                continue;

            var ps = m.GetParameters();
            if (ps.Length != 1)
                continue;

            if (ps[0].ParameterType == argType)
                return m;
        }

        throw new InvalidOperationException($"{unionType.FullName} must expose a public static factory method From*(" + argType.Name + ") that returns " + unionType.Name + ".");
    }

    private static PropertyInfo FindTypedProperty(Type unionType, Type propType)
    {
        foreach (var p in unionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.PropertyType == propType)
                return p;
        }

        throw new InvalidOperationException($"{unionType.FullName} must expose a public instance property of type " + propType.Name + ".");
    }
}

public sealed class NullableUnionJsonConverter<TLeft, TRight, TUnion> : UnionJsonConverter<TLeft, TRight, TUnion>
    where TLeft : class
    where TRight : class
{
    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        try
        {
            base.Write(writer, value, options);
        }
        catch (JsonException)
        {
            // For unions that explicitly allow null, the default struct value (no branch set)
            // should roundtrip as JSON null.
            writer.WriteNullValue();
        }
    }
}

// Back-compat for the typo in the request.
public sealed class UnionJsonCoverter<TLeft, TRight, TUnion> : UnionJsonConverter<TLeft, TRight, TUnion>
    where TLeft : class
    where TRight : class
{
}
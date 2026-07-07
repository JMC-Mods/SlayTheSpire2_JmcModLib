using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace JmcModLib.Persistence.Storage;

internal static class PersistenceJson
{
    public static JsonSerializerSettings Settings { get; } = CreateSettings();

    public static JsonSerializer Serializer => JsonSerializer.Create(Settings);

    public static JToken ToToken(object? value)
    {
        return value == null
            ? JValue.CreateNull()
            : JToken.FromObject(value, Serializer);
    }

    public static object? ToObject(JToken? token, Type valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
        {
            return valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
        }

        return token.ToObject(valueType, Serializer);
    }

    public static object? CloneValue(object? value, Type valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        if (value == null)
        {
            return valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
        }

        try
        {
            return ToObject(ToToken(value), valueType);
        }
        catch
        {
            return value;
        }
    }

    private static JsonSerializerSettings CreateSettings()
    {
        return new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            Converters =
            {
                new StringEnumConverter(),
            },
        };
    }
}

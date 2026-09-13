using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Andes.Agents.Service.Serialization;

/// <summary>Serializer settings for every record this application keeps in an agent session's state bag.</summary>
public static class SessionStateJson
{
    /// <summary>Web defaults: camelCase, case-insensitive reads.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        // The state bag resolves metadata through GetTypeInfo, which, unlike JsonSerializer, never supplies a missing resolver.
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        options.MakeReadOnly();

        return options;
    }
}

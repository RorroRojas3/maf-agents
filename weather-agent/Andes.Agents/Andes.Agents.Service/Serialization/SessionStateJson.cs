using System.Text.Json;

namespace Andes.Agents.Service.Serialization;

/// <summary>Serializer settings for every record this application keeps in an agent session's state bag.</summary>
public static class SessionStateJson
{
    /// <summary>Web defaults: camelCase, case-insensitive reads.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

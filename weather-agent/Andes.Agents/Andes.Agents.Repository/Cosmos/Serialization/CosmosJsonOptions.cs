using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andes.Agents.Repository.Cosmos.Serialization;

/// <summary>Serializer settings the Cosmos client is created with; every document in this layer is shaped by them.</summary>
public static class CosmosJsonOptions
{
    /// <summary>camelCase names, nulls omitted, fixed-width UTC timestamps.</summary>
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());

        return options;
    }
}

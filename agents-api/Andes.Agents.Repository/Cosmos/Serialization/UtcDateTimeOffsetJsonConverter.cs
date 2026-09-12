using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andes.Agents.Repository.Cosmos.Serialization;

/// <summary>Writes timestamps as fixed-width UTC (<c>yyyy-MM-ddTHH:mm:ss.fffffffZ</c>) so Cosmos DB can order them as strings.</summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string _format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString(_format, CultureInfo.InvariantCulture));
}

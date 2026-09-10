namespace Andes.Agents.Common.Constants;

/// <summary>Limits a protocol continuation id must satisfy before it becomes a Cosmos DB document id and partition value.</summary>
public static class SessionIdRules
{
    /// <summary>Longest id Cosmos DB accepts.</summary>
    public const int MaxLength = 255;

    /// <summary>Characters Cosmos DB forbids in an id.</summary>
    public const string InvalidCharacters = "/\\?#";
}

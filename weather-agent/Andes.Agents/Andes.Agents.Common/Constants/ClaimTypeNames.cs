namespace Andes.Agents.Common.Constants;

/// <summary>Claim types the API reads from a validated Entra ID token.</summary>
public static class ClaimTypeNames
{
    /// <summary>The object id as it appears when inbound claim mapping is off.</summary>
    public const string Oid = "oid";

    /// <summary>The object id as the default inbound claim mapping renames it.</summary>
    public const string ObjectIdentifier = "http://schemas.microsoft.com/identity/claims/objectidentifier";
}

namespace Andes.Agents.Api.Problems;

// Opaque identifiers clients match verbatim, not links; changing one is a breaking API change.
internal static class ProblemTypes
{
    private const string _base = "/problems/";

    public const string ValidationError = _base + "validation-error";
    public const string ResourceNotFound = _base + "resource-not-found";
    public const string Forbidden = _base + "forbidden";
    public const string ConversationBusy = _base + "conversation-busy";
    public const string TooManyRequests = _base + "too-many-requests";
}

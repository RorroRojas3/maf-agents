namespace Andes.Agents.Api.Options;

/// <summary>Whether the OpenAPI document and its Scalar reference UI are served outside Development.</summary>
public sealed class ApiDocsOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "ApiDocs";

    /// <summary>Gets or sets whether the document and UI are mapped; Development maps them regardless.</summary>
    public bool Enabled { get; set; }
}

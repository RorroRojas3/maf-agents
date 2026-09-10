using System.Collections.Concurrent;

namespace Andes.Agents.Service.Prompts;

/// <summary>Reads prompt templates embedded in this assembly.</summary>
public interface IPromptTemplateLoader
{
    /// <summary>Returns the template with the given logical resource name.</summary>
    /// <exception cref="FileNotFoundException">No embedded resource has that name.</exception>
    string Load(string name);
}

/// <inheritdoc />
public sealed class PromptTemplateLoader : IPromptTemplateLoader
{
    private readonly ConcurrentDictionary<string, string> _templates = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string Load(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _templates.GetOrAdd(name, Read);
    }

    private static string Read(string name)
    {
        using Stream stream = typeof(PromptTemplateLoader).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"No prompt is embedded under '{name}'.", name);
        using StreamReader reader = new(stream);

        return reader.ReadToEnd().Trim();
    }
}

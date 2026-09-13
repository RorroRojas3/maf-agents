using System.ComponentModel.DataAnnotations.Schema;
using Andes.Agents.Entity.Base;

namespace Andes.Agents.Entity.Agents;

/// <summary>A period during which an agent runs on a model.</summary>
/// <remarks>The period starts at <see cref="BaseEntity.DateCreated"/>; an agent has at most one mapping without <see cref="DateDeactivated"/>.</remarks>
[Table("AgentModelMapping", Schema = "Core.Ref")]
public sealed class AgentModelMapping : BaseEntity
{
    /// <summary>Gets or sets the key of the agent.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Gets or sets the key of the model the agent runs on.</summary>
    public Guid ModelId { get; set; }

    /// <summary>Gets or sets the UTC time the period ended; null while this is the agent's active model.</summary>
    public DateTimeOffset? DateDeactivated { get; set; }
}

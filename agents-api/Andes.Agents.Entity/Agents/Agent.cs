using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Andes.Agents.Common.Constants;
using Andes.Agents.Entity.Base;

namespace Andes.Agents.Entity.Agents;

/// <summary>An agent the application hosts.</summary>
[Table("Agent", Schema = "Core.Ref")]
public sealed class Agent : BaseEntity
{
    /// <summary>Gets or sets the registration name, one of <see cref="AgentNames"/>; unique.</summary>
    [StringLength(64)]
    public required string Name { get; set; }
}

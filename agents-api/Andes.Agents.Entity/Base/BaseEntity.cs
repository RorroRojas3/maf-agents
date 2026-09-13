using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andes.Agents.Entity.Base;

/// <summary>Identity, audit stamps and concurrency token shared by every relational entity.</summary>
public abstract class BaseEntity
{
    /// <summary>Gets or sets the key; generated when the entity is added.</summary>
    [Key]
    // Generated on add as an EF sequential GUID, not Guid.CreateVersion7(): SQL Server orders uniqueidentifier by its
    // last six bytes first, so version 7 values would scatter inserts across the clustered key.
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the UTC time the entity was created; stamped on the first save unless already set.</summary>
    public DateTimeOffset DateCreated { get; set; }

    /// <summary>Gets or sets the UTC time the entity last changed; stamped on every save unless the caller changed it.</summary>
    public DateTimeOffset DateModified { get; set; }

    /// <summary>Gets or sets the store-generated version, compared on every update and delete.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

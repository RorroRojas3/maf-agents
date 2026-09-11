namespace Andes.Agents.Entity.Base;

/// <summary>Identity, audit stamps and concurrency token shared by every relational entity.</summary>
public abstract class BaseEntity
{
    /// <summary>Gets or sets the key; generated when the entity is added.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the UTC time the entity was first saved.</summary>
    public DateTimeOffset DateCreated { get; set; }

    /// <summary>Gets or sets the UTC time of the last save.</summary>
    public DateTimeOffset DateUpdated { get; set; }

    /// <summary>Gets or sets the store-generated version, compared on every update and delete.</summary>
    public byte[] RowVersion { get; set; } = [];
}

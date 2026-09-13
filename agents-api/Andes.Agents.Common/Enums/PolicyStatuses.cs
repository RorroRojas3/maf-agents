namespace Andes.Agents.Common.Enums;

/// <summary>Where an insurance policy is in its lifecycle.</summary>
/// <remarks>Stored by name and constrained to these names, so renaming or adding a member is a schema change.</remarks>
public enum PolicyStatuses
{
    /// <summary>Quoted or being prepared; not yet in force.</summary>
    Draft,

    /// <summary>In force between its effective and expiration dates.</summary>
    Active,

    /// <summary>Out of force because a premium went unpaid.</summary>
    Lapsed,

    /// <summary>Terminated before its expiration date.</summary>
    Cancelled,

    /// <summary>Reached its expiration date without being renewed.</summary>
    Expired,
}

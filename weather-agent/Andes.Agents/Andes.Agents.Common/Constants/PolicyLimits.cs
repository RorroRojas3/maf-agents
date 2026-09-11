namespace Andes.Agents.Common.Constants;

/// <summary>Bounds of a stored policy that the store configuration and any validator of policy input agree on.</summary>
public static class PolicyLimits
{
    /// <summary>Longest policy number.</summary>
    public const int PolicyNumberMaxLength = 32;

    /// <summary>Longest product code.</summary>
    public const int ProductCodeMaxLength = 20;

    /// <summary>Longest reference to the holder in the system of record.</summary>
    public const int HolderReferenceMaxLength = 64;

    /// <summary>Longest holder name.</summary>
    public const int HolderNameMaxLength = 200;

    /// <summary>Exact length of an ISO 4217 currency code.</summary>
    public const int CurrencyCodeLength = 3;

    /// <summary>Total digits of a monetary amount.</summary>
    public const int MoneyPrecision = 19;

    /// <summary>Digits of a monetary amount after the decimal point.</summary>
    public const int MoneyScale = 4;
}

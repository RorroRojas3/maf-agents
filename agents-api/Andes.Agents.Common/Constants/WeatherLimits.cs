namespace Andes.Agents.Common.Constants;

/// <summary>Bounds the weather tools and service agree on.</summary>
public static class WeatherLimits
{
    /// <summary>Longest forecast a single call may ask for, today included.</summary>
    public const int MaxForecastDays = 7;
}

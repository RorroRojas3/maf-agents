using System.Globalization;
using System.Text;
using Andes.Agents.Common.Constants;

namespace Andes.Agents.Service.Weather;

/// <summary>Resolves places and reports current conditions and daily forecasts for them.</summary>
public interface IWeatherService
{
    /// <summary>Finds places matching a free-text query; empty when nothing matches.</summary>
    Task<IReadOnlyList<LocationResult>> SearchLocationsAsync(string query, CancellationToken cancellationToken);

    /// <summary>Reports the weather at a coordinate right now.</summary>
    Task<CurrentConditionsResult> GetCurrentConditionsAsync(string locationName, double latitude, double longitude, CancellationToken cancellationToken);

    /// <summary>Reports the forecast at a coordinate for the next <paramref name="days"/> days, today included.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is below 1 or above <see cref="WeatherLimits.MaxForecastDays"/>.</exception>
    Task<DailyForecastResult> GetDailyForecastAsync(string locationName, double latitude, double longitude, int days, CancellationToken cancellationToken);
}

/// <summary>Deterministic in-process weather: the same place and date always yield the same readings.</summary>
/// <remarks>
/// A stand-in for a network provider. Readings derive from latitude, season and a stable hash of the
/// coordinate and local date, so follow-up questions within a day stay consistent. Only the places in
/// the built-in gazetteer resolve; anything else is a miss, never an invention.
/// </remarks>
public sealed class WeatherService(TimeProvider timeProvider) : IWeatherService
{
    private static readonly string[] _conditions =
        ["Sunny", "Mostly sunny", "Partly cloudy", "Cloudy", "Light rain", "Showers", "Thunderstorms", "Fog", "Windy"];

    private static readonly string[] _windDirections = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    private static readonly LocationResult[] _gazetteer =
    [
        new("Seattle", "United States", 47.6062, -122.3321),
        new("Portland", "United States", 45.5152, -122.6784),
        new("San Francisco", "United States", 37.7749, -122.4194),
        new("Los Angeles", "United States", 34.0522, -118.2437),
        new("Denver", "United States", 39.7392, -104.9903),
        new("Austin", "United States", 30.2672, -97.7431),
        new("Chicago", "United States", 41.8781, -87.6298),
        new("Miami", "United States", 25.7617, -80.1918),
        new("New York", "United States", 40.7128, -74.0060),
        new("Boston", "United States", 42.3601, -71.0589),
        new("Vancouver", "Canada", 49.2827, -123.1207),
        new("Toronto", "Canada", 43.6532, -79.3832),
        new("Montreal", "Canada", 45.5019, -73.5674),
        new("Mexico City", "Mexico", 19.4326, -99.1332),
        new("Guadalajara", "Mexico", 20.6597, -103.3496),
        new("Bogotá", "Colombia", 4.7110, -74.0721),
        new("Medellín", "Colombia", 6.2442, -75.5812),
        new("Quito", "Ecuador", -0.1807, -78.4678),
        new("Lima", "Peru", -12.0464, -77.0428),
        new("Santiago", "Chile", -33.4489, -70.6693),
        new("Buenos Aires", "Argentina", -34.6037, -58.3816),
        new("São Paulo", "Brazil", -23.5505, -46.6333),
        new("Rio de Janeiro", "Brazil", -22.9068, -43.1729),
        new("London", "United Kingdom", 51.5074, -0.1278),
        new("Dublin", "Ireland", 53.3498, -6.2603),
        new("Paris", "France", 48.8566, 2.3522),
        new("Madrid", "Spain", 40.4168, -3.7038),
        new("Barcelona", "Spain", 41.3874, 2.1686),
        new("Lisbon", "Portugal", 38.7223, -9.1393),
        new("Amsterdam", "Netherlands", 52.3676, 4.9041),
        new("Berlin", "Germany", 52.5200, 13.4050),
        new("Munich", "Germany", 48.1351, 11.5820),
        new("Zurich", "Switzerland", 47.3769, 8.5417),
        new("Rome", "Italy", 41.9028, 12.4964),
        new("Milan", "Italy", 45.4642, 9.1900),
        new("Stockholm", "Sweden", 59.3293, 18.0686),
        new("Warsaw", "Poland", 52.2297, 21.0122),
        new("Athens", "Greece", 37.9838, 23.7275),
        new("Istanbul", "Türkiye", 41.0082, 28.9784),
        new("Cairo", "Egypt", 30.0444, 31.2357),
        new("Lagos", "Nigeria", 6.5244, 3.3792),
        new("Nairobi", "Kenya", -1.2921, 36.8219),
        new("Cape Town", "South Africa", -33.9249, 18.4241),
        new("Johannesburg", "South Africa", -26.2041, 28.0473),
        new("Dubai", "United Arab Emirates", 25.2048, 55.2708),
        new("Mumbai", "India", 19.0760, 72.8777),
        new("Delhi", "India", 28.6139, 77.2090),
        new("Bangalore", "India", 12.9716, 77.5946),
        new("Singapore", "Singapore", 1.3521, 103.8198),
        new("Bangkok", "Thailand", 13.7563, 100.5018),
        new("Hong Kong", "China", 22.3193, 114.1694),
        new("Shanghai", "China", 31.2304, 121.4737),
        new("Seoul", "South Korea", 37.5665, 126.9780),
        new("Tokyo", "Japan", 35.6762, 139.6503),
        new("Sydney", "Australia", -33.8688, 151.2093),
        new("Melbourne", "Australia", -37.8136, 144.9631),
        new("Auckland", "New Zealand", -36.8509, 174.7645),
    ];

    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public Task<IReadOnlyList<LocationResult>> SearchLocationsAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        string needle = Fold(query);
        IReadOnlyList<LocationResult> matches =
        [
            .. _gazetteer.Where(place =>
                Fold(place.Name).Contains(needle, StringComparison.Ordinal)
                || Fold($"{place.Name}, {place.Country}").Contains(needle, StringComparison.Ordinal)),
        ];

        return Task.FromResult(matches);
    }

    /// <inheritdoc />
    public Task<CurrentConditionsResult> GetCurrentConditionsAsync(string locationName, double latitude, double longitude, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset local = now.ToOffset(OffsetOf(longitude));
        DateOnly localDate = DateOnly.FromDateTime(local.Date);
        DayReading day = DescribeDay(latitude, longitude, localDate);

        // Warmest mid-afternoon, coldest before dawn.
        double hourSwing = 4 * Math.Sin(Math.PI * (local.Hour - 9) / 12);
        double temperature = Math.Round(day.MeanC + hourSwing, 1);
        double feelsLike = Math.Round(temperature - day.WindSpeedKph / 20 + (day.HumidityPercent > 70 ? 1 : 0), 1);

        return Task.FromResult(new CurrentConditionsResult(
            locationName,
            now,
            localDate,
            day.Conditions,
            temperature,
            ToFahrenheit(temperature),
            feelsLike,
            day.HumidityPercent,
            day.WindSpeedKph,
            day.WindDirection,
            day.PrecipitationChancePercent));
    }

    /// <inheritdoc />
    public Task<DailyForecastResult> GetDailyForecastAsync(string locationName, double latitude, double longitude, int days, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, WeatherLimits.MaxForecastDays);

        DateOnly start = DateOnly.FromDateTime(_timeProvider.GetUtcNow().ToOffset(OffsetOf(longitude)).Date);
        List<DailyForecastDay> forecast = new(days);

        for (int offset = 0; offset < days; offset++)
        {
            DateOnly date = start.AddDays(offset);
            DayReading day = DescribeDay(latitude, longitude, date);
            double high = Math.Round(day.MeanC + day.Range / 2, 1);
            double low = Math.Round(day.MeanC - day.Range / 2, 1);

            forecast.Add(new DailyForecastDay(
                date,
                day.Conditions,
                high,
                low,
                ToFahrenheit(high),
                ToFahrenheit(low),
                day.PrecipitationChancePercent,
                day.WindSpeedKph));
        }

        return Task.FromResult(new DailyForecastResult(locationName, forecast));
    }

    private static DayReading DescribeDay(double latitude, double longitude, DateOnly date)
    {
        Random random = new(StableHash(FormattableString.Invariant($"{latitude:F2}|{longitude:F2}|{date:yyyy-MM-dd}")));

        // Latitude sets the climate, the season swings it, the hash adds the day's weather.
        double seasonal = 10 * Math.Cos(2 * Math.PI * (date.DayOfYear - 200) / 365.0) * (latitude >= 0 ? 1 : -1);
        double meanC = 28 - 0.45 * Math.Abs(latitude) + seasonal + random.NextDouble() * 8 - 4;
        double range = 6 + random.NextDouble() * 8;

        int conditionIndex = random.Next(_conditions.Length);
        string conditions = meanC < 0 && conditionIndex >= 4 && conditionIndex <= 6 ? "Snow" : _conditions[conditionIndex];

        int precipitation = conditions switch
        {
            "Sunny" or "Mostly sunny" => random.Next(0, 10),
            "Partly cloudy" or "Cloudy" or "Fog" or "Windy" => random.Next(10, 35),
            "Light rain" or "Showers" or "Snow" => random.Next(55, 85),
            _ => random.Next(70, 95),
        };

        return new DayReading(
            Math.Round(meanC, 1),
            range,
            conditions,
            random.Next(30, 96),
            Math.Round((conditions == "Windy" ? 30 : 5) + random.NextDouble() * 20, 1),
            _windDirections[random.Next(_windDirections.Length)],
            precipitation);
    }

    // Case- and accent-insensitive, so "Sao Paulo" finds "São Paulo".
    private static string Fold(string value)
    {
        StringBuilder folded = new(value.Length);

        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(char.ToLowerInvariant(character));
            }
        }

        return folded.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    // Time zones follow longitude closely enough for a stub to pick the local calendar date.
    private static TimeSpan OffsetOf(double longitude) =>
        TimeSpan.FromHours(Math.Clamp(Math.Round(longitude / 15), -12, 14));

    private static double ToFahrenheit(double celsius) => Math.Round(celsius * 9 / 5 + 32, 1);

    // FNV-1a: HashCode.Combine is salted per process, which would change the weather on every restart.
    private static int StableHash(string value)
    {
        uint hash = 2166136261;

        foreach (char character in value)
        {
            hash = (hash ^ character) * 16777619;
        }

        return unchecked((int)hash);
    }

    private sealed record DayReading(
        double MeanC,
        double Range,
        string Conditions,
        int HumidityPercent,
        double WindSpeedKph,
        string WindDirection,
        int PrecipitationChancePercent);
}

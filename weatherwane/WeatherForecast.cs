using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace weatherwane
{
    public record WeatherInfo(uint Id, string Name, int Icon);

    public record WeatherListing(WeatherInfo Weather, DateTimeOffset Time, DateTimeOffset End);

    public class ZoneInfo
    {
        public uint TerritoryId { get; init; }
        public string Name { get; init; } = string.Empty;
        public (WeatherInfo Weather, byte CumulativeRate)[] Rates { get; init; } = [];
    }

    public class WeatherForecastService
    {
        private const int MillisecondsPerEorzeaHour = 175_000;
        private const int MillisecondsPerEorzeaWeather = 8 * MillisecondsPerEorzeaHour;
        private const int SecondsPerEorzeaHour = MillisecondsPerEorzeaHour / 1000;
        private const int SecondsPerEorzeaDay = 24 * SecondsPerEorzeaHour;

        private readonly Dictionary<uint, ZoneInfo> zones = new();

        public IReadOnlyList<ZoneInfo> AllZones { get; }

        public WeatherForecastService(IDataManager dataManager)
        {
            var weathers = new Dictionary<uint, WeatherInfo>();
            foreach (var w in dataManager.GetExcelSheet<Weather>())
            {
                weathers[w.RowId] = new WeatherInfo(w.RowId, w.Name.ExtractText(), w.Icon);
            }

            var weatherRates = new Dictionary<byte, (WeatherInfo Weather, byte CumulativeRate)[]>();
            foreach (var wr in dataManager.GetExcelSheet<WeatherRate>())
            {
                var rates = new List<(WeatherInfo, byte)>();
                byte cumulative = 0;
                for (var i = 0; i < 8; i++)
                {
                    var rate = wr.Rate[i];
                    if (rate <= 0)
                        continue;
                    var weatherId = wr.Weather[i].RowId;
                    if (!weathers.TryGetValue(weatherId, out var weatherInfo))
                        continue;
                    cumulative += (byte)rate;
                    rates.Add((weatherInfo, cumulative));
                }
                if (rates.Count > 0)
                    weatherRates[(byte)wr.RowId] = rates.ToArray();
            }

            var allZones = new List<ZoneInfo>();
            var seenNames = new HashSet<string>();

            foreach (var tt in dataManager.GetExcelSheet<TerritoryType>())
            {
                if (!tt.PCSearch || tt.WeatherRate.RowId == 0)
                    continue;

                var rateId = (byte)tt.WeatherRate.RowId;
                if (!weatherRates.TryGetValue(rateId, out var rates) || rates.Length <= 1)
                    continue;

                var placeName = tt.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                if (string.IsNullOrEmpty(placeName) || !seenNames.Add(placeName))
                    continue;

                var zone = new ZoneInfo
                {
                    TerritoryId = tt.RowId,
                    Name = placeName,
                    Rates = rates,
                };
                zones[tt.RowId] = zone;
                allZones.Add(zone);
            }

            allZones.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            AllZones = allZones;
        }

        public WeatherListing[] GetForecast(uint territoryId, int count)
        {
            if (!zones.TryGetValue(territoryId, out var zone) || count <= 0)
                return [];

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sync = now - (now % MillisecondsPerEorzeaWeather);

            var result = new WeatherListing[count];
            for (var i = 0; i < count; i++)
            {
                var timestamp = sync + (long)i * MillisecondsPerEorzeaWeather;
                var target = CalculateTarget(timestamp);
                var weather = GetWeather(target, zone.Rates);
                var time = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                var end = DateTimeOffset.FromUnixTimeMilliseconds(timestamp + MillisecondsPerEorzeaWeather);
                result[i] = new WeatherListing(weather, time, end);
            }
            return result;
        }

        public long GetCurrentWeatherPeriod()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now - (now % MillisecondsPerEorzeaWeather);
        }

        private static byte CalculateTarget(long unixMs)
        {
            var seconds = unixMs / 1000;
            var hour = seconds / SecondsPerEorzeaHour;
            var shiftedHour = (uint)(hour + 8 - hour % 8) % 24;
            var day = seconds / SecondsPerEorzeaDay;

            var ret = (uint)day * 100 + shiftedHour;
            ret = (ret << 11) ^ ret;
            ret = (ret >> 8) ^ ret;
            ret %= 100;
            return (byte)ret;
        }

        private static WeatherInfo GetWeather(byte target, (WeatherInfo Weather, byte CumulativeRate)[] rates)
        {
            foreach (var (weather, cumulativeRate) in rates)
            {
                if (cumulativeRate > target)
                    return weather;
            }
            return rates[^1].Weather;
        }
    }
}

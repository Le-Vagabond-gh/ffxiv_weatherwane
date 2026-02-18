using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace weatherwane
{
    public class MainWindow : Window
    {
        private readonly Configuration configuration;
        private readonly WeatherForecastService forecast;
        private long lastWeatherPeriod;

        private readonly List<(string Zone, WeatherListing[] Weathers)> cachedData = new();
        private string[] timeHeaders = [];
        private bool dirty = true;
        private int autoFitFrames;

        private static readonly Vector2 IconSize = new(24, 24);

        private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        private const ImGuiWindowFlags LockFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

        public MainWindow(Configuration configuration, WeatherForecastService forecast)
            : base("WeatherWane", BaseFlags)
        {
            this.configuration = configuration;
            this.forecast = forecast;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 50),
                MaximumSize = new Vector2(2000, 1000),
            };
            UpdateFlags();
        }

        public void UpdateFlags()
        {
            var flags = configuration.LockWindow ? BaseFlags | LockFlags : BaseFlags;
            if (configuration.AutoResize || autoFitFrames > 0)
                flags |= ImGuiWindowFlags.AlwaysAutoResize;
            if (!configuration.ShowBackground)
                flags |= ImGuiWindowFlags.NoBackground;
            Flags = flags;
        }

        public void CheckForWeatherChange()
        {
            var period = forecast.GetCurrentWeatherPeriod();
            if (period != lastWeatherPeriod)
            {
                lastWeatherPeriod = period;
                dirty = true;
            }
        }

        public void SetDirty() => dirty = true;

        public void RequestAutoFit() => autoFitFrames = 3;

        private void RefreshCache()
        {
            if (!dirty)
                return;
            dirty = false;

            var count = configuration.ForecastCount;
            cachedData.Clear();

            foreach (var zone in forecast.AllZones)
            {
                if (!configuration.SelectedZones.Contains(zone.TerritoryId))
                    continue;
                var listings = forecast.GetForecast(zone.TerritoryId, count + 1);
                cachedData.Add((zone.Name, listings));
            }

            timeHeaders = new string[count + 1];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sync = now - (now % 1_400_000);
            for (var i = 0; i < timeHeaders.Length; i++)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(sync + (long)i * 1_400_000).LocalDateTime;
                timeHeaders[i] = time.ToString("HH:mm");
            }
        }

        public override void PreDraw()
        {
            if (autoFitFrames > 0)
            {
                autoFitFrames--;
                UpdateFlags();
            }
        }

        public override void Draw()
        {
            RefreshCache();

            if (configuration.SelectedZones.Count == 0)
            {
                ImGui.TextWrapped("No zones selected. Right-click the \u2601 in the server info bar to open settings.");
                return;
            }

            if (cachedData.Count == 0)
            {
                ImGui.TextWrapped("No matching zones found.");
                return;
            }

            var columnCount = configuration.ForecastCount + 2;

            if (!ImGui.BeginTable("WeatherTable", columnCount,
                    ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Resizable))
                return;

            ImGui.TableSetupScrollFreeze(1, 1);
            ImGui.TableSetupColumn("Zone", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            for (var i = 0; i < timeHeaders.Length; i++)
            {
                var label = i == 0 ? "Now" : timeHeaders[i];
                ImGui.TableSetupColumn(label);
            }
            ImGui.TableHeadersRow();

            foreach (var (zone, weathers) in cachedData)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var textOffset = (IconSize.Y * ImGuiHelpers.GlobalScale - ImGui.GetTextLineHeight()) / 2;
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOffset);
                ImGui.TextUnformatted(zone);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(zone);

                for (var i = 0; i < weathers.Length && i < timeHeaders.Length; i++)
                {
                    ImGui.TableNextColumn();

                    if (i == 0)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.2f, 0.4f, 0.2f, 0.3f)));

                    var listing = weathers[i];
                    var scaledIconSize = IconSize * ImGuiHelpers.GlobalScale;
                    var cellWidth = ImGui.GetContentRegionAvail().X;
                    var iconOffset = (cellWidth - scaledIconSize.X) / 2;
                    if (iconOffset > 0)
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + iconOffset);

                    var icon = Service.TextureProvider.GetFromGameIcon(listing.Weather.Icon);
                    if (icon.TryGetWrap(out var wrap, out _))
                    {
                        ImGui.Image(wrap.Handle, scaledIconSize);
                    }
                    else
                    {
                        ImGui.Dummy(scaledIconSize);
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(listing.Weather.Name);
                }
            }

            ImGui.EndTable();
        }
    }
}

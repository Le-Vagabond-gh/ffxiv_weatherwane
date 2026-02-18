using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace weatherwane
{
    public class ConfigWindow : Window
    {
        private readonly Configuration configuration;
        private readonly WeatherForecastService forecast;
        private readonly MainWindow mainWindow;
        private string filterText = string.Empty;

        public ConfigWindow(Configuration configuration, WeatherForecastService forecast, MainWindow mainWindow)
            : base("WeatherWane Config", ImGuiWindowFlags.None)
        {
            this.configuration = configuration;
            this.forecast = forecast;
            this.mainWindow = mainWindow;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(350, 400),
                MaximumSize = new Vector2(600, 800),
            };
        }

        public override void Draw()
        {
            var changed = false;

            if (ImGui.Button("Open Weather Window"))
                mainWindow.IsOpen = true;
            ImGui.Separator();

            var showBackground = configuration.ShowBackground;
            if (ImGui.Checkbox("Show background", ref showBackground))
            {
                configuration.ShowBackground = showBackground;
                mainWindow.UpdateFlags();
                changed = true;
            }

            var autoResize = configuration.AutoResize;
            if (ImGui.Checkbox("Auto-resize window", ref autoResize))
            {
                configuration.AutoResize = autoResize;
                mainWindow.UpdateFlags();
                changed = true;
            }

            var forecastCount = configuration.ForecastCount;
            if (ImGui.SliderInt("Forecast periods", ref forecastCount, 3, 16))
            {
                configuration.ForecastCount = forecastCount;
                changed = true;
            }

            var lockWindow = configuration.LockWindow;
            if (ImGui.Checkbox("Lock window position", ref lockWindow))
            {
                configuration.LockWindow = lockWindow;
                mainWindow.UpdateFlags();
                changed = true;
            }

            ImGui.Separator();
            ImGui.Text($"Zones ({configuration.SelectedZones.Count} selected):");

            if (ImGui.Button("Select All"))
            {
                foreach (var zone in forecast.AllZones)
                    configuration.SelectedZones.Add(zone.TerritoryId);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Deselect All"))
            {
                configuration.SelectedZones.Clear();
                changed = true;
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##filter", "Filter zones...", ref filterText, 256);

            ImGui.Separator();

            if (ImGui.BeginChild("ZoneList", new Vector2(-1, -1), false))
            {
                foreach (var zone in forecast.AllZones)
                {
                    if (!string.IsNullOrEmpty(filterText) &&
                        !zone.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var selected = configuration.SelectedZones.Contains(zone.TerritoryId);
                    if (ImGui.Checkbox(zone.Name, ref selected))
                    {
                        if (selected)
                            configuration.SelectedZones.Add(zone.TerritoryId);
                        else
                            configuration.SelectedZones.Remove(zone.TerritoryId);
                        changed = true;
                    }
                }
            }
            ImGui.EndChild();

            if (changed)
            {
                configuration.Save();
                mainWindow.SetDirty();
            }
        }
    }
}

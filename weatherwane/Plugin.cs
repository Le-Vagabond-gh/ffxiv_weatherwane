using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace weatherwane
{
    public sealed class Plugin : IDalamudPlugin
    {
        private const string CommandName = "/weatherwane";
        private const string ShortCommandName = "/ww";

        private IDalamudPluginInterface PluginInterface { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("WeatherWane");

        private readonly WeatherForecastService forecast;
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;
        private readonly IDtrBarEntry dtrEntry;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
            this.PluginInterface.Create<Service>();

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            this.forecast = new WeatherForecastService(Service.DataManager);

            this.mainWindow = new MainWindow(this.Configuration, this.forecast);
            this.configWindow = new ConfigWindow(this.Configuration, this.forecast, this.mainWindow);

            WindowSystem.AddWindow(mainWindow);
            WindowSystem.AddWindow(configWindow);

            Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the weather forecast window. Use 'config' to open settings.",
            });
            Service.CommandManager.AddHandler(ShortCommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the weather forecast window. Use '/ww config' to open settings.",
            });

            dtrEntry = Service.DtrBar.Get("WeatherWane");
            dtrEntry.Text = new SeString(new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload(" \u2601 "));
            dtrEntry.Tooltip = new SeString(new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload("Click: toggle weather window\nShift-click: settings"));
            dtrEntry.OnClick = _ =>
            {
                if (ImGui.GetIO().KeyShift)
                    configWindow.IsOpen = !configWindow.IsOpen;
                else
                    mainWindow.IsOpen = !mainWindow.IsOpen;
            };

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenMainUi += OpenMainUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            Service.Framework.Update += OnFrameworkUpdate;
        }

        private void OnCommand(string command, string args)
        {
            var trimmed = args.Trim().ToLowerInvariant();
            if (trimmed == "config")
            {
                configWindow.IsOpen = true;
            }
            else
            {
                mainWindow.IsOpen = !mainWindow.IsOpen;
            }
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (mainWindow.IsOpen)
                mainWindow.CheckForWeatherChange();
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        private void OpenMainUI()
        {
            mainWindow.IsOpen = true;
        }

        private void OpenConfigUI()
        {
            configWindow.IsOpen = true;
        }

        public void Dispose()
        {
            Service.Framework.Update -= OnFrameworkUpdate;
            this.PluginInterface.UiBuilder.Draw -= DrawUI;
            this.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUI;
            this.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUI;
            WindowSystem.RemoveAllWindows();
            Service.CommandManager.RemoveHandler(CommandName);
            Service.CommandManager.RemoveHandler(ShortCommandName);
            dtrEntry.Remove();
        }
    }
}

using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using DotNetLib.Core.Updating;
using DotNetLib.Tray;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Loadout.App.About;
using Loadout.App.Api;
using Loadout.App.Config;
using Loadout.App.Discovery;
using Loadout.App.Integration;
using Loadout.App.Interaction;
using Loadout.App.Launcher;
using Loadout.App.Maintenance;
using Loadout.App.Themes;
using Loadout.App.Tray;
using Loadout.App.Updates;
using Loadout.Core.Confirmations;
using Loadout.Core.Execution;
using Loadout.Core.Startup;
using Loadout.Core.Storage;
using Loadout.Core.Windows;
using Loadout.Windows;
using ThemeMode = Loadout.App.Themes.ThemeMode;

namespace Loadout.App;

internal static class Program
{
    private const string InstanceName = "Loadout";
    private const string UpdatesHttpClient = "updates";

    [STAThread]
    private static void Main(string[] args)
    {
        // First of all, before anything reads or creates the data folder: bring over the data of the old
        // name (Startup Profiles) if this is the first start under the new one.
        MigrateData();

        // Installer maintenance commands (--register-login, --register-protocol, ...) run headlessly and
        // exit; they must not take the mutex or start the API.
        if (MaintenanceCommands.TryRun(args)) return;

        // A `register` / startupprofiles:// invocation is a one-shot confirmation dialog: it must not take
        // the single-instance mutex or start the API, so it can run alongside a full instance.
        if (RegistrationLaunch.IsRegistrationInvocation(args))
        {
            RegistrationApp.Run(args);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Diagnostics.CrashLog.Write("AppDomain", ex);
            else Diagnostics.CrashLog.Write("AppDomain", e.ExceptionObject?.ToString() ?? "unknown");
        };

        var options = LaunchOptions.Parse(args);

        // Single instance per Windows user (DotNetLib.Tray): a later launch, e.g. from the Start Menu while the
        // app sits in the tray, knocks on the running instance, which shows its launcher, then exits.
        using var instance = SingleInstance.TryAcquire(InstanceName);
        if (instance is null)
        {
            SingleInstance.Knock(InstanceName);
            return;
        }

        MigrateAutostart();

        var host = BuildHost(options);
        host.StartAsync().GetAwaiter().GetResult();
        RuntimeInfo.Write(options.Port);

        try
        {
            if (options.Headless)
                host.WaitForShutdownAsync().GetAwaiter().GetResult();
            else
                RunUi(host, instance);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            RuntimeInfo.Delete();
        }
    }

    private static void MigrateData()
    {
        var result = DataMigration.ForCurrentUser().Run();
        if (result.Outcome != DataMigrationOutcome.Failed) return;

        // Keep working from the old folder so the user still sees their profiles; the next start tries again.
        LoadoutPaths.UseLegacyDirectoryForThisRun();
        Diagnostics.CrashLog.Write("DataMigration", $"Could not copy the old data folder: {result.Error}");
    }

    // The old name's "start at login" entry would start a removed exe. Swap it for the new one, keeping
    // it off when the user had switched it off.
    private static void MigrateAutostart()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path is null) return;
            LegacyAutostart.Replace(
                new WindowsStartupRegistration(WindowsStartupRegistration.LegacyValueName),
                new WindowsStartupRegistration(), $"\"{path}\"");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Diagnostics.CrashLog.Write("AutostartMigration", ex);
        }
    }

    private static void RunUi(WebApplication host, SingleInstance instance)
    {
        var profiles = host.Services.GetRequiredService<IProfileStore>();
        var baseStore = host.Services.GetRequiredService<IBaseStore>();
        var library = host.Services.GetRequiredService<Core.Library.LibraryService>();
        var executor = host.Services.GetRequiredService<ProfileExecutor>();
        var config = host.Services.GetRequiredService<IConfigStore>();
        var history = host.Services.GetRequiredService<IHistoryStore>();

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Diagnostics.CrashLog.Write("Dispatcher", e.Exception);
            e.Handled = true; // Keep the app alive; the exception is logged to error.log for diagnosis.
        };
        using var theme = new ThemeManager(app);
        theme.Apply(ThemeManager.Parse(config.GetValue("theme")));

        var updates = host.Services.GetRequiredService<UpdateCoordinator>();
        updates.ExitRequested += () => app.Dispatcher.BeginInvoke(() => app.Shutdown());

        TrayIcon? tray = null;
        void ApplyTheme(ThemeMode mode)
        {
            theme.Apply(mode);
            config.SetValue("theme", mode.ToString());
            tray?.Refresh();
        }

        void OpenConfig()
        {
            var window = new ConfigWindow(host.Services.GetRequiredService<ConfigViewModel>(),
                ThemeManager.Parse(config.GetValue("theme")), ApplyTheme);
            window.Closed += (_, _) => tray?.Refresh();
            window.Show();
        }

        // At most one launcher: reopening it (tray, or launching the app again) brings the open one forward.
        // Only the one shown at login may start a profile by itself after a countdown.
        LauncherWindow? launcher = null;
        void OpenLauncher() => ShowLauncher(atLogin: false);
        void ShowLauncher(bool atLogin)
        {
            if (launcher is not null)
            {
                if (launcher.WindowState == WindowState.Minimized) launcher.WindowState = WindowState.Normal;
                launcher.Activate();
                return;
            }

            launcher = new LauncherWindow(new LauncherViewModel(profiles, executor, history, atLogin: atLogin), OpenConfig);
            launcher.Closed += (_, _) =>
            {
                launcher = null;
                tray?.Refresh();
            };
            launcher.Show();
        }

        // One About window at a time; opening it again brings the open one forward.
        AboutWindow? about = null;
        void OpenAbout()
        {
            if (about is not null)
            {
                if (about.WindowState == WindowState.Minimized) about.WindowState = WindowState.Normal;
                about.Activate();
                return;
            }

            about = new AboutWindow();
            about.Closed += (_, _) => about = null;
            about.Show();
        }

        tray = new TrayIcon(profiles, history, executor, updates, () => ThemeManager.Parse(config.GetValue("theme")),
            ApplyTheme, OpenConfig, OpenLauncher, OpenAbout, app.Shutdown);

        // Show the login selector at startup; the app then lives in the tray. Launching the app again while
        // it runs shows the launcher.
        ShowLauncher(atLogin: true);

        // Look for apps that set themselves to start with Windows since last time, off the UI thread, and offer
        // them once the launcher is out of the way.
        if (!string.Equals(config.GetValue(AppSettingKeys.DiscoverStartupApps), "false", StringComparison.OrdinalIgnoreCase))
            app.Dispatcher.BeginInvoke(async () =>
            {
                var catalog = host.Services.GetRequiredService<IStartupAppCatalog>();
                var discovery = new StartupDiscovery(
                    catalog,
                    new StartupSeenStore(),
                    new StartupTakeover(catalog, profiles, new StartupTakeoverStore(), WindowsStartupRegistration.DefaultValueName),
                    profiles,
                    baseStore,
                    library,
                    WindowsStartupRegistration.DefaultValueName);

                IReadOnlyList<StartupEntry> found;
                try { found = await Task.Run(discovery.FindNew); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    Diagnostics.CrashLog.Write("StartupDiscovery", ex);
                    return;
                }
                if (found.Count == 0) return;

                void Offer() => new NewStartupAppsWindow(new NewStartupAppsViewModel(found, profiles.GetAll(), discovery)).Show();
                if (launcher is null) Offer();
                else launcher.Closed += (_, _) => Offer();
            });
        // Once a day, look for a new version in the background; the tray and Settings show it.
        if (updates.IsAutoCheckDue)
            app.Dispatcher.BeginInvoke(async () =>
            {
                await updates.CheckAsync();
                if (updates.AvailableVersion is { } version)
                    tray?.Notify("Loadout", $"Version {version} is available. Install it from the tray menu or Settings.");
            });

        instance.Listen(() => app.Dispatcher.BeginInvoke(OpenLauncher));

        try
        {
            app.Run();
        }
        finally
        {
            tray.Dispose();
        }
    }

    private static string AppVersion => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    // Starts the downloaded installer detached; the app exits only after this returns true.
    private static bool StartInstaller(string path, string arguments)
    {
        try
        {
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path, arguments) { UseShellExecute = true }) is not null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Diagnostics.CrashLog.Write("Update", ex);
            return false;
        }
    }

    private static WebApplication BuildHost(LaunchOptions options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = options.RawArgs,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddProvider(new Diagnostics.CrashLogLoggerProvider());

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddSingleton<IProfileStore>(_ => new ProfileStore());
        builder.Services.AddSingleton<IBaseStore>(_ => new BaseStore());
        builder.Services.AddSingleton<ILibraryStore>(_ => new LibraryStore());
        builder.Services.AddSingleton<Core.Library.LibraryService>();
        builder.Services.AddSingleton<IConfigStore>(_ => new ConfigStore());
        builder.Services.AddSingleton<IHistoryStore>(_ => new HistoryStore());
        builder.Services.AddSingleton(_ => WindowsRuntime.CreateActionRegistry());
        builder.Services.AddSingleton(sp => new ProfileRunner(
            sp.GetRequiredService<Core.Actions.ActionHandlerRegistry>(), new TaskDelayer()));
        builder.Services.AddSingleton<ProfileExecutor>();
        builder.Services.AddSingleton<ConfirmationService>();
        builder.Services.AddSingleton<Core.Integration.IProfileRegistrar>(sp =>
            new Core.Integration.ProfileRegistrar(
                sp.GetRequiredService<IProfileStore>(),
                sp.GetRequiredService<IBaseStore>(),
                sp.GetRequiredService<Core.Library.LibraryService>()));

        builder.Services.AddSingleton<IUserPrompts, UserPrompts>();
        // A fresh catalog per use, as before. The factory also pins the parameterless constructor.
        builder.Services.AddTransient<IStartupAppCatalog>(_ => new WindowsStartupAppCatalog());
        builder.Services.AddSingleton(sp => new Core.Backup.BackupService(
            sp.GetRequiredService<IProfileStore>(), sp.GetRequiredService<IBaseStore>(),
            sp.GetRequiredService<ILibraryStore>(), sp.GetRequiredService<IConfigStore>(), AppVersion));

        // The update check and installer download share one named HttpClient with the app's user agent.
        builder.Services.AddHttpClient(UpdatesHttpClient, client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"Loadout/{AppVersion}"));
        builder.Services.AddSingleton(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(UpdatesHttpClient);
            return new UpdateCoordinator(
                new GitHubReleaseSource(http, "lukr-99", "startup-profiles",
                    name => name.StartsWith("Loadout-Setup-", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)),
                http, AppVersion, sp.GetRequiredService<IConfigStore>(), StartInstaller,
                Path.Combine(Path.GetTempPath(), "Loadout", "updates"));
        });

        // One per Settings window, so each gets its own debounced save scheduler.
        builder.Services.AddTransient<ISaveScheduler, DebouncedSaveScheduler>();
        builder.Services.AddTransient<ConfigViewModel>();

        var app = builder.Build();
        app.Urls.Add($"http://127.0.0.1:{options.Port}");
        app.MapLoadoutApi();
        return app;
    }
}

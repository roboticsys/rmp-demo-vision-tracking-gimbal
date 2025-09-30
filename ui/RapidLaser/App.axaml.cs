
namespace RapidLaser;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;


    /** BASE **/
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // configure dependency injection (DI)
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // avoid duplicate validations from both Avalonia and MVVM CommunityToolkit 
        DisableAvaloniaDataAnnotationValidation();

        // get the mainviewmodel
        var mainViewModel = _serviceProvider?.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            // attach viewmodel events to the window
            mainViewModel?.SetMainWindow(desktop.MainWindow);

            // dispose service provider on appl shutdown
            desktop.ShutdownRequested += (s, e) => _serviceProvider?.Dispose();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView { DataContext = mainViewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }


    /** METHODS **/
    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISshService, SshService>();
        services.AddSingleton<ICameraService, HttpCameraService>();
        services.AddSingleton<IRmpGrpcService, RmpGrpcService>();
        services.AddSingleton<MainViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins

        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
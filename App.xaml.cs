using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Grimlok.Configuration;
using Grimlok.Services;
#region SYSTEM INITIALIZATION : BLACK STAR PROJECT
// ========================================================================
//   ____  _        _    ____ _  __  ____ _____  _    ____  
//  | __ )| |      / \  / ___| |/ / / ___|_   _|/ \  |  _ \ 
//  |  _ \| |     / _ \| |   | ' /  \___ \ | | / _ \ | |_) |
//  | |_) | |___ / ___ \ |___| . \   ___) || |/ ___ \|  _ < 
//  |____/|_____/_/   \_\____|_|\_\ |____/ |_/_/   \_\_| \_\
//                                                          
//              R E S E A R C H   F A C I L I T Y           
//                                                          
//             [ LOCATION: ICELAND ]            
// ========================================================================
#endregion
namespace Grimlok
{
    public sealed partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static IConfiguration Configuration { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Log.Fatal((Exception)args.ExceptionObject, "AppDomain unhandled exception");
            DispatcherUnhandledException += (s, args) =>
            {
                Log.Fatal(args.Exception, "Dispatcher unhandled exception");
                MessageBox.Show($"An unexpected error occurred: {args.Exception.Message}", "Grimlok Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Log.Fatal(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            // Load configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            // Setup Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/grimlok-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Initializing Grimlok Security Camera Monitoring Subsystem...");

            // Configure DI
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                Log.Information("Grimlok MainWindow launched successfully.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical failure during Grimlok application startup.");
                MessageBox.Show($"Fatal application error: {ex.Message}", "Grimlok System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Bind configuration
            services.Configure<GrimlokOptions>(Configuration.GetSection(GrimlokOptions.SectionName));

            // Enable DataAnnotations validation
            services.AddOptions<GrimlokOptions>()
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            // Logging
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(dispose: true);
            });

            // Core services
            services.AddSingleton<SnapshotStore>();
            services.AddSingleton<MotionEventStore>();
            services.AddSingleton<MotionAnalyzer>();
            services.AddSingleton<YoloObjectDetector>();
            services.AddSingleton<IObjectDetector>(sp => sp.GetRequiredService<YoloObjectDetector>());
            services.AddSingleton<SmtpAlertDispatcher>();
            services.AddSingleton<IAlertDispatcher>(sp => sp.GetRequiredService<SmtpAlertDispatcher>());
            services.AddSingleton<MonitorService>();

            // Main window
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Grimlok Security Camera Monitoring Subsystem shutting down...");
            (ServiceProvider as IDisposable)?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}

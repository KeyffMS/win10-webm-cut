using System.Windows;

namespace Win10WebmCut;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => AppLog.Write("unhandled-ui", args.Exception.ToString());
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Write("unhandled-process", args.ExceptionObject.ToString() ?? "Nieznany wyjątek");
        TaskScheduler.UnobservedTaskException += (_, args) => AppLog.Write("unobserved-task", args.Exception.ToString());
        // Log unexpected failures; do not mark them handled and continue in corrupt state.
        AppLog.Write("startup", AppLog.Summary());
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Write("exit", $"code={e.ApplicationExitCode}");
        base.OnExit(e);
    }
}

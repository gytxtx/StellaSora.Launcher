using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cafe.Launcher.Composition;
using Cafe.Launcher.Testing;
using Cafe.Launcher.Core.Services.Diagnostics;
using Cafe.Launcher.UI.ViewModels;
using Cafe.Launcher.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cafe.Launcher.HeadlessTests;

public sealed class StellaProductHeadlessTests
{
    [AvaloniaFact]
    public void ProductionWindow_HidesCafeEntryAndFirstRunMirrorOption()
    {
        using var directory = TestDirectory.Create(TestDirectoryCleanup.BestEffort);
        var services = new ServiceCollection();
        services.AddLauncherServices(launcherDataRoot: directory.DataRoot);
        services.AddSingleton(_ => new UnifiedLogger(directory.Sub("logs")));
        using var provider = services.BuildServiceProvider();
        var viewModel = provider.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        window.ConfigureViewModel(viewModel);
        try
        {
            window.Show();
            viewModel.Dialogs.ShowSetupWizard();
            Dispatcher.UIThread.RunJobs();

            var resourceEntry = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Command == viewModel.ResourcePanel.OpenResourcePanelCommand);
            Assert.False(resourceEntry.IsVisible);
            Assert.False(resourceEntry.Command!.CanExecute(null));
            var mirrorOption = window.GetVisualDescendants().OfType<RadioButton>()
                .Single(button => button.GroupName == "SetupWizardDownloadSource" && !button.IsVisible);
            Assert.False(mirrorOption.IsVisible);
            Assert.False(viewModel.Settings.CheckForUpdatesCommand.CanExecute(null));
            Assert.Equal("StellaSora Launcher", viewModel.Shell.ProductName);
        }
        finally
        {
            window.Close();
        }
    }
}

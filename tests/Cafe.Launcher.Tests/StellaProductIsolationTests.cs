using System.Globalization;
using Cafe.Launcher.Composition;
using Cafe.Launcher.Core.Constants;
using Cafe.Launcher.Core.Models;
using Cafe.Launcher.Core.Services;
using Cafe.Launcher.Core.Services.Auth;
using Cafe.Launcher.Core.Services.Diagnostics;
using Cafe.Launcher.Testing;
using Cafe.Launcher.UI.Features.ResourcePanel;
using Cafe.Launcher.UI.Features.Settings;
using Cafe.Launcher.UI.Features.SetupWizard;
using Cafe.Launcher.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cafe.Launcher.Tests;

[Collection(nameof(LocalizationServiceTestIsolation))]
public sealed class StellaProductIsolationTests
{
    [Fact]
    public void ProductionComposition_UsesStellaIdentityAndIsolatedDataRoot()
    {
        var services = new ServiceCollection();
        services.AddLauncherServices();
        using var provider = services.BuildServiceProvider();

        Assert.Same(LauncherProfiles.StellaSora, provider.GetRequiredService<LauncherProductProfile>());
        Assert.Same(LauncherProfiles.StellaSoraChina, provider.GetRequiredService<YostarGameProfile>());
        Assert.Equal(LauncherDataRoot.ForCurrentProcess(LauncherProfiles.StellaSora.ProductName).Root,
            provider.GetRequiredService<LauncherDataRoot>().Root);
        Assert.NotEqual(LauncherProfiles.Cafe.ProductName, LauncherProfiles.StellaSora.ProductName);
        Assert.DoesNotContain(LauncherProfiles.Cafe.InstanceName, Program.LockSignalName, StringComparison.Ordinal);
        Assert.Contains(LauncherProfiles.StellaSora.InstanceName, Program.LaunchGameSignalName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledUpdateChannel_DoesNotRequestReleaseMetadata()
    {
        var transport = new StubRemoteHttpTransport(_ => throw new InvalidOperationException("Unexpected request"));
        var service = new LauncherUpdateService(LauncherProfiles.StellaSora, transport, "1.0.0");

        var result = await service.CheckForUpdateAsync(UpdateChannels.Stable);

        Assert.True(result.IsSuccessful);
        Assert.False(result.IsUpdateAvailable);
        Assert.Empty(transport.RequestedUris);
        Assert.Empty(LauncherProfiles.StellaSora.GitHubReleasesPageUrl);
    }

    [Fact]
    public async Task DisabledResourcePanel_RejectsReadsAndWritesBeforeTransport()
    {
        var transport = new StubRemoteHttpTransport();
        var client = new ResourcePanelApiClient(LauncherProfiles.StellaSora, transport);

        Assert.False(client.IsAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStatusAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetConfigAsync("uid"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SaveConfigAsync("uid", "1", "1", "1"));
        Assert.Empty(transport.RequestedUris);
    }

    [Fact]
    public void UnavailableMirror_PreservesBothOfficialCdnHostsEvenWithStaleCafePreference()
    {
        var service = new PatchUrlGroupService(LauncherProfiles.StellaSoraChina, LauncherProfiles.StellaSora);
        var primary = "https://game-launcher-ss-cn.yostar.net";
        var backup = "https://game-launcher-ss-cn-bk.yostar.net";
        var result = service.RewriteCdnConfig(new CdnConfigResponse { PrimaryCdn = primary, BackUpCdn = backup }, PatchUrlGroups.Cafe);

        Assert.Equal(PatchUrlGroups.Official, service.Resolve(PatchUrlGroups.Cafe).Code);
        Assert.Equal(primary, result.PrimaryCdn);
        Assert.Equal(backup, result.BackUpCdn);
    }

    [Fact]
    public async Task ChineseDefaultsAndPersistedCafePreference_NormalizeToOfficialForStella()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        using var directory = TestDirectory.Create();
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            Assert.Equal(PatchUrlGroups.Official, LauncherSettings.CreateDefaults(productProfile: LauncherProfiles.StellaSora).PatchUrlGroup);
            using var service = new LauncherSettingsService(new LauncherDataRoot(directory), productProfile: LauncherProfiles.StellaSora);
            await File.WriteAllTextAsync(service.SettingsPath, "{\"patchUrlGroup\":\"cafe\"}");
            Assert.Equal(PatchUrlGroups.Official, (await service.ReadAsync()).PatchUrlGroup);
            var saved = await service.SaveAsync(new LauncherSettings { PatchUrlGroup = PatchUrlGroups.Cafe });
            Assert.Equal(PatchUrlGroups.Official, saved.PatchUrlGroup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void FirstRunAndSettings_OnlyOfferOfficialSourceAndDisableUpdateCommands()
    {
        TestLocalizationHelper.Initialize();
        var localizer = new LocalizationService();
        using var wizard = new SetupWizardViewModel(localizer,
            new GameInstallationPath(LauncherProfiles.StellaSoraChina),
            new LocalInstallationStateStore(LauncherProfiles.StellaSoraChina),
            new LocalDiagnostics(), new StubFilePickerService(), productProfile: LauncherProfiles.StellaSora);
        var options = new SettingsOptionsViewModel(localizer, new DiskSpaceService(), LauncherProfiles.StellaSora);

        Assert.Equal(PatchUrlGroups.Official, Assert.Single(wizard.DownloadSources).Code);
        Assert.Equal(PatchUrlGroups.Official, Assert.Single(options.PatchUrlGroup).Code);
        Assert.False(options.SupportsLauncherUpdates);
    }

    [Fact]
    public async Task OfficialOnlineSamples_ParseThroughStellaClientWithoutBackgroundUrlCorruption()
    {
        var transport = new StubRemoteHttpTransport(uri => File.ReadAllText(TestRepository.InRepository(
            "docs", "research", "samples", "stella-2026-09-30",
            uri.AbsolutePath.EndsWith("/base/config", StringComparison.Ordinal) ? "base-config.json" : "game-config.json")));
        var profile = LauncherProfiles.StellaSoraChina;
        var client = new LauncherApiClient(profile, transport, new AuthorizationHeaderFactory(profile),
            new PatchUrlGroupService(profile, LauncherProfiles.StellaSora));

        var game = await client.GetGameConfigAsync();
        var basis = await client.GetBaseConfigAsync();

        Assert.Equal("xtlr", game.GameStartExeName);
        Assert.Equal("1.13.0", game.GameLatestVersion);
        Assert.Equal("https://game-launcher-ss-cn.yostar.net/launcher_background_img/ad0b5c35936ecb249b38abd4abb3b344.jpg", basis.LauncherBackgroundImg);
        Assert.All(transport.RequestedUris, uri => Assert.Equal("launcher-api.yostar.net", uri.Host));
    }
}

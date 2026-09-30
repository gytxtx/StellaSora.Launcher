using System.Text.Json;
using System.Text.Json.Nodes;
using Cafe.Launcher.Core.Constants;
using Cafe.Launcher.Core.Models;
using Cafe.Launcher.Core.Services;
using Cafe.Launcher.Core.Services.Auth;
using Cafe.Launcher.Testing;

namespace Cafe.Launcher.Tests;

public sealed class YostarProtocolContractTests
{
    [Theory]
    [InlineData("stella-legacy")]
    [InlineData("ba-empty-params")]
    [InlineData("ba-nonempty-params")]
    public void Config_RoundTrip_PreservesParameterPresenceKeyOrderAndIndependentVc(string fixtureName)
    {
        using var fixture = ReadFixture(fixtureName);
        var expected = fixture.RootElement.GetProperty("config");
        var config = Assert.IsType<GameLauncherConfig>(expected.Deserialize<GameLauncherConfig>());

        Assert.Equal(expected.TryGetProperty("params", out _), config.Params is not null);
        Assert.Equal(expected.GetProperty("vc").GetString(), OfficialHashService.GetGameConfigHash(config));
        Assert.True(OfficialHashService.IsGameConfigHashValid(config));
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(config));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(serialized.RootElement));
    }

    [Theory]
    [InlineData("stella-legacy")]
    [InlineData("ba-empty-params")]
    [InlineData("ba-nonempty-params")]
    public void Authorization_MatchesIndependentReferenceForBothGamesAndBodies(string fixtureName)
    {
        using var fixture = ReadFixture(fixtureName);
        var root = fixture.RootElement;
        var profile = ProtocolProfile(root);
        var clock = new FixtureTimeProvider(root.GetProperty("unixTimeSeconds").GetInt64());
        var factory = new AuthorizationHeaderFactory(profile, clock);
        foreach (var vector in root.GetProperty("authorization").EnumerateArray())
        {
            Assert.Equal(vector.GetProperty("expected").GetString(),
                factory.Create(vector.GetProperty("body").GetString()!, profile.AuthorizationVersion));
        }
    }

    [Theory]
    [InlineData("stella-legacy")]
    [InlineData("ba-empty-params")]
    [InlineData("ba-nonempty-params")]
    public async Task ReadAndCommit_BothGames_PreserveOfficialWireFormat(string fixtureName)
    {
        using var fixture = ReadFixture(fixtureName);
        using var directory = TestDirectory.Create();
        var root = fixture.RootElement;
        var profile = ProtocolProfile(root);
        var store = new LocalInstallationStateStore(profile);
        var gamePath = Path.Combine(directory, profile.Tag);
        await WriteStateAsync(gamePath, root);
        var beforeConfig = await File.ReadAllTextAsync(Path.Combine(gamePath, LauncherPaths.GameConfigFileName));

        var state = await store.ReadAsync(gamePath);

        Assert.Equal(LocalInstallationStateKind.Valid, state.Kind);
        Assert.Equal(beforeConfig, await File.ReadAllTextAsync(Path.Combine(gamePath, LauncherPaths.GameConfigFileName)));
        var config = Assert.IsType<GameLauncherConfig>(state.GameConfig);
        var manifest = Assert.IsType<LocalManifest>(state.Manifest);
        var result = await store.CommitAsync(gamePath, new LocalInstallationStateCommit(
            config.Version!, manifest.Basis!, config.Name!, config.Params ?? [],
            manifest.Files.Select(file => new LocalInstallationFile(file.Path, file.SizeBytes, file.Hash)).ToArray()));

        Assert.Equal(LocalInstallationStateKind.Valid, result.Kind);
        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(gamePath, LauncherPaths.GameConfigFileName)));
        Assert.Equal(JsonSerializer.Serialize(root.GetProperty("config")), JsonSerializer.Serialize(written.RootElement));
        using var writtenManifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(gamePath, LauncherPaths.ManifestFileName)));
        Assert.Equal(JsonSerializer.Serialize(root.GetProperty("manifest")), JsonSerializer.Serialize(writtenManifest.RootElement));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"not-an-array\"")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    public async Task Read_InvalidParameterKind_ReturnsCorrupted(string parameters)
    {
        using var fixture = ReadFixture("ba-empty-params");
        using var directory = TestDirectory.Create();
        var root = fixture.RootElement;
        await WriteStateAsync(directory, root);
        var config = JsonNode.Parse(root.GetProperty("config").GetRawText())!.AsObject();
        config["params"] = JsonNode.Parse(parameters);
        await File.WriteAllTextAsync(Path.Combine(directory, LauncherPaths.GameConfigFileName), config.ToJsonString());

        var state = await new LocalInstallationStateStore(ProtocolProfile(root)).ReadAsync(directory);

        Assert.Equal(LocalInstallationStateKind.Corrupted, state.Kind);
    }

    [Theory]
    [InlineData("stella-legacy")]
    [InlineData("ba-empty-params")]
    public async Task Read_ParameterFieldAddedOrRemovedWithoutNewVc_ReturnsCorrupted(string fixtureName)
    {
        using var fixture = ReadFixture(fixtureName);
        using var directory = TestDirectory.Create();
        var root = fixture.RootElement;
        await WriteStateAsync(directory, root);
        var config = JsonNode.Parse(root.GetProperty("config").GetRawText())!.AsObject();
        if (!config.Remove("params"))
        {
            config.Insert(2, "params", new JsonArray());
        }

        await File.WriteAllTextAsync(Path.Combine(directory, LauncherPaths.GameConfigFileName), config.ToJsonString());

        var state = await new LocalInstallationStateStore(ProtocolProfile(root)).ReadAsync(directory);

        Assert.Equal(LocalInstallationStateKind.Corrupted, state.Kind);
    }

    [Theory]
    [InlineData("config", LauncherPaths.GameConfigFileName)]
    [InlineData("manifest", LauncherPaths.ManifestFileName)]
    public async Task Read_EitherStateDocumentBelongsToAnotherGame_ReturnsCorrupted(string key, string fileName)
    {
        using var ba = ReadFixture("ba-empty-params");
        using var stella = ReadFixture("stella-legacy");
        using var directory = TestDirectory.Create();
        await WriteStateAsync(directory, ba.RootElement);
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), stella.RootElement.GetProperty(key).GetRawText());

        var state = await new LocalInstallationStateStore(LauncherProfiles.BlueArchiveJapan).ReadAsync(directory);

        Assert.Equal(LocalInstallationStateKind.Corrupted, state.Kind);
    }

    [Theory]
    [InlineData("stella-legacy")]
    [InlineData("ba-nonempty-params")]
    public async Task Read_ReorderedConfig_UsesOriginalFieldOrderForVc(string fixtureName)
    {
        using var fixture = ReadFixture(fixtureName);
        using var directory = TestDirectory.Create();
        var root = fixture.RootElement;
        await WriteStateAsync(directory, root);
        var reordered = root.GetProperty("reorderedConfig");
        await File.WriteAllTextAsync(Path.Combine(directory, LauncherPaths.GameConfigFileName), reordered.GetRawText());
        var store = new LocalInstallationStateStore(ProtocolProfile(root));

        Assert.Equal(LocalInstallationStateKind.Valid, (await store.ReadAsync(directory)).Kind);

        // Keeping the canonical-order vc after changing key order must fail, even though
        // deserialization maps to the same property values.
        var tampered = JsonNode.Parse(reordered.GetRawText())!.AsObject();
        tampered["vc"] = root.GetProperty("config").GetProperty("vc").GetString();
        await File.WriteAllTextAsync(Path.Combine(directory, LauncherPaths.GameConfigFileName), tampered.ToJsonString());

        Assert.Equal(LocalInstallationStateKind.Corrupted, (await store.ReadAsync(directory)).Kind);
    }

    [Fact]
    public async Task Commit_LegacyFormatWithLaunchParameters_RejectsBeforeWriting()
    {
        using var fixture = ReadFixture("stella-legacy");
        using var directory = TestDirectory.Create();
        var store = new LocalInstallationStateStore(ProtocolProfile(fixture.RootElement));

        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitAsync(directory,
            new LocalInstallationStateCommit("fixture-version", "fixture-basis", "fixture-host", ["--argument"], [])));

        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
    }

    [Theory]
    [InlineData("{\"params\":null,\"params\":[]}")]
    [InlineData("{\"tag\":\"game\",\"tag\":\"game\"}")]
    [InlineData("{\"extra\":\"unsupported\"}")]
    public void ConfigVc_DuplicateOrUnsupportedFields_AreRejected(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.False(OfficialHashService.IsGameConfigHashValid(document.RootElement));
    }

    private static JsonDocument ReadFixture(string name) => JsonDocument.Parse(File.ReadAllText(
        TestRepository.InRepository("tests", "Cafe.Launcher.Tests", "Fixtures", "YostarProtocol", name + ".json")));

    // Only protocol fields are consumed by these tests. Installation executables, cookies and
    // runtime paths inherited from BA are deliberately not a production Stella game profile.
    private static YostarGameProfile ProtocolProfile(JsonElement fixture) => LauncherProfiles.BlueArchiveJapan with
    {
        Tag = fixture.GetProperty("config").GetProperty("tag").GetString()!,
        ApiBaseUrl = fixture.GetProperty("apiBaseUrl").GetString()!,
        AuthorizationSalt = fixture.GetProperty("salt").GetString()!,
        AuthorizationVersion = fixture.GetProperty("protocolVersion").GetString()!,
        GameConfigIncludesParameters = fixture.GetProperty("includesParameters").GetBoolean()
    };

    private static async Task WriteStateAsync(string gamePath, JsonElement fixture)
    {
        Directory.CreateDirectory(gamePath);
        await File.WriteAllTextAsync(Path.Combine(gamePath, LauncherPaths.GameConfigFileName),
            fixture.GetProperty("config").GetRawText());
        await File.WriteAllTextAsync(Path.Combine(gamePath, LauncherPaths.ManifestFileName),
            fixture.GetProperty("manifest").GetRawText());
    }

    private sealed class FixtureTimeProvider(long unixTimeSeconds) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
    }
}

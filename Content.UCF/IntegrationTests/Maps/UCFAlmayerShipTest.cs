using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.UCF14.Maps;

[TestFixture]
public sealed class UCFAlmayerShipTest
{
    [Test]
    public async Task UscmShipIsTheAlmayer()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var uscm = protos.Index<PlatoonPrototype>("USCM");
            Assert.That(uscm.PossibleShips, Is.EqualTo(new[] { "UCFAlmayer" }));
            Assert.That(protos.Index<GameMapPoolPrototype>("RMCDefaultMapPool").Maps, Does.Contain("UCFAlmayer"));

            var round = server.EntMan.System<AuRoundSystem>();
            server.EntMan.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = uscm;
            Assert.That(round.SetPlanet("CMUPlanetHopesRetreat"), Is.True);
            round.SetGovforShip(string.Empty);
            Assert.That(round.ResolveGovforShip(protos.Index<GamePresetPrototype>("DistressSignal")), Is.EqualTo("UCFAlmayer"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlatoonSpawnRuleStartsOnTheAlmayer()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ticker = entMan.System<GameTicker>();
            var round = entMan.System<AuRoundSystem>();
            var uscm = server.ProtoMan.Index<PlatoonPrototype>("USCM");

            entMan.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = uscm;
            Assert.That(round.SetPlanet("CMUPlanetHopesRetreat"), Is.True);

            var proto = server.ProtoMan.Index<GameMapPrototype>("UCFAlmayer");
            var grids = ticker.LoadGameMap(proto, out _, DeserializationOptions.Default with { InitializeMaps = true });
            Assert.That(grids, Is.Not.Empty);
            foreach (var grid in grids)
                entMan.EnsureComponent<ShipFactionComponent>(grid).Faction = "govfor";

            Assert.That(ticker.StartGameRule("PlatoonSpawn"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlmayerHasHijackDestinationsAndCargoElevator()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var ticker = entMan.System<GameTicker>();

            var proto = server.ProtoMan.Index<GameMapPrototype>("UCFAlmayer");
            ticker.LoadGameMap(proto, out _, DeserializationOptions.Default with { InitializeMaps = true });

            var hijack = entMan.EntityQuery<DropshipHijackDestinationComponent>().Count();
            var elevators = entMan.EntityQuery<MetaDataComponent>()
                .Count(meta => meta.EntityPrototype?.ID == "CMCargoElevator");

            Assert.Multiple(() =>
            {
                Assert.That(hijack, Is.EqualTo(4));
                Assert.That(elevators, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }
}

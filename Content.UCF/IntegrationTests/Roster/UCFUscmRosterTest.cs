#pragma warning disable RA0002 // the test reads the station job table

using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.UCF14.Roster;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.UCF14.Roster;

[TestFixture]
public sealed class UCFUscmRosterTest
{
    [Test]
    public async Task UscmRosterScalesSquadsAndSpawnsMarines()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ticker = entMan.System<GameTicker>();
            var round = entMan.System<AuRoundSystem>();
            var stationSystem = entMan.System<StationSystem>();
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var uscm = server.ProtoMan.Index<PlatoonPrototype>("USCM");

            entMan.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = uscm;
            Assert.That(round.SetPlanet("CMUPlanetHopesRetreat"), Is.True);

            var proto = server.ProtoMan.Index<GameMapPrototype>("UCFAlmayer");
            var grids = ticker.LoadGameMap(proto, out _, DeserializationOptions.Default with { InitializeMaps = true });
            foreach (var grid in grids)
            {
                entMan.EnsureComponent<ShipFactionComponent>(grid).Faction = "govfor";
            }

            Assert.That(ticker.StartGameRule("AddGovfor"), Is.True);
            Assert.That(ticker.StartGameRule("UCFUscmRoster"), Is.True);

            var ship = grids.First(grid => entMan.HasComponent<ShipFactionComponent>(grid));
            var station = stationSystem.GetOwningStation(ship);
            Assert.That(station, Is.Not.Null);
            var jobs = entMan.GetComponent<StationJobsComponent>(station!.Value);

            var roster = entMan.EntityQuery<UCFUscmRosterRuleComponent>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(roster.Active, Is.True);
                Assert.That(roster.Squads, Has.Count.EqualTo(4));
                Assert.That(jobs.SetupAvailableJobs.Keys.Any(job => job.Id.StartsWith("AU14JobGOVFOR")), Is.False);
                Assert.That(jobs.SetupAvailableJobs.ContainsKey("CMRifleman"), Is.True);
                Assert.That(jobs.SetupAvailableJobs.ContainsKey("CMMilitaryWarden"), Is.False);
                Assert.That(jobs.SetupAvailableJobs["CMHospitalCorpsman"][0], Is.EqualTo(12), "three medics in each of four squads at low pop");
            });

            var spawned = new EntityUid[4];
            for (var i = 0; i < spawned.Length; i++)
            {
                var ev = new PlayerSpawningEvent("CMRifleman", HumanoidCharacterProfile.DefaultWithSpecies(), station);
                entMan.EventBus.RaiseEvent(EventSource.Local, ev);
                Assert.That(ev.SpawnResult, Is.Not.Null);
                spawned[i] = ev.SpawnResult!.Value;
            }

            var squads = spawned
                .Select(marine => entMan.GetComponent<SquadMemberComponent>(marine).Squad)
                .ToHashSet();
            Assert.That(squads, Has.Count.EqualTo(4), "riflemen spread across the four squads");
        });

        await pair.CleanReturnAsync();
    }
}

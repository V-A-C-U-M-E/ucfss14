using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Doors;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.UCF14.Maps;

[TestFixture]
public sealed class UCFAlmayerLoadTest
{
    [TestCase("Almayer")]
    [TestCase("UCFAlmayer")]
    public async Task AlmayerLoadsWithGridAndAreas(string mapProto)
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var report = new System.Collections.Generic.List<string>();
        var tileCount = 0;
        var areaCount = 0;
        var lateJoinPoints = 0;
        var jobPoints = 0;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var ticker = entMan.System<GameTicker>();
            var mapSystem = entMan.System<SharedMapSystem>();

            Assert.That(server.ProtoMan.TryIndex<GameMapPrototype>(mapProto, out var proto), Is.True);
            var options = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(proto!, out var mapId, options);

            var spawnQuery = entMan.EntityQueryEnumerator<SpawnPointComponent>();
            while (spawnQuery.MoveNext(out _, out var spawn))
            {
                if (spawn.SpawnType == SpawnPointType.LateJoin)
                    lateJoinPoints++;
                else if (spawn.SpawnType == SpawnPointType.Job)
                    jobPoints++;
            }

            var grids = mapSystem.GetAllGrids(mapId).ToList();
            report.Add($"{mapProto}: grids={grids.Count} lateJoinPoints={lateJoinPoints} jobPoints={jobPoints}");
            foreach (var grid in grids)
            {
                var tiles = mapSystem.GetAllTiles(grid.Owner, grid.Comp).Count();
                var areas = entMan.TryGetComponent(grid.Owner, out AreaGridComponent? areaGrid) ? areaGrid.Areas.Count : 0;
                tileCount = System.Math.Max(tileCount, tiles);
                areaCount = System.Math.Max(areaCount, areas);
                report.Add($"{mapProto}: grid {grid.Owner} name={entMan.GetComponent<MetaDataComponent>(grid.Owner).EntityName} tiles={tiles} areas={areas} entities={entMan.EntityCount}");
            }
        });

        TestContext.Out.WriteLine(string.Join(System.Environment.NewLine, report));
        if (mapProto == "UCFAlmayer")
        {
            Assert.That(tileCount, Is.GreaterThan(20000));
            Assert.That(areaCount, Is.GreaterThan(10000));
            Assert.That(lateJoinPoints, Is.GreaterThan(0));
            Assert.That(jobPoints, Is.GreaterThan(0));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlmayerDoorButtonsAreLinked()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var unlinked = new System.Collections.Generic.List<string>();
        var buttons = 0;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var ticker = entMan.System<GameTicker>();
            var mapSystem = entMan.System<SharedMapSystem>();

            Assert.That(server.ProtoMan.TryIndex<GameMapPrototype>("UCFAlmayer", out var proto), Is.True);
            ticker.LoadGameMap(proto!, out var mapId, DeserializationOptions.Default with { InitializeMaps = true });

            var shipGrid = mapSystem.GetAllGrids(mapId).OrderByDescending(g => mapSystem.GetAllTiles(g.Owner, g.Comp).Count()).First().Owner;
            var doorIds = new System.Collections.Generic.HashSet<string>();
            var podDoors = entMan.EntityQueryEnumerator<RMCPodDoorComponent, TransformComponent, MetaDataComponent>();
            while (podDoors.MoveNext(out _, out var pod, out var xform, out var meta))
            {
                if (xform.GridUid == shipGrid)
                    doorIds.Add(pod.Id ?? meta.EntityName);
            }

            var query = entMan.EntityQueryEnumerator<RMCDoorButtonComponent, TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out _, out var button, out var xform, out var meta))
            {
                if (xform.GridUid != shipGrid)
                    continue;

                buttons++;
                var id = button.Id ?? meta.EntityName;
                if (!doorIds.Contains(id))
                    unlinked.Add(id);
            }
        });

        Assert.That(buttons, Is.GreaterThan(100));
        Assert.That(unlinked, Is.Empty);

        await pair.CleanReturnAsync();
    }
}

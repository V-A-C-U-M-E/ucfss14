using RosterSpawners = Content.Server._RMC14.Rules.Spawners;
using Content.Server._RMC14.Stations;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.HyperSleep;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.UCF14.Roster;

public sealed class UCFUscmRosterSystem : GameRuleSystem<UCFUscmRosterRuleComponent>
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoon = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private RMCStationJobsSystem _rmcStationJobs = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<HyperSleepChamberComponent> _hyperSleepQuery;

    public override void Initialize()
    {
        base.Initialize();

        _hyperSleepQuery = GetEntityQuery<HyperSleepChamberComponent>();

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning,
            before: [typeof(ArrivalsSystem), typeof(SpawnPointSystem)]);
    }

    protected override void Started(EntityUid uid, UCFUscmRosterRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        comp.Active = false;
        comp.Squads.Clear();
        comp.Jobs.Clear();

        if (_platoon.SelectedGovforPlatoon is not { ShipJobsOnly: true })
            return;

        if (!TryGetShipStation(out var station, out var stationJobs))
            return;

        comp.Active = true;

        foreach (var id in comp.SquadIds)
        {
            comp.Squads[id] = Spawn(id, MapCoordinates.Nullspace);
        }

        foreach (var id in comp.ExtraSquadIds)
        {
            Spawn(id, MapCoordinates.Nullspace);
        }

        var setup = stationJobs.SetupAvailableJobs;
        foreach (var job in setup.Keys)
        {
            comp.Jobs.Add(job);
        }

        ApplyScaling(comp, station, stationJobs);
    }

    private bool TryGetShipStation(out EntityUid station, out StationJobsComponent stationJobs)
    {
        station = default;
        stationJobs = default!;

        var query = AllEntityQuery<ShipFactionComponent>();
        while (query.MoveNext(out var ship, out var faction))
        {
            if (!string.Equals(faction.Faction, "govfor", StringComparison.OrdinalIgnoreCase))
                continue;

            if (_station.GetOwningStation(ship) is not { } owner ||
                !TryComp(owner, out StationJobsComponent? jobs))
            {
                continue;
            }

            station = owner;
            stationJobs = jobs;
            return true;
        }

        return false;
    }

    private void ApplyScaling(UCFUscmRosterRuleComponent comp, EntityUid station, StationJobsComponent stationJobs)
    {
        if (!comp.Scaling.TryGet(out var scaling, _prototypes, _compFactory))
            return;

        var marines = Math.Max(1, _players.PlayerCount);
        var setup = stationJobs.SetupAvailableJobs;
        foreach (var (job, entry) in scaling.Jobs)
        {
            var slots = _rmcStationJobs.GetSlots(marines, entry.Factor, entry.C, entry.Min, entry.Max);
            if (entry.Squad)
            {
                foreach (var squad in comp.Squads.Values)
                {
                    if (TryComp(squad, out SquadTeamComponent? team))
                        _squad.SetSquadMaxRole((squad, team), job, slots);
                }

                slots *= comp.Squads.Count;
            }

            if (setup.TryGetValue(job, out var available))
            {
                for (var i = 0; i < available.Length; i++)
                {
                    available[i] = slots;
                }
            }

            _stationJobs.TrySetJobSlot(station, job, slots, stationJobs: stationJobs);
        }
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        if (ev.Job is not { } jobId ||
            !_prototypes.TryIndex(jobId, out var job) ||
            !job.IsCM)
        {
            return;
        }

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {
            if (!comp.Active || !comp.Jobs.Contains(jobId))
                continue;

            if (GetSpawner(comp, job) is not { } spawnerInfo)
                return;

            var (spawner, squad) = spawnerInfo;
            if (_hyperSleepQuery.TryComp(spawner, out var hyperSleep) &&
                _containers.TryGetContainer(spawner, hyperSleep.ContainerId, out var container))
            {
                ev.SpawnResult = _stationSpawning.SpawnPlayerMob(spawner.ToCoordinates(), ev.Job, ev.HumanoidCharacterProfile, ev.Station);
                _containers.Insert(ev.SpawnResult.Value, container);
            }
            else
            {
                var coordinates = _transform.GetMoverCoordinates(spawner);
                ev.SpawnResult = _stationSpawning.SpawnPlayerMob(coordinates, ev.Job, ev.HumanoidCharacterProfile, ev.Station);
            }

            if (squad != null)
                _squad.AssignSquad(ev.SpawnResult.Value, squad.Value, jobId);

            return;
        }
    }

    private (EntityUid Spawner, EntityUid? Squad)? GetSpawner(UCFUscmRosterRuleComponent comp, JobPrototype job)
    {
        var spawners = GetSpawners();
        EntityUid? squad = null;

        if (job.HasSquad)
        {
            var (squadId, squadEnt) = NextSquad(comp, job.ID);
            squad = squadEnt;

            if (spawners.Squad.TryGetValue(squadId, out var jobSpawners) &&
                jobSpawners.TryGetValue(job.ID, out var picked))
            {
                return (_random.Pick(picked), squadEnt);
            }

            if (spawners.SquadAny.TryGetValue(squadId, out var any))
                return (_random.Pick(any), squadEnt);

            if (spawners.SquadFull.TryGetValue(squadId, out jobSpawners) &&
                jobSpawners.TryGetValue(job.ID, out picked))
            {
                return (_random.Pick(picked), squadEnt);
            }

            if (spawners.SquadAnyFull.TryGetValue(squadId, out any))
                return (_random.Pick(any), squadEnt);
        }

        if (spawners.NonSquad.TryGetValue(job.ID, out var points))
            return (_random.Pick(points), squad);

        if (spawners.NonSquadFull.TryGetValue(job.ID, out points))
            return (_random.Pick(points), squad);

        var pointsQuery = EntityQueryEnumerator<SpawnPointComponent>();
        var anyJob = new List<EntityUid>();
        var late = new List<EntityUid>();
        while (pointsQuery.MoveNext(out var uid, out var point))
        {
            if (point.SpawnType == SpawnPointType.Job)
                anyJob.Add(uid);
            else if (point.SpawnType == SpawnPointType.LateJoin)
                late.Add(uid);
        }

        if (anyJob.Count > 0)
            return (_random.Pick(anyJob), squad);

        return late.Count > 0 ? (_random.Pick(late), squad) : null;
    }

    private (EntProtoId Id, EntityUid Squad) NextSquad(UCFUscmRosterRuleComponent comp, ProtoId<JobPrototype> job)
    {
        var squads = new List<(EntProtoId Id, EntityUid Squad, int Players)>();
        foreach (var (id, squad) in comp.Squads)
        {
            var players = 0;
            var full = false;
            if (TryComp(squad, out SquadTeamComponent? team))
            {
                team.Roles.TryGetValue(job, out players);
                full = team.MaxRoles.TryGetValue(job, out var max) && players >= max;
            }

            if (!full)
                squads.Add((id, squad, players));
        }

        if (squads.Count == 0)
        {
            foreach (var (id, squad) in comp.Squads)
            {
                squads.Add((id, squad, 0));
            }
        }

        _random.Shuffle(squads);
        squads.Sort((a, b) => a.Players.CompareTo(b.Players));

        var chosen = squads[0];
        return (chosen.Id, chosen.Squad);
    }

    private RosterSpawners GetSpawners()
    {
        var spawners = new RosterSpawners();
        var squadQuery = EntityQueryEnumerator<SquadSpawnerComponent>();
        while (squadQuery.MoveNext(out var uid, out var spawner))
        {
            var target = uid;
            var full = false;
            var found = false;

            if (_hyperSleepQuery.TryComp(uid, out var chamber))
            {
                found = true;
                full = IsFull(uid, chamber);
            }
            else
            {
                foreach (var cardinal in _rmcMap.CardinalDirections)
                {
                    var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(uid, cardinal);
                    while (anchored.MoveNext(out var anchoredId))
                    {
                        if (!_hyperSleepQuery.TryComp(anchoredId, out chamber))
                            continue;

                        target = anchoredId;
                        full = IsFull(anchoredId, chamber);
                        found = true;
                        break;
                    }

                    if (found)
                        break;
                }
            }

            if (spawner.Role == null)
            {
                (full ? spawners.SquadAnyFull : spawners.SquadAny).GetOrNew(spawner.Squad).Add(target);
            }
            else
            {
                (full ? spawners.SquadFull : spawners.Squad).GetOrNew(spawner.Squad).GetOrNew(spawner.Role.Value).Add(target);
            }
        }

        var nonSquadQuery = EntityQueryEnumerator<SpawnPointComponent>();
        while (nonSquadQuery.MoveNext(out var uid, out var point))
        {
            if (point.Job == null)
                continue;

            var full = _hyperSleepQuery.TryComp(uid, out var chamber) && IsFull(uid, chamber);
            (full ? spawners.NonSquadFull : spawners.NonSquad).GetOrNew(point.Job.Value).Add(uid);
        }

        return spawners;
    }

    private bool IsFull(EntityUid uid, HyperSleepChamberComponent chamber)
    {
        return _containers.TryGetContainer(uid, chamber.ContainerId, out var container) && container.Count > 0;
    }
}

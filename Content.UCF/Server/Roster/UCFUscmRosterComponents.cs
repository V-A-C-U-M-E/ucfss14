using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.UCF14.Roster;

[RegisterComponent]
public sealed partial class UCFUscmRosterRuleComponent : Component
{
    [DataField]
    public List<EntProtoId> SquadIds = new() { "SquadAlpha", "SquadBravo", "SquadCharlie", "SquadDelta" };

    [DataField]
    public List<EntProtoId> ExtraSquadIds = new() { "SquadIntel" };

    [DataField]
    public EntProtoId<UCFJobSlotScalingComponent> Scaling = "UCFJobSlotScaling";

    public bool Active;

    public readonly Dictionary<EntProtoId, EntityUid> Squads = new();

    public readonly HashSet<ProtoId<JobPrototype>> Jobs = new();
}

[RegisterComponent]
public sealed partial class UCFJobSlotScalingComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, UCFJobSlotScaling> Jobs = new();
}

[DataRecord]
public readonly partial record struct UCFJobSlotScaling(int Factor, int C, int Min, int Max, bool Squad);

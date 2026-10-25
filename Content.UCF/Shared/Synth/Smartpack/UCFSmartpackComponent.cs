using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.UCF14.Synth.Smartpack;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(UCFSmartpackSystem))]
public sealed partial class UCFSmartpackComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Charge = 200;

    [DataField]
    public int MaxCharge = 200;

    [DataField]
    public int ProtectiveCost = 150;

    [DataField]
    public int ImmobileCost = 20;

    [DataField]
    public int RepairCost = 100;

    [DataField]
    public TimeSpan ProtectiveDuration = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan ImmobileLockout = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan RepairDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan RepairCooldown = TimeSpan.FromSeconds(18);

    [DataField]
    public TimeSpan ApcChargeDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float RepairAmount = 50;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> ProtectiveModifier = "UCFSmartpackProtective";

    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField, AutoNetworkedField]
    public bool Protective;

    [DataField, AutoNetworkedField]
    public bool Immobile;

    [DataField, AutoNetworkedField]
    public bool Repairing;

    public TimeSpan? ProtectiveEnd;

    public TimeSpan? RepairEnd;

    public TimeSpan FormLockoutEnd;

    public ProtoId<DamageModifierSetPrototype>? SavedModifier;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class UCFSmartpackProtectedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class UCFSmartpackImmobileComponent : Component;

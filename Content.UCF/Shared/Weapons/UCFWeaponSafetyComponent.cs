using Robust.Shared.GameStates;

namespace Content.Shared.UCF14.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(UCFWeaponSafetySystem))]
public sealed partial class UCFWeaponSafetyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool On;
}

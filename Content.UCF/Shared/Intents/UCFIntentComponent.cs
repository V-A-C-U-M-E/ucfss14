using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.UCF14.Intents;

[Serializable, NetSerializable]
public enum UCFIntent : byte
{
    Help,
    Disarm,
    Grab,
    Harm
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(UCFIntentSystem))]
public sealed partial class UCFIntentComponent : Component
{
    [DataField, AutoNetworkedField]
    public UCFIntent Intent = UCFIntent.Help;
}

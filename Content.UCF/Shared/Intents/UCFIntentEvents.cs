using Content.Shared.Alert;
using Robust.Shared.Serialization;

namespace Content.Shared.UCF14.Intents;

public sealed partial class UCFIntentClickedAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed class UCFGrabAttemptEvent : EntityEventArgs
{
    public readonly NetEntity Target;

    public UCFGrabAttemptEvent(NetEntity target)
    {
        Target = target;
    }
}

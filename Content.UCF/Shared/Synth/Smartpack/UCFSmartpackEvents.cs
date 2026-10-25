using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.UCF14.Synth.Smartpack;

public sealed partial class UCFSmartpackProtectiveEvent : InstantActionEvent;

public sealed partial class UCFSmartpackImmobileEvent : InstantActionEvent;

public sealed partial class UCFSmartpackRepairEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class UCFSmartpackRepairDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class UCFSmartpackChargeDoAfterEvent : SimpleDoAfterEvent;

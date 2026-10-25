using Robust.Shared.Input;

namespace Content.Shared.UCF14.Input;

[KeyFunctions]
public sealed class UCFKeyFunctions
{
    public static readonly BoundKeyFunction ToggleWeaponSafety = "UCFToggleWeaponSafety";
    public static readonly BoundKeyFunction IntentHelp = "UCFIntentHelp";
    public static readonly BoundKeyFunction IntentDisarm = "UCFIntentDisarm";
    public static readonly BoundKeyFunction IntentGrab = "UCFIntentGrab";
    public static readonly BoundKeyFunction IntentHarm = "UCFIntentHarm";
    public static readonly BoundKeyFunction IntentCycle = "UCFIntentCycle";
    public static readonly BoundKeyFunction TileContents = "UCFTileContents";
}

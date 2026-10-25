using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> UCFAccelerationModifier =
        CVarDef.Create("ucf.movement.acceleration_modifier", 1f, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);
}

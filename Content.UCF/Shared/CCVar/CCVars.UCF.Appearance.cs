using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> UCFRegulationAppearance =
        CVarDef.Create("ucf.appearance.regulation", false, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);
}

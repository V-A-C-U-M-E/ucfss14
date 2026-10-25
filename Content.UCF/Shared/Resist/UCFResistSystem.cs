using Content.Shared.Alert;
using Content.Shared.UCF14.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.UCF14.Resist;

public sealed class UCFResistSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype>[] ResistAlerts =
    [
        "Fire",
        "Handcuffed",
        "Buckled",
        "Ensnared",
        "Pulled"
    ];

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(UCFKeyFunctions.Resist,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (_net.IsServer && session?.AttachedEntity is { } user)
                        Resist(user);
                }, handle: false))
            .Register<UCFResistSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<UCFResistSystem>();
    }

    public bool Resist(EntityUid user)
    {
        foreach (var id in ResistAlerts)
        {
            if (!_alerts.IsShowingAlert(user, id) || !_alerts.TryGet(id, out var alert))
                continue;

            if (_alerts.ActivateAlert(user, alert))
                return true;
        }

        return false;
    }
}

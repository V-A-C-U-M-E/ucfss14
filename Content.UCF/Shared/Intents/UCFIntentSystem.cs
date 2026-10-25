using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.UCF14.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.UCF14.Intents;

public sealed class UCFIntentSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> IntentAlert = "UCFIntent";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CombatModeComponent, PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<UCFIntentComponent, UCFIntentClickedAlertEvent>(OnAlertClicked);
        SubscribeAllEvent<UCFGrabAttemptEvent>(OnGrabAttempt);

        CommandBinds.Builder
            .Bind(UCFKeyFunctions.IntentHelp, Select(UCFIntent.Help))
            .Bind(UCFKeyFunctions.IntentDisarm, Select(UCFIntent.Disarm))
            .Bind(UCFKeyFunctions.IntentGrab, Select(UCFIntent.Grab))
            .Bind(UCFKeyFunctions.IntentHarm, Select(UCFIntent.Harm))
            .Bind(UCFKeyFunctions.IntentCycle,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (session?.AttachedEntity is { } user)
                        Cycle(user);
                }, handle: false))
            .Register<UCFIntentSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<UCFIntentSystem>();
    }

    private InputCmdHandler Select(UCFIntent intent)
    {
        return InputCmdHandler.FromDelegate(session =>
        {
            if (session?.AttachedEntity is { } user)
                SetIntent(user, intent);
        }, handle: false);
    }

    public UCFIntent GetIntent(EntityUid uid)
    {
        return TryComp(uid, out UCFIntentComponent? comp) ? comp.Intent : UCFIntent.Harm;
    }

    public void SetIntent(EntityUid uid, UCFIntent intent)
    {
        if (!TryComp(uid, out UCFIntentComponent? comp))
            return;

        if (comp.Intent != intent)
        {
            comp.Intent = intent;
            Dirty(uid, comp);
        }

        if (HasComp<CombatModeComponent>(uid))
            _combatMode.SetInCombatMode(uid, intent != UCFIntent.Help);

        _alerts.ShowAlert(uid, IntentAlert, (short) intent);
    }

    public void Cycle(EntityUid uid)
    {
        var next = (UCFIntent) (((int) GetIntent(uid) + 1) % 4);
        SetIntent(uid, next);
    }

    private void OnAttached(Entity<CombatModeComponent> ent, ref PlayerAttachedEvent args)
    {
        if (_net.IsClient)
            return;

        EnsureComp<UCFIntentComponent>(ent);

        SetIntent(ent, UCFIntent.Help);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<UCFIntentComponent, CombatModeComponent>();
        while (query.MoveNext(out var uid, out var intent, out var combat))
        {
            if (combat.IsInCombatMode && intent.Intent == UCFIntent.Help)
                SetIntent(uid, UCFIntent.Harm);
            else if (!combat.IsInCombatMode && intent.Intent != UCFIntent.Help)
                SetIntent(uid, UCFIntent.Help);
        }
    }

    private void OnAlertClicked(Entity<UCFIntentComponent> ent, ref UCFIntentClickedAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Cycle(ent);
    }

    private void OnGrabAttempt(UCFGrabAttemptEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var target = GetEntity(msg.Target);
        if (target == user || !Exists(target) || GetIntent(user) != UCFIntent.Grab)
            return;

        if (!_interaction.InRangeUnobstructed(user, target))
            return;

        _pulling.TryStartPull(user, target);
    }
}

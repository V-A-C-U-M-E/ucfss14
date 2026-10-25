using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.UCF14.Input;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Input.Binding;

namespace Content.Shared.UCF14.Weapons;

public sealed class UCFWeaponSafetySystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<UCFWeaponSafetyComponent, AttemptShootEvent>(OnAttemptShoot);

        CommandBinds.Builder
            .Bind(UCFKeyFunctions.ToggleWeaponSafety,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (session?.AttachedEntity is { } user)
                        Toggle(user);
                }, handle: false))
            .Register<UCFWeaponSafetySystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<UCFWeaponSafetySystem>();
    }

    private void OnAttemptShoot(Entity<UCFWeaponSafetyComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.On)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("ucf-weapon-safety-blocked");
    }

    private void Toggle(EntityUid user)
    {
        if (!_hands.TryGetActiveItem(user, out var held) || !HasComp<GunComponent>(held))
            return;

        var safety = EnsureComp<UCFWeaponSafetyComponent>(held.Value);
        safety.On = !safety.On;
        Dirty(held.Value, safety);

        var message = safety.On ? "ucf-weapon-safety-on" : "ucf-weapon-safety-off";
        _popup.PopupClient(Loc.GetString(message, ("gun", held.Value)), held.Value, user);
    }
}

using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Throwing;
using Content.Shared.UCF14.Intents;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.UCF14.Synth.Smartpack;

public sealed class UCFSmartpackSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private const string BackSlot = "back";

    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private UCFIntentSystem _intents = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<UCFSmartpackComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<UCFSmartpackComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<UCFSmartpackComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<UCFSmartpackComponent, UCFSmartpackProtectiveEvent>(OnProtective);
        SubscribeLocalEvent<UCFSmartpackComponent, UCFSmartpackImmobileEvent>(OnImmobile);
        SubscribeLocalEvent<UCFSmartpackComponent, UCFSmartpackRepairEvent>(OnRepair);
        SubscribeLocalEvent<UCFSmartpackComponent, UCFSmartpackRepairDoAfterEvent>(OnRepairDoAfter);

        SubscribeLocalEvent<UCFSmartpackProtectedComponent, AttackAttemptEvent>(OnProtectedAttack);
        SubscribeLocalEvent<UCFSmartpackProtectedComponent, ShotAttemptedEvent>(OnProtectedShot);
        SubscribeLocalEvent<UCFSmartpackProtectedComponent, ThrowAttemptEvent>(OnProtectedThrow);

        SubscribeLocalEvent<UCFSmartpackImmobileComponent, UpdateCanMoveEvent>(OnImmobileCanMove);
        SubscribeLocalEvent<UCFSmartpackImmobileComponent, PullAttemptEvent>(OnImmobilePull);

        SubscribeLocalEvent<SynthComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<UCFSmartpackComponent, UCFSmartpackChargeDoAfterEvent>(OnApcCharged);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<UCFSmartpackComponent>();
        while (query.MoveNext(out var uid, out var pack))
        {
            if (pack.ProtectiveEnd is { } protectiveEnd && now >= protectiveEnd)
                EndProtective((uid, pack), true);

            if (pack.RepairEnd is { } repairEnd && now >= repairEnd)
            {
                pack.RepairEnd = null;
                pack.Repairing = false;
                Dirty(uid, pack);
            }
        }
    }

    private void OnExamined(Entity<UCFSmartpackComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("ucf-smartpack-charge", ("charge", ent.Comp.Charge), ("max", ent.Comp.MaxCharge)));
    }

    private void OnEquipped(Entity<UCFSmartpackComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != BackSlot)
            return;

        ent.Comp.Wearer = args.EquipTarget;
        Dirty(ent);

        if (_net.IsServer && !HasComp<SynthComponent>(args.EquipTarget))
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-unauthorized", ("pack", ent.Owner)), args.EquipTarget, args.EquipTarget, PopupType.MediumCaution);
    }

    private void OnUnequipped(Entity<UCFSmartpackComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != BackSlot)
            return;

        if (_net.IsServer)
        {
            EndProtective(ent, false);
            EndImmobile(ent);
            ent.Comp.Repairing = false;
            ent.Comp.RepairEnd = null;
        }

        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private bool CanUse(Entity<UCFSmartpackComponent> ent, EntityUid user)
    {
        if (ent.Comp.Wearer != user || !HasComp<SynthComponent>(user) || _mobState.IsDead(user))
        {
            Say(ent, user, "ucf-smartpack-unavailable");
            return false;
        }

        return true;
    }

    private bool HasCharge(Entity<UCFSmartpackComponent> ent, EntityUid user, int cost)
    {
        if (ent.Comp.Charge >= cost)
            return true;

        Say(ent, user, "ucf-smartpack-low-charge", ("charge", ent.Comp.Charge), ("cost", cost));
        return false;
    }

    private void Say(Entity<UCFSmartpackComponent> ent, EntityUid user, string message, params (string, object)[] args)
    {
        var all = new (string, object)[args.Length + 1];
        args.CopyTo(all, 0);
        all[^1] = ("pack", ent.Owner);
        _popup.PopupEntity(Loc.GetString(message, all), user, user, PopupType.MediumCaution);
    }

    private void Report(Entity<UCFSmartpackComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("ucf-smartpack-charge-report", ("charge", ent.Comp.Charge), ("max", ent.Comp.MaxCharge)), user, user);
    }

    private void OnProtective(Entity<UCFSmartpackComponent> ent, ref UCFSmartpackProtectiveEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;
        var user = args.Performer;
        var now = _timing.CurTime;

        if (!CanUse(ent, user))
            return;

        if (ent.Comp.Protective || ent.Comp.Immobile || now < ent.Comp.FormLockoutEnd)
        {
            Say(ent, user, "ucf-smartpack-unavailable");
            return;
        }

        if (!HasCharge(ent, user, ent.Comp.ProtectiveCost))
            return;

        ent.Comp.Charge -= ent.Comp.ProtectiveCost;
        ent.Comp.Protective = true;
        ent.Comp.ProtectiveEnd = now + ent.Comp.ProtectiveDuration;

        if (TryComp(user, out DamageableComponent? damageable))
        {
            ent.Comp.SavedModifier = damageable.DamageModifierSetId;
            _damageable.SetDamageModifierSetId((user, damageable), ent.Comp.ProtectiveModifier);
        }

        EnsureComp<UCFSmartpackProtectedComponent>(user);
        var unremoveable = EnsureComp<UnremoveableComponent>(ent);
        unremoveable.DeleteOnDrop = false;
        Dirty(ent, ent.Comp);
        Dirty(ent, unremoveable);

        _popup.PopupEntity(Loc.GetString("ucf-smartpack-protective-on", ("pack", ent.Owner)), user, user, PopupType.MediumCaution);
        Report(ent, user);
    }

    private void EndProtective(Entity<UCFSmartpackComponent> ent, bool announce)
    {
        if (!ent.Comp.Protective)
            return;

        ent.Comp.Protective = false;
        ent.Comp.ProtectiveEnd = null;

        if (ent.Comp.Wearer is { } wearer && Exists(wearer))
        {
            RemComp<UCFSmartpackProtectedComponent>(wearer);

            if (TryComp(wearer, out DamageableComponent? damageable))
                _damageable.SetDamageModifierSetId((wearer, damageable), ent.Comp.SavedModifier);

            if (announce)
                _popup.PopupEntity(Loc.GetString("ucf-smartpack-protective-off", ("pack", ent.Owner)), wearer, wearer, PopupType.MediumCaution);
        }

        ent.Comp.SavedModifier = null;
        RemComp<UnremoveableComponent>(ent);
        Dirty(ent);
    }

    private void OnImmobile(Entity<UCFSmartpackComponent> ent, ref UCFSmartpackImmobileEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;
        var user = args.Performer;
        var now = _timing.CurTime;

        if (!CanUse(ent, user))
            return;

        if (ent.Comp.Protective || now < ent.Comp.FormLockoutEnd)
        {
            Say(ent, user, "ucf-smartpack-unavailable");
            return;
        }

        if (ent.Comp.Immobile)
        {
            EndImmobile(ent);
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-immobile-off", ("pack", ent.Owner)), user, user, PopupType.MediumCaution);
        }
        else
        {
            if (!HasCharge(ent, user, ent.Comp.ImmobileCost))
                return;

            ent.Comp.Charge -= ent.Comp.ImmobileCost;
            ent.Comp.Immobile = true;
            EnsureComp<UCFSmartpackImmobileComponent>(user);
            _blocker.UpdateCanMove(user);
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-immobile-on", ("pack", ent.Owner)), user, user, PopupType.MediumCaution);
            Report(ent, user);
        }

        ent.Comp.FormLockoutEnd = now + ent.Comp.ImmobileLockout;
        Dirty(ent);
    }

    private void EndImmobile(Entity<UCFSmartpackComponent> ent)
    {
        if (!ent.Comp.Immobile)
            return;

        ent.Comp.Immobile = false;

        if (ent.Comp.Wearer is { } wearer && Exists(wearer))
        {
            RemComp<UCFSmartpackImmobileComponent>(wearer);
            _blocker.UpdateCanMove(wearer);
        }

        Dirty(ent);
    }

    private void OnRepair(Entity<UCFSmartpackComponent> ent, ref UCFSmartpackRepairEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;
        var user = args.Performer;

        if (!CanUse(ent, user))
            return;

        if (ent.Comp.Protective || ent.Comp.Repairing)
        {
            Say(ent, user, "ucf-smartpack-unavailable");
            return;
        }

        if (!HasCharge(ent, user, ent.Comp.RepairCost))
            return;

        if (!HasDamage(user, BruteGroup) && !HasDamage(user, BurnGroup))
        {
            Say(ent, user, "ucf-smartpack-no-damage");
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.RepairDelay, new UCFSmartpackRepairDoAfterEvent(), ent, user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.Repairing = true;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("ucf-smartpack-repair-start", ("pack", ent.Owner)), user, user, PopupType.MediumCaution);
    }

    private void OnRepairDoAfter(Entity<UCFSmartpackComponent> ent, ref UCFSmartpackRepairDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (args.Cancelled)
        {
            ent.Comp.Repairing = false;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-repair-cancelled", ("pack", ent.Owner)), user, user, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Charge = Math.Max(0, ent.Comp.Charge - ent.Comp.RepairCost);
        ent.Comp.RepairEnd = _timing.CurTime + ent.Comp.RepairCooldown;

        var heal = BuildHeal(user, BruteGroup, ent.Comp.RepairAmount);
        heal += BuildHeal(user, BurnGroup, ent.Comp.RepairAmount);
        _damageable.TryChangeDamage(user, heal, true, false, origin: user);

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("ucf-smartpack-repair-done", ("pack", ent.Owner), ("charge", ent.Comp.Charge), ("max", ent.Comp.MaxCharge)), user, user);
    }

    private bool HasDamage(EntityUid uid, ProtoId<DamageGroupPrototype> group)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return false;

        var current = _damageable.GetAllDamage((uid, damageable));
        if (current.Empty)
            return false;

        return current.GetDamagePerGroup(_prototypes).GetValueOrDefault(group) > FixedPoint2.Zero;
    }

    private DamageSpecifier BuildHeal(EntityUid uid, ProtoId<DamageGroupPrototype> group, float amount)
    {
        var heal = new DamageSpecifier();
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return heal;

        var current = _damageable.GetAllDamage((uid, damageable));
        var remaining = FixedPoint2.New(amount);
        foreach (var type in _prototypes.Index(group).DamageTypes)
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            var have = current.DamageDict.GetValueOrDefault(type);
            if (have <= FixedPoint2.Zero)
                continue;

            var take = FixedPoint2.Min(have, remaining);
            heal.DamageDict[type] = -take;
            remaining -= take;
        }

        return heal;
    }

    private void OnProtectedAttack(Entity<UCFSmartpackProtectedComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnProtectedShot(Entity<UCFSmartpackProtectedComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnProtectedThrow(Entity<UCFSmartpackProtectedComponent> ent, ref ThrowAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnImmobileCanMove(Entity<UCFSmartpackImmobileComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnImmobilePull(Entity<UCFSmartpackImmobileComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PulledUid == ent.Owner)
            args.Cancelled = true;
    }

    private bool TryGetPack(EntityUid user, out Entity<UCFSmartpackComponent> pack)
    {
        pack = default;
        if (!_inventory.TryGetSlotEntity(user, BackSlot, out var back) ||
            !TryComp(back, out UCFSmartpackComponent? comp))
        {
            return false;
        }

        pack = (back.Value, comp);
        return true;
    }

    private void OnBeforeInteractHand(Entity<SynthComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<RMCApcComponent>(args.Target))
            return;

        if (_intents.GetIntent(ent.Owner) != UCFIntent.Grab || !TryGetPack(ent.Owner, out var pack))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, pack.Comp.ApcChargeDelay, new UCFSmartpackChargeDoAfterEvent(), pack, args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnApcCharged(Entity<UCFSmartpackComponent> pack, ref UCFSmartpackChargeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } apc || !TryComp(apc, out RMCApcComponent? apcComp))
            return;

        args.Handled = true;
        var user = args.User;

        if (apcComp.Broken)
        {
            var spark = new DamageSpecifier();
            spark.DamageDict[HeatType] = FixedPoint2.New(10);
            _damageable.TryChangeDamage(user, spark, true, false, origin: apc);
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-apc-broken"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!_container.TryGetContainer(apc, apcComp.CellContainerSlot, out var slot) ||
            slot.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-apc-empty"), user, user, PopupType.SmallCaution);
            return;
        }

        var cell = slot.ContainedEntities[0];
        var available = _battery.GetCharge(cell);
        if (available <= 0)
        {
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-apc-empty"), user, user, PopupType.SmallCaution);
            return;
        }

        if (pack.Comp.Charge >= pack.Comp.MaxCharge)
        {
            _popup.PopupEntity(Loc.GetString("ucf-smartpack-apc-full", ("pack", pack.Owner)), user, user, PopupType.SmallCaution);
            return;
        }

        var taken = (int) Math.Min(available, pack.Comp.MaxCharge - pack.Comp.Charge);
        if (taken <= 0)
            return;

        _battery.UseCharge(cell, taken);
        pack.Comp.Charge += taken;
        Dirty(pack);
        _popup.PopupEntity(Loc.GetString("ucf-smartpack-apc-charged", ("pack", pack.Owner), ("charge", pack.Comp.Charge), ("max", pack.Comp.MaxCharge)), user, user);
    }
}

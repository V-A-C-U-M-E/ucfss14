#pragma warning disable RA0002 // the test sets smartpack charge directly

using System.Linq;
using Content.Shared._RMC14.Synth;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.UCF14.Synth.Smartpack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.UCF14.Synth;

[TestFixture]
public sealed class UCFSmartpackTest
{
    private static readonly string[] FormActions =
    [
        "UCFActionSmartpackProtective",
        "UCFActionSmartpackImmobile",
        "UCFActionSmartpackRepair"
    ];

    private static List<string?> ActionIds(IEntityManager entMan, EntityUid user)
    {
        return entMan.System<SharedActionsSystem>()
            .GetActions(user)
            .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
            .ToList();
    }

    [Test]
    public async Task SmartpackFormsWorkForSynthsOnly()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid synth = default;
        EntityUid pack = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            synth = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<SynthComponent>(synth);
            Assert.That(inventory.SpawnItemInSlot(synth, "back", "RMCSmartpack"), Is.True);
            Assert.That(inventory.TryGetSlotEntity(synth, "back", out var back), Is.True);
            pack = back!.Value;

            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(inventory.SpawnItemInSlot(human, "back", "RMCSmartpack"), Is.True);

            var synthActions = ActionIds(entMan, synth);
            var humanActions = ActionIds(entMan, human);
            Assert.Multiple(() =>
            {
                foreach (var id in FormActions)
                {
                    Assert.That(synthActions, Does.Contain(id));
                    Assert.That(humanActions, Does.Not.Contain(id));
                }
            });
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var blocker = entMan.System<ActionBlockerSystem>();
            var comp = entMan.GetComponent<UCFSmartpackComponent>(pack);
            comp.Charge = 200;

            var original = entMan.GetComponent<DamageableComponent>(synth).DamageModifierSetId;

            var protective = new UCFSmartpackProtectiveEvent { Performer = synth };
            entMan.EventBus.RaiseLocalEvent(pack, protective);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Protective, Is.True);
                Assert.That(comp.Charge, Is.EqualTo(50));
                Assert.That(entMan.HasComponent<UCFSmartpackProtectedComponent>(synth), Is.True);
                Assert.That(entMan.GetComponent<DamageableComponent>(synth).DamageModifierSetId?.Id, Is.EqualTo("UCFSmartpackProtective"));
            });

            var immobile = new UCFSmartpackImmobileEvent { Performer = synth };
            entMan.EventBus.RaiseLocalEvent(pack, immobile);
            Assert.That(comp.Immobile, Is.False, "forms are mutually exclusive");

            comp.ProtectiveEnd = System.TimeSpan.Zero;
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blocker = entMan.System<ActionBlockerSystem>();
            var comp = entMan.GetComponent<UCFSmartpackComponent>(pack);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Protective, Is.False);
                Assert.That(entMan.HasComponent<UCFSmartpackProtectedComponent>(synth), Is.False);
                Assert.That(entMan.GetComponent<DamageableComponent>(synth).DamageModifierSetId?.Id, Is.Not.EqualTo("UCFSmartpackProtective"));
            });

            var immobile = new UCFSmartpackImmobileEvent { Performer = synth };
            entMan.EventBus.RaiseLocalEvent(pack, immobile);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Immobile, Is.True);
                Assert.That(comp.Charge, Is.EqualTo(30));
                Assert.That(blocker.CanMove(synth), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}

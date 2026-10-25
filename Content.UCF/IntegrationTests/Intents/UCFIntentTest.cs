using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.UCF14.Intents;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.UCF14.Intents;

[TestFixture]
public sealed class UCFIntentTest
{
    [Test]
    public async Task IntentsDriveCombatModeAndAlert()
    {
        var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var intents = entMan.System<UCFIntentSystem>();
            var combat = entMan.System<SharedCombatModeSystem>();

            var mob = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            entMan.EnsureComponent<UCFIntentComponent>(mob);

            intents.SetIntent(mob, UCFIntent.Harm);
            Assert.Multiple(() =>
            {
                Assert.That(intents.GetIntent(mob), Is.EqualTo(UCFIntent.Harm));
                Assert.That(combat.IsInCombatMode(mob), Is.True);
            });

            intents.SetIntent(mob, UCFIntent.Help);
            Assert.That(combat.IsInCombatMode(mob), Is.False);

            intents.Cycle(mob);
            Assert.Multiple(() =>
            {
                Assert.That(intents.GetIntent(mob), Is.EqualTo(UCFIntent.Disarm));
                Assert.That(combat.IsInCombatMode(mob), Is.True);
            });

            intents.Cycle(mob);
            intents.Cycle(mob);
            intents.Cycle(mob);
            Assert.Multiple(() =>
            {
                Assert.That(intents.GetIntent(mob), Is.EqualTo(UCFIntent.Help));
                Assert.That(combat.IsInCombatMode(mob), Is.False);
                Assert.That(entMan.System<AlertsSystem>().IsShowingAlert(mob, "UCFIntent"), Is.True);
            });

            var noIntent = entMan.SpawnEntity("MobMouse", map.MapCoords);
            Assert.That(intents.GetIntent(noIntent), Is.EqualTo(UCFIntent.Harm));
        });

        await pair.CleanReturnAsync();
    }
}

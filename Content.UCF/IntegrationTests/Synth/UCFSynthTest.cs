using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared._RMC14.Synth;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.UCF14.Synth;

[TestFixture]
public sealed class UCFSynthTest
{
    [Test]
    public async Task SynthsResistStunsAndPullAnyXeno()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var synth = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<SynthComponent>(synth);

            var addComponents = server.ProtoMan.Index<EntityPrototype>("RMCSynthAddComponents");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<RMCStunResistanceComponent>(synth).Resistance, Is.EqualTo(5f));
                Assert.That(addComponents.Components.ContainsKey("ParalyzeOnPullAttemptImmune"), Is.True);
                Assert.That(addComponents.Components.ContainsKey("ParalyzeImmuneFrom"), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}

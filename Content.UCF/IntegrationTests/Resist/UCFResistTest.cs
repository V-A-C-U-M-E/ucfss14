using Content.Shared.Buckle;
using Content.Shared.UCF14.Resist;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.UCF14.Resist;

[TestFixture]
public sealed class UCFResistTest
{
    [Test]
    public async Task ResistFreesBuckledMob()
    {
        var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var resist = entMan.System<UCFResistSystem>();

            var chair = entMan.SpawnEntity("Chair", map.MapCoords);
            mob = entMan.SpawnEntity("CMMobHuman", map.MapCoords);

            Assert.That(resist.Resist(mob), Is.False);
            Assert.That(buckle.TryBuckle(mob, mob, chair), Is.True);
            Assert.That(buckle.IsBuckled(mob), Is.True);
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var resist = entMan.System<UCFResistSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(resist.Resist(mob), Is.True);
                Assert.That(buckle.IsBuckled(mob), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}

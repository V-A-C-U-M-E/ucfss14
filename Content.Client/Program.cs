using Robust.Client;
using Robust.Shared;

namespace Content.Client
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ContentStart.StartLibrary(args, new GameControllerOptions
            {
                MountOptions = new MountOptions(
                    dirMounts: new List<string> { "../../Content.CMU/Resources", "../../Content.UCF/Resources" }, // UCF14
                    zipMounts: new List<string>()),
            });
        }
    }
}

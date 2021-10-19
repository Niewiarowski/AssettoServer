using Qmmands;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Attributes
{
    public class RequireConnectedPlayerAttribute : CheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(CommandContext context)
        {
            if (context is ACCommandContext acContext && !acContext.IsConsole)
                return CheckResult.Successful;

            return CheckResult.Failed("This command cannot be executed by the console.");
        }
    }
}

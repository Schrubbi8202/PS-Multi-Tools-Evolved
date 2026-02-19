using System.Diagnostics;
using PSMultiTools.Core;

namespace PSMultiTools.Infrastructure;

public sealed class ToolProcessRunner : IToolProcessRunner
{
    public async Task<int> RunAsync(string toolPath, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}

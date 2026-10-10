using System.Diagnostics;
using System.Text;

namespace OpenDot.Automation.Workspaces;

public class CommandRunner
{
    public static async Task<string> RunAsync(string command, string workingDir, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        Directory.CreateDirectory(workingDir);
        var timeoutLimit = timeout ?? TimeSpan.FromMinutes(2);

        // Escape command for powershell.exe -Command
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "`\"")}\"",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(timeoutLimit);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);

            var stdOut = output.ToString().Trim();
            var stdErr = error.ToString().Trim();

            if (process.ExitCode != 0)
            {
                var combined = string.IsNullOrWhiteSpace(stdErr) ? stdOut : $"{stdOut}\nError: {stdErr}";
                return string.IsNullOrWhiteSpace(combined) ? $"Process exited with code {process.ExitCode}" : combined;
            }

            return string.IsNullOrWhiteSpace(stdOut) ? "(no output)" : stdOut;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch { }
            return "Command execution timed out or was cancelled.";
        }
        catch (Exception ex)
        {
            return $"Failed to run command: {ex.Message}";
        }
    }
}

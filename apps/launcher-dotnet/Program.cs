using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Assume repo-root-relative execution in CI and in dev.
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FlowByEarthin"
        );
        var envPath = Path.Combine(appDataDir, ".env");
        Directory.CreateDirectory(appDataDir);

        // Copy .env.example -> user env on first run (no secrets embedded in exe)
        var envExamplePath = Path.Combine(repoRoot, ".env.example");
        if (!File.Exists(envPath) && File.Exists(envExamplePath))
            File.Copy(envExamplePath, envPath);

        var composePath = Path.Combine(repoRoot, "infra", "docker-compose.yml");
        if (!File.Exists(composePath))
        {
            Console.WriteLine("infra/docker-compose.yml not found. Repo root: " + repoRoot);
            return 1;
        }

        Console.WriteLine("Starting docker compose...");
        var p = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose -f \"{composePath}\" --env-file \"{envPath}\" up -d",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (p == null) return 1;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        Console.WriteLine(stdout);
        Console.WriteLine(stderr);

        var webUrl = "http://localhost:3000";
        Console.WriteLine("Waiting for UI: " + webUrl);

        using var http = new HttpClient();
        var timeoutAt = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                var resp = await http.GetAsync(webUrl);
                if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 500)
                    break;
            }
            catch { }

            await Task.Delay(1500);
        }

        Process.Start(new ProcessStartInfo { FileName = webUrl, UseShellExecute = true });
        Console.WriteLine("Done.");
        return 0;
    }
}


using System.Diagnostics;

namespace IntegrationTests;

public class TestServer : IDisposable
{
    public string Url { get; }
    private Process _process;

    private TestServer(string url, Process process)
    {
        Url = url;
        _process = process;
    }

    public static async Task<TestServer> StartNew()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "ChatServer.dll",
                UseShellExecute = true,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            throw new ApplicationException("Failed to start server");
        }

        await Task.Delay(TimeSpan.FromSeconds(1));

        return new("http://localhost:5000", process);
    }

    public void Dispose()
    {
        _process.Kill();
    }
}

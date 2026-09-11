using System.IO.Pipes;

namespace Stage3ServerNamedPipes;

class Program
{
    static void Main(string[] args)
    {
        using var server = new NamedPipeServerStream("pipelab", PipeDirection.InOut);

        Console.WriteLine("[server] waiting for connection...");
        server.WaitForConnection();
        Console.WriteLine("[server] connected!");

        var buffer = new byte[4096];
        int n;
        while ((n = server.Read(buffer, 0, buffer.Length)) > 0)
            Console.WriteLine($"[server] read {n} bytes: {System.Text.Encoding.UTF8.GetString(buffer, 0, n)}");

        Console.WriteLine("[server] client disconnected");
    }
}
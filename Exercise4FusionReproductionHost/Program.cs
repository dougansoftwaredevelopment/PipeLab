using System.IO.Pipes;

namespace Exercise4FusionReproductionHost;

class Program
{
    static void Main(string[] args)
    {
        //Create the named pipe
        using var fusionPipe = new NamedPipeServerStream("FusionPipe", PipeDirection.InOut);
        Console.WriteLine("[server] waiting for connection...");
        fusionPipe.WaitForConnection();
        Console.WriteLine("[server] server has connected succesfully!");
        Thread.Sleep(5000);  
        var buffer = new byte[4096];
        int n;
        while ((n = fusionPipe.Read(buffer)) > 0)
            Console.WriteLine($"[server] read {n} bytes: {System.Text.Encoding.UTF8.GetString(buffer, 0, n)}");

        Console.WriteLine("[server] client disconnected");
        

    }
}
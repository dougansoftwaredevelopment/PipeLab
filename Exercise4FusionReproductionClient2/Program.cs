using System.IO.Pipes;
using System.Text;

namespace Exercise4FusionReproductionClient2;

class Program
{
    static void Main(string[] args)
    {
        using var client = new NamedPipeClientStream(".", "FusionPipe", PipeDirection.InOut);
        client.Connect(5000);
        Console.WriteLine("[client] connected");

        foreach (var msg in new[] { "helloClient1", "firstClient1", "secondClient1", "thirdClient1" })
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            client.Write(bytes, 0, bytes.Length);
            Console.WriteLine($"[client] wrote {bytes.Length} bytes: {msg}");
        }

        Console.WriteLine("[client] done, closing");
    }
}
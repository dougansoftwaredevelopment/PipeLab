using System.IO.Pipes;
using System.Text;

namespace Stage3ClientNamedPipes;

class Program
{
    static void Main(string[] args)
    {
        using var client = new NamedPipeClientStream(".", "pipelab", PipeDirection.InOut);
        client.Connect(5000);
        Console.WriteLine("[client] connected");

        var payload = Encoding.UTF8.GetBytes("hello");
        client.Write(payload, 0, payload.Length);
        
        foreach (var msg in new[] { "first", "second", "third" })
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            client.Write(bytes, 0, bytes.Length);
        }
    }
}
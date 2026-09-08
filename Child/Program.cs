using System.IO.Pipes;

namespace Child;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Bootstrapping Child Pipe process...");
        //Instantiate the client
        using var client = new AnonymousPipeClientStream(PipeDirection.In, args[0]);
        using var reader = new StreamReader(client);

        string? line;
        while ((line = reader.ReadLine()) is not null)
            Console.WriteLine($"[child] got: {line}");
            
        Console.WriteLine("[Child] pipe closed, exiting");
    }
}
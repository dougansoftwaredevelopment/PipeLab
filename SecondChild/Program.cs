using System.IO.Pipes;

namespace SecondChild;

class Program
{
    static void Main(string[] args)
    {
        using var client = new AnonymousPipeClientStream(PipeDirection.Out, args[0]);
        using var writer = new StreamWriter(client) { AutoFlush = true };

        writer.WriteLine("hello from child");
        writer.WriteLine("line two");
        writer.WriteLine("line three");

        Console.WriteLine("[child] wrote three lines, exiting");
    }
}
using System.IO.Pipes;

namespace Exercise2Child;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: Exercise2Child <pipe-handle>");
            return 1;
        }

        using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, args[0]);
        using var writer = new StreamWriter(pipe) { AutoFlush = true };

        writer.WriteLine("hello from child");
        writer.WriteLine("line two");
        writer.WriteLine("line three");

        Console.WriteLine("[child] wrote three lines");
        return 0;
    }
}
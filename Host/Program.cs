using System.Diagnostics;
using System.IO.Pipes;

namespace Host;

class Program
{
    static void Main(string[] args)
    {
     // EXERCISE 1 GO FROM HOST TO CHILD   
        string childPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Child", "bin", "Debug", "net10.0", "Child.exe"));

        if (!File.Exists(childPath))
            throw new FileNotFoundException($"child not built: {childPath}");
        
        
        //create the pipe
        using var server = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);//instantiate the pipe from the win32 and system.IO.Pipes bcl
        var clientHandle = server.GetClientHandleAsString(); // need to grab the handle here
        Console.WriteLine($"[host] handling child handle {clientHandle}"); // dump status to stdout

        var psi = new ProcessStartInfo
        {
            FileName = childPath,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(clientHandle);

        using var child = Process.Start(psi)!;
        
        
        server.DisposeLocalCopyOfClientHandle();

        using (var writer = new StreamWriter(server) {AutoFlush = true})
        {
            writer.WriteLine("Hello from host");
            writer.WriteLine("line two");
            writer.WriteLine("line three");
        }

        child.WaitForExit();
        Console.WriteLine($"[host] child exited with {child.ExitCode}");
        
    }
}
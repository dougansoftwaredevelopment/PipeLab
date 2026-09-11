using System.Diagnostics;
using System.IO.Pipes;

namespace Exercise2Host;

class Program
{
    static void Main(string[] args)
    {
        // HOST (writes)
        // resolve + verify child exe path
        string childPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, 
            "../","../","../", "../",
            "Exercise2Child/","bin/","debug/","net10.0/","Exercise2Child.exe"));

        if (!File.Exists(childPath))
            throw new FileNotFoundException($"Child not built: {childPath}");
        
        // create AnonymousPipeServerStream(Out, Inheritable)

        var host2 = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
       
        // get client handle string
        var hostHanlde = host2.GetClientHandleAsString();
        // build ProcessStartInfo: FileName = child path, UseShellExecute = false
        var psi = new ProcessStartInfo()
        {
            FileName = childPath,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(hostHanlde);
        // add handle to ArgumentList
        // start the process
        using var child = Process.Start(psi)!;
        // dispose local copy of client handle      <- after start, never before
        host2.DisposeLocalCopyOfClientHandle();
        // wrap server in StreamWriter (AutoFlush)
        using var reader = new StreamReader(host2);
        string? line;
        while((line = reader.ReadLine()) is not null) 
            Console.WriteLine($"[host] got {line}");
        
        // write lines
        // dispose the writer                        <- this is what signals EOF
        // WaitForExit, report exit code
        child.WaitForExit();
    }
}
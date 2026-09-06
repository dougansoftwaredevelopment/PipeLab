# PipeLab

A hands-on lab covering everything in .NET that gets called "pipes". Not a sample app — a sequence of stages where each one is built, deliberately broken, and then fixed.

"Pipes" in .NET means three unrelated things. This lab covers all of them:

| Concept | Type | What it actually is |
|---|---|---|
| Anonymous pipes | `AnonymousPipeServerStream` / `AnonymousPipeClientStream` | A raw inherited handle pair between parent and child process |
| Named pipes | `NamedPipeServerStream` / `NamedPipeClientStream` | A named rendezvous point; any two processes, with ACLs and identity |
| Pipelines | `PipeReader` / `PipeWriter` | Not IPC at all — a buffering and parsing abstraction for byte streams |

## Requirements

- .NET 8 or later (`ReadExactlyAsync` needs .NET 7+; the pre-7 manual loop is written in Stage 3 anyway)
- Windows for Stages 4 and 6b (message mode and Win32 ACLs); everything else is cross-platform
- Optional: Sysinternals `pipelist`, BenchmarkDotNet

## Layout

```
PipeLab.sln
  PipeLab.Host/     parent process / pipe server
  PipeLab.Child/    child process / pipe client
  PipeLab.Core/     framing, protocol, shared helpers (added at Stage 3)
```

Two real processes, not two threads. Threads hide half the bugs this lab is about.

```
dotnet new sln -n PipeLab
dotnet new console -n PipeLab.Host
dotnet new console -n PipeLab.Child
```

Run a stage with `dotnet run --project PipeLab.Host -- --stage 3`.

---

## Stage 1 — Anonymous pipes, one direction

**Build:** Host creates `new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable)`, calls `GetClientHandleAsString()`, and launches the child with that string as `argv[0]`. Child opens `new AnonymousPipeClientStream(PipeDirection.In, args[0])` and reads to end.

**Break it first:** omit `server.DisposeLocalCopyOfClientHandle()` after the child starts. The child hangs forever.

**Why:** EOF on a pipe is refcount-driven, not content-driven. While the host holds its own copy of the write handle open, the OS never signals end of stream to the reader.

**Exercise:** make it bidirectional. `PipeDirection.InOut` throws for anonymous pipes, so you need two pipes and two handle strings. Note how fast this gets unpleasant — that's the case for named pipes.

**Done when:** child prints the payload and exits cleanly with no hang, both directions.

---

## Stage 2 — The pipes you already use

Redirected stdio is an anonymous pipe.

**Build:** Host launches the child with `RedirectStandardOutput` and `RedirectStandardError`, and reads both synchronously in sequence with `ReadToEnd()`. Child writes ~100 KB to stderr before anything to stdout.

**Break:** it deadlocks. The child blocks filling the stderr pipe buffer (roughly 4–64 KB depending on platform) while the host is blocked reading stdout.

**Fix:** `ReadToEndAsync` on both, awaited with `Task.WhenAll`, then `WaitForExit`.

**Done when:** you can explain the deadlock without looking at this file.

---

## Stage 3 — Named pipes and the framing lie

**Build:** `NamedPipeServerStream("pipelab", PipeDirection.InOut)` + `WaitForConnectionAsync()`. Client: `NamedPipeClientStream(".", "pipelab", PipeDirection.InOut)` + `ConnectAsync(timeout)`. Naive echo — one `ReadAsync` into a 4 KB buffer.

**Break:** client fires three writes back to back with no delay. The server reads all three at once, or half of one. A byte-mode pipe is a stream, not a datagram.

**Fix — write this yourself, it goes in PipeLab.Core:**

```csharp
// write
Span<byte> len = stackalloc byte[4];
BinaryPrimitives.WriteInt32LittleEndian(len, payload.Length);
await stream.WriteAsync(len, ct);
await stream.WriteAsync(payload, ct);

// read
await stream.ReadExactlyAsync(header, ct);
int n = BinaryPrimitives.ReadInt32LittleEndian(header);
if (n < 0 || n > MaxFrame) throw new InvalidDataException();
```

Write the manual read loop version too (pre-.NET 7), at least once. The length cap is not optional — an unchecked prefix is a real DoS in shipped IPC code.

**Done when:** 10,000 back-to-back frames of random size arrive intact and in order.

---

## Stage 4 — Message mode, and why it's a trap

**Build (Windows):** server ctor takes `PipeTransmissionMode.Message`; client sets `ReadMode = PipeTransmissionMode.Message`. Read in a loop, growing the buffer until `IsMessageComplete` is true.

**The lesson:** this throws `PlatformNotSupportedException` on Linux and macOS, where .NET implements named pipes as **Unix domain sockets** rather than FIFOs. Message mode is not portable. Stage 3's framing is what actually ships.

**Done when:** the same binary runs on both platforms, using message mode where available and falling back to length-prefix framing otherwise.

---

## Stage 5 — More than one client

One `NamedPipeServerStream` instance serves exactly one connection.

**Break first:** accept a connection, handle it to completion, then loop. Prove with two concurrent clients that you've written a serial server.

**Fix:** create an instance, `await WaitForConnectionAsync(ct)`, hand the connection off to a separate task, loop immediately to create the next instance. Set `maxNumberOfServerInstances` (or `NamedPipeServerStream.MaxAllowedServerInstances`).

**Also:** add `PipeOptions.Asynchronous` and measure. Without it the handle is opened synchronously on Windows and your async reads are thread-pool blocking in disguise.

**Exercise:** graceful shutdown. Cancel the accept loop, drain in-flight clients. Treat `IOException` on a dead client as normal control flow, not an error.

**Done when:** 50 concurrent clients complete, and Ctrl+C drains rather than drops.

---

## Stage 6 — Security

The main reason to reach for named pipes over a TCP port.

**6a — ACLs.** Build a `PipeSecurity` and create the server with `NamedPipeServerStreamAcl.Create(...)`, granting `PipeAccessRights.ReadWrite` to one specific SID. Connect from another user account and watch it fail.

**6b — Squatting.** Start a hostile "server" from a second process that grabs `\\.\pipe\pipelab` before the real one. Demo the attack, then the mitigations:

- Server refuses to run unless it created the *first* instance of the pipe.
- Clients pass `TokenImpersonationLevel.Anonymous` so a rogue server can't impersonate them.

**6c — Impersonation.** Call `server.RunAsClient(() => ...)` and print `WindowsIdentity.GetCurrent().Name` inside and outside the callback.

**6d — Unix.** `PipeOptions.CurrentUserOnly`, then `ls -l /tmp/CoreFxPipe_pipelab` to see the socket file and its mode bits.

**Done when:** an unauthorized user is denied, and the squat attempt is detected and refused.

---

## Stage 7 — System.IO.Pipelines

Rewrite the Stage 3 read side over `PipeReader.Create(stream)`.

```csharp
var reader = PipeReader.Create(pipeStream);
while (true)
{
    ReadResult result = await reader.ReadAsync(ct);
    ReadOnlySequence<byte> buffer = result.Buffer;
    while (TryParseFrame(ref buffer, out var frame)) Handle(frame);
    reader.AdvanceTo(buffer.Start, buffer.End);
    if (result.IsCompleted) break;
}
await reader.CompleteAsync();
```

**Break both ways, on purpose:**

- `AdvanceTo(buffer.Start, buffer.Start)` on a partial frame → infinite spin; `ReadAsync` returns instantly with the same bytes.
- `AdvanceTo(buffer.End)` → you consumed a partial frame and lost it.

`consumed` is what you're done with; `examined` is how far you looked. Returning without advancing `examined` past what you've seen is the spin bug.

**Multi-segment:** `ReadOnlySequence<byte>` is not contiguous. The parser must use `SequenceReader<byte>` (`TryReadLittleEndian`, `TryReadExact`) rather than assuming a single span. Force it by having the client write in 7-byte chunks with delays.

**Backpressure:** build a raw `new Pipe(new PipeOptions(pauseWriterThreshold: 8192, resumeWriterThreshold: 4096))`, run a fast producer against a slow consumer, and observe `FlushAsync` actually blocking. This is the feature Kestrel is built on.

**Benchmark:** Stage 3 vs Stage 7 over 1M small frames with BenchmarkDotNet. Watch allocations, not just time.

**Done when:** the parser survives a client that writes one byte at a time.

---

## Stage 8 — Capstone

A small request/response service pulling in everything above:

- Named pipe server, N concurrent clients, cancellation-driven shutdown
- Length-prefixed JSON frames, `PipeReader`/`PipeWriter` on both ends
- Correlation IDs so responses may return out of order
- DACL restricted to the current user
- A `--squat` flag that runs the Stage 6b attack against your own server

## Diagnostics

```powershell
# Windows: list live named pipes
[System.IO.Directory]::GetFiles("\\.\pipe\")
pipelist.exe            # Sysinternals, shows instance counts
```

```bash
# Linux/macOS: .NET named pipes are Unix domain sockets
ls -l /tmp/CoreFxPipe_*
```

## Gotcha index

| Symptom | Cause | Stage |
|---|---|---|
| Child read never returns | Local copy of client handle not disposed | 1 |
| `Process` hangs on large output | Serial synchronous reads of stdout and stderr | 2 |
| Messages merge or split | Byte-mode pipe has no message boundaries | 3 |
| `PlatformNotSupportedException` | Message mode requested on Unix | 4 |
| Second client waits for the first | Accept loop handles before looping | 5 |
| Async reads still block a thread | `PipeOptions.Asynchronous` missing | 5 |
| Server impersonated by another process | Pipe name squatted before startup | 6 |
| `ReadAsync` spins at 100% CPU | `examined` not advanced in `AdvanceTo` | 7 |
| Random parse failures under load | Parser assumed a single-segment sequence | 7 |

## Reference

- `System.IO.Pipes` — anonymous and named pipes
- `System.IO.Pipelines` — `Pipe`, `PipeReader`, `PipeWriter`, `PipeOptions`
- `System.Buffers` — `ReadOnlySequence<byte>`, `SequenceReader<byte>`
- `System.IO.Pipes.AccessControl` — `PipeSecurity`, `NamedPipeServerStreamAcl`

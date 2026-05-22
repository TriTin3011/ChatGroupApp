using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

const int DefaultPort = 5000;

var port = ReadPort(args);
var clients = new ConcurrentDictionary<TcpClient, ClientState>();
var listener = new TcpListener(IPAddress.Any, port);

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "ChatGroupApp Server";

listener.Start();

Console.WriteLine("=== ChatGroupApp Server ===");
Console.WriteLine($"Server running at port: {port}");
Console.WriteLine("IP server:");
foreach (var ip in GetLocalIPv4Addresses())
{
    Console.WriteLine($" - {ip}");
}

Console.WriteLine();
Console.WriteLine("Clients can connect with one of the IP addresses above.");
Console.WriteLine("Press Ctrl+C to stop the server.");
Console.WriteLine();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    listener.Stop();
    foreach (var client in clients.Keys)
    {
        client.Close();
    }
};

try
{
    while (true)
    {
        var tcpClient = await listener.AcceptTcpClientAsync();
        _ = HandleClientAsync(tcpClient);
    }
}
catch (SocketException)
{
    Console.WriteLine("Server stopped.");
}
catch (ObjectDisposedException)
{
    Console.WriteLine("Server stopped.");
}

async Task HandleClientAsync(TcpClient tcpClient)
{
    var endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
    Console.WriteLine($"Client connected: {endpoint}");

    try
    {
        await using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var requestedName = await reader.ReadLineAsync();
        var name = SanitizeName(requestedName, endpoint);
        var state = new ClientState(name, writer);
        clients[tcpClient] = state;

        await SendToClientAsync(state, $"SERVER|Welcome {name}! Connected to ChatGroupApp.");
        await BroadcastAsync($"SERVER|{name} joined the chat.", tcpClient);
        Console.WriteLine($"{name} joined.");

        string? message;
        while ((message = await reader.ReadLineAsync()) is not null)
        {
            message = message.Trim();
            if (message.Length == 0)
            {
                continue;
            }

            Console.WriteLine($"{name}: {message}");
            await BroadcastAsync($"CHAT|{name}|{message}", tcpClient);
            await SendToClientAsync(state, $"ME|{message}");
        }
    }
    catch (IOException)
    {
    }
    catch (SocketException)
    {
    }
    finally
    {
        if (clients.TryRemove(tcpClient, out var state))
        {
            Console.WriteLine($"{state.Name} left.");
            await BroadcastAsync($"SERVER|{state.Name} left the chat.", tcpClient);
        }

        tcpClient.Close();
    }
}

async Task BroadcastAsync(string line, TcpClient? exceptClient = null)
{
    var disconnectedClients = new List<TcpClient>();

    foreach (var (client, state) in clients)
    {
        if (ReferenceEquals(client, exceptClient))
        {
            continue;
        }

        try
        {
            await SendToClientAsync(state, line);
        }
        catch (IOException)
        {
            disconnectedClients.Add(client);
        }
        catch (ObjectDisposedException)
        {
            disconnectedClients.Add(client);
        }
    }

    foreach (var client in disconnectedClients)
    {
        clients.TryRemove(client, out _);
        client.Close();
    }
}

static async Task SendToClientAsync(ClientState state, string line)
{
    await state.SendLock.WaitAsync();
    try
    {
        await state.Writer.WriteLineAsync(line);
    }
    finally
    {
        state.SendLock.Release();
    }
}

static int ReadPort(string[] args)
{
    if (args.Length > 0 && int.TryParse(args[0], out var argPort) && IsValidPort(argPort))
    {
        return argPort;
    }

    Console.Write($"Enter Port server : ");
    var input = Console.ReadLine();
    if (int.TryParse(input, out var typedPort) && IsValidPort(typedPort))
    {
        return typedPort;
    }

    return DefaultPort;
}

static bool IsValidPort(int port)
{
    return port is > 0 and <= 65535;
}

static string SanitizeName(string? name, string fallback)
{
    name = name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return $"Guest-{fallback.Replace(':', '-')}";
    }

    return name.Length <= 24 ? name : name[..24];
}

static IEnumerable<IPAddress> GetLocalIPv4Addresses()
{
    return NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address)
        .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
        .DefaultIfEmpty(IPAddress.Loopback);
}

sealed record ClientState(string Name, StreamWriter Writer)
{
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}

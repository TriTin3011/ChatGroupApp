using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatGroupApp.Client;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages = [];
    private readonly string[] _emoji =
    [
        "😀", "😁", "😂", "🤣", "😊", "😍",
        "😘", "😎", "😢", "😭", "😡", "👍",
        "👏", "🙏", "💪", "🎉", "🔥", "❤️",
        "💖", "⭐", "✅", "❌", "🍀", "☕"
    ];

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _receiveCancellation;

    public MainWindow()
    {
        InitializeComponent();
        MessagesListBox.ItemsSource = _messages;
        BuildEmojiButtons();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortTextBox.Text.Trim(), out var port) || port is <= 0 or > 65535)
        {
            MessageBox.Show("Port phai la so tu 1 den 65535.", "Sai port", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var host = ServerIpTextBox.Text.Trim();
        var userName = UserNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(userName))
        {
            MessageBox.Show("Vui long nhap IP server va ten cua ban.", "Thieu thong tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetConnectionUi(isConnected: false, isConnecting: true);
            StatusTextBlock.Text = "Dang ket noi server...";

            _client = new TcpClient();
            await _client.ConnectAsync(host, port);

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };

            await _writer.WriteLineAsync(userName);
            _receiveCancellation = new CancellationTokenSource();

            SetConnectionUi(isConnected: true);
            AddMessage("He thong", $"Da ket noi toi {host}:{port}", MessageKind.System);
            StatusTextBlock.Text = $"Dang chat tai {host}:{port}";

            _ = ReceiveLoopAsync(_receiveCancellation.Token);
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            AddMessage("He thong", $"Khong ket noi duoc server: {ex.Message}", MessageKind.System);
            Disconnect();
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        Disconnect();
        AddMessage("He thong", "Da ngat ket noi.", MessageKind.System);
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        EmojiPanel.Visibility = EmojiPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Disconnect();
    }

    private async Task SendMessageAsync()
    {
        var text = MessageTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || _writer is null)
        {
            return;
        }

        try
        {
            await _writer.WriteLineAsync(text);
            MessageTextBox.Clear();
            MessageTextBox.Focus();
        }
        catch (IOException ex)
        {
            AddMessage("He thong", $"Gui tin nhan that bai: {ex.Message}", MessageKind.System);
            Disconnect();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                Dispatcher.Invoke(() => HandleServerLine(line));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            Dispatcher.Invoke(() => AddMessage("He thong", "Mat ket noi server.", MessageKind.System));
        }
        finally
        {
            Dispatcher.Invoke(Disconnect);
        }
    }

    private void HandleServerLine(string line)
    {
        var parts = line.Split('|', 3);
        switch (parts[0])
        {
            case "CHAT" when parts.Length == 3:
                AddMessage(parts[1], parts[2], MessageKind.Other);
                break;
            case "ME" when parts.Length == 2:
                AddMessage("Ban", parts[1], MessageKind.Me);
                break;
            case "SERVER" when parts.Length == 2:
                AddMessage("He thong", parts[1], MessageKind.System);
                break;
            default:
                AddMessage("Server", line, MessageKind.System);
                break;
        }
    }

    private void BuildEmojiButtons()
    {
        foreach (var emoji in _emoji)
        {
            var button = new Button
            {
                Content = emoji,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 20,
                Margin = new Thickness(3),
                MinHeight = 36,
                ToolTip = $"Chen {emoji}"
            };

            button.Click += (_, _) =>
            {
                var selectionStart = MessageTextBox.SelectionStart;
                MessageTextBox.Text = MessageTextBox.Text.Insert(selectionStart, emoji);
                MessageTextBox.SelectionStart = selectionStart + emoji.Length;
                MessageTextBox.Focus();
            };

            EmojiPanel.Children.Add(button);
        }
    }

    private void AddMessage(string sender, string text, MessageKind kind)
    {
        _messages.Add(new ChatMessage(sender, text, kind));
        MessagesListBox.ScrollIntoView(_messages[^1]);
    }

    private void SetConnectionUi(bool isConnected, bool isConnecting = false)
    {
        ServerIpTextBox.IsEnabled = !isConnected && !isConnecting;
        PortTextBox.IsEnabled = !isConnected && !isConnecting;
        UserNameTextBox.IsEnabled = !isConnected && !isConnecting;
        ConnectButton.IsEnabled = !isConnected && !isConnecting;
        DisconnectButton.IsEnabled = isConnected;
        MessageTextBox.IsEnabled = isConnected;
        SendButton.IsEnabled = isConnected;
    }

    private void Disconnect()
    {
        _receiveCancellation?.Cancel();
        _receiveCancellation?.Dispose();
        _receiveCancellation = null;

        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Close();

        _reader = null;
        _writer = null;
        _client = null;

        SetConnectionUi(isConnected: false);
        StatusTextBlock.Text = "Chua ket noi server.";
    }
}

public sealed class ChatMessage(string sender, string text, MessageKind kind)
{
    public string Sender { get; } = sender;
    public string Text { get; } = text;

    public Brush Background { get; } = kind switch
    {
        MessageKind.Me => new SolidColorBrush(Color.FromRgb(218, 238, 255)),
        MessageKind.Other => new SolidColorBrush(Color.FromRgb(239, 242, 247)),
        _ => new SolidColorBrush(Color.FromRgb(255, 246, 213))
    };

    public HorizontalAlignment Alignment { get; } = kind == MessageKind.Me
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;
}

public enum MessageKind
{
    System,
    Other,
    Me
}

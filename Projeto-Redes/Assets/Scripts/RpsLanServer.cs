using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RpsLanServer : MonoBehaviour
{
    public const int Port = 7777;

    public event Action<string[]> OnPlayers;     // [host, client?]
    public event Action<string> OnStatus;
    public event Action OnStartGame;

    // ✅ Resultado do jogo (linha RESULT|...)
    public event Action<string> OnResultLine;

    public bool IsHosting => _listener != null;
    public bool HasClient => !string.IsNullOrWhiteSpace(_clientNick) && _client != null && _client.Connected;

    public string HostNick => _hostNick;
    public string ClientNick => _clientNick;

    [Header("Game Settings")]
    public int totalRounds = 5;

    private TcpListener _listener;
    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private CancellationTokenSource _cts;

    private string _hostNick;
    private string _clientNick;

    // Estado do jogo no servidor
    private int _currentRound = 1;
    private int _hostScore = 0;
    private int _clientScore = 0;
    private string _hostMove = null;
    private string _clientMove = null;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public string GetLocalLanIp()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ip)) continue;
                string s = ip.ToString();
                if (s.StartsWith("169.254.")) continue;
                return s;
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public void StartHost(string hostNick)
    {
        StopHost();

        _hostNick = hostNick;
        _clientNick = null;
        ResetMatchState();

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();

        FireStatus($"[HOST] A ouvir em {GetLocalLanIp()}:{Port}");
        FirePlayers();

        _ = AcceptLoopAsync(_cts.Token);
    }

    public void StopHost()
    {
        try { _cts?.Cancel(); } catch { }

        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }

        _cts = null;
        _writer = null;
        _reader = null;
        _client = null;
        _listener = null;

        _clientNick = null;
        ResetMatchState();
    }

    public async void StartGame()
    {
        if (!IsHosting)
        {
            FireStatus("[HOST] Não estás em modo Host.");
            return;
        }

        if (!HasClient || _writer == null)
        {
            FireStatus("[HOST] Precisas de 2 jogadores ligados para começar.");
            return;
        }

        try
        {
            await _writer.WriteLineAsync("START_GAME");
            FireStatus("[HOST] START_GAME enviado.");
            OnStartGame?.Invoke();
        }
        catch (Exception e)
        {
            FireStatus("[HOST] Erro ao enviar START_GAME: " + e.Message);
        }
    }

    // ✅ Host submete a jogada (chamado pelo UI na Scene Game)
    public void SubmitHostMove(string move)
    {
        if (!IsHosting) return;
        if (!HasClient) { FireStatus("[HOST] Ainda não há cliente."); return; }
        if (_currentRound > totalRounds) return;
        if (_hostMove != null) return; // já escolheu esta ronda

        _hostMove = NormalizeMove(move);
        FireStatus($"[HOST] Jogada registada: {_hostMove}");
        TryResolveRound();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            FireStatus("[HOST] À espera de cliente...");

            _client = await _listener.AcceptTcpClientAsync();
            _client.NoDelay = true;

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };

            FireStatus("[HOST] Cliente ligado. A aguardar HELLO...");
            _ = ReadLoopAsync(ct);

            await SendLobbyAsync(ct);
        }
        catch (ObjectDisposedException) { }
        catch (Exception e)
        {
            FireStatus("[HOST] Erro accept: " + e.Message);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _client != null && _client.Connected)
            {
                string line = await _reader.ReadLineAsync();
                if (line == null) throw new IOException("Ligação terminada.");

                line = line.Trim();

                if (line.StartsWith("HELLO|", StringComparison.Ordinal))
                {
                    _clientNick = line.Substring(6).Trim();
                    FireStatus($"[HOST] HELLO recebido: {_clientNick}");
                    FirePlayers();
                    await SendLobbyAsync(ct);
                    continue;
                }

                if (line.StartsWith("MOVE|", StringComparison.Ordinal))
                {
                    if (_currentRound > totalRounds) continue;
                    if (_clientMove != null) continue; // já escolheu esta ronda

                    string move = line.Substring(5).Trim();
                    _clientMove = NormalizeMove(move);
                    FireStatus($"[HOST] Jogada do cliente: {_clientMove}");
                    TryResolveRound();
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            FireStatus("[HOST] Cliente saiu: " + e.Message);
            CleanupClientOnly();
            FirePlayers();
        }
    }

    private void CleanupClientOnly()
    {
        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }

        _writer = null;
        _reader = null;
        _client = null;
        _clientNick = null;

        // se o cliente saiu, limpa jogadas pendentes
        _clientMove = null;
    }

    private async Task SendLobbyAsync(CancellationToken ct)
    {
        if (_writer == null) return;

        string clientShown = string.IsNullOrWhiteSpace(_clientNick) ? "-" : _clientNick;
        await _writer.WriteLineAsync($"LOBBY|{_hostNick}|{clientShown}");
    }

    private void TryResolveRound()
    {
        if (!HasClient) return;
        if (_hostMove == null || _clientMove == null) return;

        string winner = ComputeWinner(_hostMove, _clientMove); // HOST / CLIENT / DRAW
        if (winner == "HOST") _hostScore++;
        else if (winner == "CLIENT") _clientScore++;

        bool gameOver = (_currentRound >= totalRounds);

        // RESULT|round|total|hostNick|clientNick|hostMove|clientMove|winner|hostScore|clientScore|gameOver(0/1)
        string resultLine =
            $"RESULT|{_currentRound}|{totalRounds}|{_hostNick}|{_clientNick}|{_hostMove}|{_clientMove}|{winner}|{_hostScore}|{_clientScore}|{(gameOver ? 1 : 0)}";

        // Host UI
        OnResultLine?.Invoke(resultLine);

        // Cliente via TCP
        _ = SendToClientAsync(resultLine);

        if (!gameOver)
        {
            _currentRound++;
            _hostMove = null;
            _clientMove = null;
        }
        else
        {
            FireStatus("[HOST] Fim do jogo.");
        }
    }

    private async Task SendToClientAsync(string line)
    {
        try
        {
            if (_writer != null)
                await _writer.WriteLineAsync(line);
        }
        catch { }
    }

    private void ResetMatchState()
    {
        _currentRound = 1;
        _hostScore = 0;
        _clientScore = 0;
        _hostMove = null;
        _clientMove = null;
    }

    private static string NormalizeMove(string m)
    {
        m = (m ?? "").Trim();

        if (m.Equals("Rock", StringComparison.OrdinalIgnoreCase)) return "Pedra";
        if (m.Equals("Paper", StringComparison.OrdinalIgnoreCase)) return "Papel";
        if (m.Equals("Scissors", StringComparison.OrdinalIgnoreCase)) return "Tesoura";

        // Aceitar já em PT:
        if (m.Equals("Pedra", StringComparison.OrdinalIgnoreCase)) return "Pedra";
        if (m.Equals("Papel", StringComparison.OrdinalIgnoreCase)) return "Papel";
        if (m.Equals("Tesoura", StringComparison.OrdinalIgnoreCase)) return "Tesoura";

        return m; // fallback (para debug)
    }

    private static string ComputeWinner(string hostMove, string clientMove)
    {
        if (hostMove == clientMove) return "DRAW";

        bool hostWins =
            (hostMove == "Pedra" && clientMove == "Tesoura") ||
            (hostMove == "Tesoura" && clientMove == "Papel") ||
            (hostMove == "Papel" && clientMove == "Pedra");

        return hostWins ? "HOST" : "CLIENT";
    }

    private void FirePlayers()
    {
        string[] p = new string[]
        {
            string.IsNullOrWhiteSpace(_hostNick) ? "-" : _hostNick,
            string.IsNullOrWhiteSpace(_clientNick) ? null : _clientNick
        };
        OnPlayers?.Invoke(p);
    }

    private void FireStatus(string s) => OnStatus?.Invoke(s);
}

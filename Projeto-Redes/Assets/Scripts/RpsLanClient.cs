using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RpsLanClient : MonoBehaviour
{
    public event Action<string[]> OnPlayers;
    public event Action<string> OnStatus;
    public event Action OnStartGame;

    // ✅ Resultado do jogo (linha RESULT|...)
    public event Action<string> OnResultLine;

    public bool IsConnected => _tcp != null && _tcp.Connected;
    public string MyNick => _nick;

    private TcpClient _tcp;
    private StreamReader _reader;
    private StreamWriter _writer;
    private CancellationTokenSource _cts;

    private string _nick;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public async void Connect(string hostIp, string nick)
    {
        await ConnectAsync(hostIp, nick);
    }

    public async Task<bool> ConnectAsync(string hostIp, string nick)
    {
        Disconnect();

        _nick = nick;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(hostIp, RpsLanServer.Port);
            _tcp.NoDelay = true;

            var stream = _tcp.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };

            FireStatus($"[CLIENT] Ligado a {hostIp}:{RpsLanServer.Port}");

            await _writer.WriteLineAsync($"HELLO|{_nick}");
            _ = ReadLoopAsync(ct);

            return true;
        }
        catch (Exception e)
        {
            FireStatus("[CLIENT] Erro a ligar: " + e.Message);
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }

        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _tcp?.Close(); } catch { }

        _cts = null;
        _writer = null;
        _reader = null;
        _tcp = null;
    }

    // ✅ Cliente submete jogada
    public async void SubmitMove(string move)
    {
        try
        {
            if (_writer == null || !IsConnected) return;
            await _writer.WriteLineAsync($"MOVE|{move}");
        }
        catch (Exception e)
        {
            FireStatus("[CLIENT] Erro a enviar MOVE: " + e.Message);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _tcp != null && _tcp.Connected)
            {
                string line = await _reader.ReadLineAsync();
                if (line == null) throw new IOException("Ligação terminada.");

                line = line.Trim();

                if (line == "START_GAME")
                {
                    OnStartGame?.Invoke();
                    continue;
                }

                if (line.StartsWith("LOBBY|", StringComparison.Ordinal))
                {
                    string payload = line.Substring(6);
                    string[] parts = payload.Split('|');

                    string host = parts.Length > 0 ? parts[0] : "-";
                    string client = parts.Length > 1 ? parts[1] : "-";
                    if (client == "-") client = _nick;

                    OnPlayers?.Invoke(new[] { host, client });
                    continue;
                }

                if (line.StartsWith("RESULT|", StringComparison.Ordinal))
                {
                    OnResultLine?.Invoke(line);
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            FireStatus("[CLIENT] Desligado: " + e.Message);
            Disconnect();
        }
    }

    private void FireStatus(string s) => OnStatus?.Invoke(s);
}

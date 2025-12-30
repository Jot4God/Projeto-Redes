using System;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RPSGameUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text resultText;
    public TMP_Text roundText;

    [Header("Buttons Root (optional)")]
    public GameObject buttonsRoot; // parent dos 3 botões para bloquear/desbloquear

    [Header("Scene Names")]
    public string lobbySceneName = "LobbyScene";

    private RpsLanServer _server;
    private RpsLanClient _client;

    private bool _isHost;
    private bool _pickedThisRound;

    // ✅ Recebe resultados de threads de rede sem mexer na UI diretamente
    private readonly ConcurrentQueue<string> _pendingResults = new();

    private void Start()
    {
        _server = FindFirstObjectByType<RpsLanServer>();
        _client = FindFirstObjectByType<RpsLanClient>();

        _isHost = (_server != null && _server.IsHosting);

        if (_isHost)
        {
            _server.OnResultLine += EnqueueResult;
            SetText($"És HOST ({_server.HostNick}). Escolhe uma opção.", "Ronda 1");
        }
        else
        {
            if (_client == null || !_client.IsConnected)
            {
                SetText("Não estás ligado à rede. Volta ao lobby.", "-");
                EnableChoices(false);
                return;
            }

            _client.OnResultLine += EnqueueResult;
            SetText($"És CLIENT ({_client.MyNick}). Escolhe uma opção.", "Ronda 1");
        }

        _pickedThisRound = false;
        EnableChoices(true);
    }

    private void OnDestroy()
    {
        if (_server != null) _server.OnResultLine -= EnqueueResult;
        if (_client != null) _client.OnResultLine -= EnqueueResult;
    }

    private void Update()
    {
        // ✅ Processa resultados na main thread
        while (_pendingResults.TryDequeue(out var line))
        {
            ApplyResultLine(line);
        }
    }

    private void EnqueueResult(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            _pendingResults.Enqueue(line);
    }

    public void Choose(string choice)
    {
        if (_pickedThisRound) return;

        _pickedThisRound = true;
        EnableChoices(false);

        if (_isHost)
        {
            _server.SubmitHostMove(choice);
            resultText.text = $"Escolheste {choice}. À espera do CLIENT...";
        }
        else
        {
            _client.SubmitMove(choice);
            resultText.text = $"Escolheste {choice}. À espera do HOST...";
        }
    }

    // RESULT|round|total|hostNick|clientNick|hostMove|clientMove|winner|hostScore|clientScore|gameOver
    private void ApplyResultLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 11) return;

        int round = int.Parse(parts[1]);
        int total = int.Parse(parts[2]);

        string hostNick = parts[3];
        string clientNick = parts[4];
        string hostMove = parts[5];
        string clientMove = parts[6];
        string winner = parts[7];
        int hostScore = int.Parse(parts[8]);
        int clientScore = int.Parse(parts[9]);
        bool gameOver = parts[10] == "1";

        string winnerText =
            winner == "DRAW" ? "Empate!" :
            winner == "HOST" ? $"{hostNick} ganha a ronda!" :
            $"{clientNick} ganha a ronda!";

        if (roundText) roundText.text = $"Ronda {round}/{total}";

        if (resultText)
        {
            resultText.text =
                $"Ronda {round}/{total}\n\n" +
                $"{hostNick}: {hostMove}\n" +
                $"{clientNick}: {clientMove}\n\n" +
                $"{winnerText}\n\n" +
                $"Pontuação: {hostScore} - {clientScore}";
        }

        if (gameOver)
        {
            string final =
                hostScore > clientScore ? $"{hostNick} vence o jogo!" :
                clientScore > hostScore ? $"{clientNick} vence o jogo!" :
                "Empate final!";

            if (resultText) resultText.text += $"\n\nFIM DO JOGO!\n{final}";
            EnableChoices(false);
        }
        else
        {
            _pickedThisRound = false;
            EnableChoices(true);
            if (resultText) resultText.text += "\n\nEscolhe uma opção para a próxima ronda.";
        }
    }

    public void BackToLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    private void EnableChoices(bool enabled)
    {
        if (buttonsRoot != null)
            buttonsRoot.SetActive(enabled);
    }

    private void SetText(string msg, string round)
    {
        if (resultText) resultText.text = msg;
        if (roundText) roundText.text = round;
    }
}

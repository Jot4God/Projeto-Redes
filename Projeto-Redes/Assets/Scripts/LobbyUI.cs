using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelMainMenu;
    public GameObject panelLobby;

    [Header("Main Menu UI")]
    public TMP_InputField nicknameInput;

    [Header("Lobby UI")]
    public TMP_InputField roomCodeInput; 
    public TMP_Text statusText;
    public TMP_Text playersText;

    [Header("Lobby Buttons")]
    public GameObject startGameButton;

    [Header("LAN Networking (TCP)")]
    public RpsLanServer lanServer;
    public RpsLanClient lanClient;

    [Header("Scene")]
    public string gameSceneName = "Game";

    private string _nickname;
    private bool _inLobby;

    private Action _startGameAction;
    private bool _networkPinned;

    private void Start()
    {
        ShowMainMenu();
        SetStatus("Pronto.");
        SetPlayers("-");

        _startGameAction = () => MainThreadDispatcher.Post(() =>
        {
            SetStatus("A entrar no jogo...");
            SceneManager.LoadScene(gameSceneName);
        });

        if (lanServer != null)
        {
            lanServer.OnStatus += (msg) => MainThreadDispatcher.Post(() => SetStatus(msg));
            lanServer.OnPlayers += (p) => MainThreadDispatcher.Post(() => UpdatePlayersUI(p));
            lanServer.OnStartGame += _startGameAction;
        }

        if (lanClient != null)
        {
            lanClient.OnStatus += (msg) => MainThreadDispatcher.Post(() => SetStatus(msg));
            lanClient.OnPlayers += (p) => MainThreadDispatcher.Post(() => UpdatePlayersUI(p));
            lanClient.OnStartGame += _startGameAction;
        }

        if (startGameButton) startGameButton.SetActive(false);
    }

    private void OnDestroy()
    {
        if (lanServer != null) lanServer.OnStartGame -= _startGameAction;
        if (lanClient != null) lanClient.OnStartGame -= _startGameAction;
    }

    private void ShowMainMenu()
    {
        _inLobby = false;
        if (panelMainMenu) panelMainMenu.SetActive(true);
        if (panelLobby) panelLobby.SetActive(false);
    }

    private void ShowLobby()
    {
        _inLobby = true;
        if (panelMainMenu) panelMainMenu.SetActive(false);
        if (panelLobby) panelLobby.SetActive(true);
    }

    public void OnClickPlay()
    {
        _nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(_nickname))
        {
            SetStatus("Escreve um nickname para continuar.");
            return;
        }

        ShowLobby();
        SetStatus($"Bem-vindo, {_nickname}. Cria uma sala (Host) ou entra com o IP do Host.");
        SetPlayers("Aguardando...");
        if (startGameButton) startGameButton.SetActive(false);
    }

    public void OnClickBack()
    {
        if (lanClient != null) lanClient.Disconnect();
        if (lanServer != null) lanServer.StopHost();

        ShowMainMenu();
        SetStatus("Voltaste ao menu.");
        SetPlayers("-");
        if (startGameButton) startGameButton.SetActive(false);
    }

    public void OnClickCreateRoom()
    {
        if (!_inLobby) return;
        if (lanServer == null)
        {
            SetStatus("RpsLanServer não está ligado no Inspector.");
            return;
        }

        PinNetworkAcrossScenes();

        lanServer.StartHost(_nickname);

        string ip = lanServer.GetLocalLanIp();
        if (roomCodeInput) roomCodeInput.text = ip;

        SetStatus($"Host ativo. Partilha este IP: {ip}");
        UpdatePlayersUI(new[] { _nickname, (string)null });

        // Mostra quando o cliente entrar (UpdatePlayersUI também controla)
        if (startGameButton) startGameButton.SetActive(false);
    }

    public void OnClickJoinRoom()
    {
        if (!_inLobby) return;

        string ip = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Escreve o IP do Host para entrar (ex: 192.168.1.23).");
            return;
        }

        if (lanClient == null)
        {
            SetStatus("RpsLanClient não está ligado no Inspector.");
            return;
        }

        PinNetworkAcrossScenes();

        SetStatus("A ligar ao Host...");
        lanClient.Connect(ip, _nickname);

        if (startGameButton) startGameButton.SetActive(false);
    }

    public void OnClickStartGame()
    {
        if (lanServer == null || !lanServer.IsHosting)
        {
            SetStatus("Só o Host pode iniciar o jogo.");
            return;
        }

        if (!lanServer.HasClient)
        {
            SetStatus("Ainda falta o 2º jogador ligar.");
            return;
        }

        lanServer.StartGame();
    }

    private void UpdatePlayersUI(string[] p)
    {
        string host = (p != null && p.Length > 0) ? p[0] : "-";
        string client = (p != null && p.Length > 1) ? p[1] : null;

        if (string.IsNullOrWhiteSpace(host)) host = "-";

        if (string.IsNullOrWhiteSpace(client))
            SetPlayers($"[HOST] {host}\n(à espera do 2º jogador)");
        else
            SetPlayers($"[HOST] {host}\n[CLIENT] {client}");

        // ✅ Botão Start: só no Host e só quando já há cliente
        if (startGameButton != null)
        {
            bool show = lanServer != null && lanServer.IsHosting && lanServer.HasClient;
            startGameButton.SetActive(show);
        }
    }

    private void PinNetworkAcrossScenes()
    {
        if (_networkPinned) return;

        GameObject networkRoot = null;
        if (lanServer != null) networkRoot = lanServer.gameObject;
        else if (lanClient != null) networkRoot = lanClient.gameObject;

        if (networkRoot != null)
        {
            DontDestroyOnLoad(networkRoot);
            _networkPinned = true;
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log(msg);
    }

    private void SetPlayers(string msg)
    {
        if (playersText) playersText.text = msg;
    }
}

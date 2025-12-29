using System;
using TMPro;
using UnityEngine;

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

    // Estado offline (simulação) — depois substituis pelo estado real vindo do servidor
    private string _nickname;
    private string _currentRoomCode;
    private bool _inLobby;

    private void Start()
    {
        ShowMainMenu();
        SetStatus("Pronto.");
        SetPlayers("-");
    }

    // -------------------------
    // Navegação de Panels
    // -------------------------
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

    // -------------------------
    // BOTÃO: Jogar
    // -------------------------
    public void OnClickPlay()
    {
        _nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(_nickname))
        {
            SetStatus("Escreve um nickname para continuar.");
            return;
        }

        ShowLobby();
        SetStatus($"Bem-vindo, {_nickname}. Cria uma sala ou entra com um código.");
        SetPlayers("Aguardando...");
    }

    // -------------------------
    // BOTÃO: Voltar
    // -------------------------
    public void OnClickBack()
    {
        // No futuro, aqui também envias "leave_room" no TCP
        _currentRoomCode = null;
        ShowMainMenu();
        SetStatus("Voltaste ao menu.");
        SetPlayers("-");
    }

    // -------------------------
    // BOTÃO: Criar Sala (offline simulado)
    // -------------------------
    public void OnClickCreateRoom()
    {
        if (!_inLobby) return;

        // FUTURO TCP: enviar {"type":"create_room"}
        // POR AGORA: simulação
        _currentRoomCode = UnityEngine.Random.Range(10000, 99999).ToString();
        if (roomCodeInput) roomCodeInput.text = _currentRoomCode;

        SetStatus($"Sala criada: {_currentRoomCode}. Partilha este código com o outro jogador.");
        SetPlayers($"[HOST] {_nickname} - NOT READY\n(à espera do 2º jogador)");
    }

    // -------------------------
    // BOTÃO: Entrar (offline simulado)
    // -------------------------
    public void OnClickJoinRoom()
    {
        if (!_inLobby) return;

        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Escreve o código da sala para entrar.");
            return;
        }

        // FUTURO TCP: enviar {"type":"join_room","code":"..."}
        // POR AGORA: simulação
        _currentRoomCode = code;
        SetStatus($"Tentativa de entrar na sala: {code} (simulado).");
        SetPlayers($"{_nickname} - NOT READY\n(sem rede ainda)");
    }

    // -------------------------
    // Helpers UI
    // -------------------------
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

using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RPSGameUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text resultText;
    public TMP_Text roundText; // Novo: mostra a ronda atual

    private string player1Choice = "";
    private string player2Choice = "";
    private bool player1Ready = false;
    private bool player2Ready = false;
    private bool isPlayer1Turn = true;

    private int player1Score = 0;
    private int player2Score = 0;
    private int currentRound = 1;
    private int totalRounds = 5;

    // -------------------------
    // Escolha de cada jogador
    // -------------------------
    public void Choose(string choice)
    {
        if (currentRound > totalRounds)
            return; // jogo acabou

        if (isPlayer1Turn && !player1Ready)
        {
            player1Choice = choice;
            player1Ready = true;
            isPlayer1Turn = false;
            resultText.text = "Jogador 2, escolhe a tua opção!";
        }
        else if (!isPlayer1Turn && !player2Ready)
        {
            player2Choice = choice;
            player2Ready = true;
            CheckBothChoices();
        }
    }

    // -------------------------
    // Calcula resultado da ronda
    // -------------------------
    private void CheckBothChoices()
    {
        string result;

        if (player1Choice == player2Choice)
            result = "Empate!";
        else if (
            (player1Choice == "Pedra" && player2Choice == "Tesoura") ||
            (player1Choice == "Tesoura" && player2Choice == "Papel") ||
            (player1Choice == "Papel" && player2Choice == "Pedra")
        )
        {
            result = "Jogador 1 ganha a ronda!";
            player1Score++;
        }
        else
        {
            result = "Jogador 2 ganha a ronda!";
            player2Score++;
        }

        resultText.text =
            $"Ronda {currentRound}/{totalRounds}\n" +
            $"Jogador 1: {player1Choice}\n" +
            $"Jogador 2: {player2Choice}\n\n" +
            result +
            $"\n\nPontuação: {player1Score} - {player2Score}";

        // Próxima ronda ou fim do jogo
        currentRound++;
        if (currentRound > totalRounds)
        {
            ShowFinalWinner();
        }
        else
        {
            ResetRoundForNext();
        }
    }

    // -------------------------
    // Reset para próxima ronda
    // -------------------------
    private void ResetRoundForNext()
    {
        player1Choice = "";
        player2Choice = "";
        player1Ready = false;
        player2Ready = false;
        isPlayer1Turn = true;
        roundText.text = $"Ronda {currentRound}/{totalRounds}";
        resultText.text += "\n\nJogador 1, escolhe a tua opção!";
    }

    // -------------------------
    // Mostra vencedor final
    // -------------------------
    private void ShowFinalWinner()
    {
        string winner;
        if (player1Score > player2Score) winner = "Jogador 1 vence o jogo!";
        else if (player2Score > player1Score) winner = "Jogador 2 vence o jogo!";
        else winner = "Empate!";

        resultText.text += $"\n\nFIM DO JOGO!\n{winner}";
    }

    // -------------------------
    // Voltar ao Lobby
    // -------------------------
    public void BackToLobby()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    // -------------------------
    // Reset manual do jogo
    // -------------------------
    public void ResetGame()
    {
        player1Score = 0;
        player2Score = 0;
        currentRound = 1;
        ResetRoundForNext();
    }
}

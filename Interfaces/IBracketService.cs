// Interfaces/IBracketService.cs
namespace EsportsTournament.Interfaces // <--- Adjust namespace if needed
{
    public interface IBracketService
    {
        Task GenerateBracket(int tournamentId);
        Task UpdateMatch(int matchId, int winnerId);
        // Add any other methods your BracketService will have
    }
}
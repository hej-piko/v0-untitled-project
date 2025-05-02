using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class Match
    {
        public int Id { get; set; }
        
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        //public DateTime? MatchDate { get; set; }

        //public string? TeamA { get; set; }

        //public string? TeamB { get; set; }

        //public int? ScoreA { get; set; }
        //public int? ScoreB { get; set; }

        
        public int? Participant1Id { get; set; }
        public virtual Participant Participant1 { get; set; }
        
        public int? Participant2Id { get; set; }
        public virtual Participant Participant2 { get; set; }
        
        public int? WinnerId { get; set; }
        public virtual Participant Winner { get; set; }
        
        public int Round { get; set; }
        
        public int MatchNumber { get; set; }
        
        public bool IsCompleted { get; set; } = false;


        // ✅ Helper properties to default nulls to 0
        public int Participant1SafeId => Participant1Id ?? 0;
        public int Participant2SafeId => Participant2Id ?? 0;
        public int WinnerSafeId => WinnerId ?? 0;
    }
}

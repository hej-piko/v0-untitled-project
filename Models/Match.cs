using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class Match
    {
        public int Id { get; set; }
        
        [Required]
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        
        public int? Participant1Id { get; set; }
        public virtual Participant Participant1 { get; set; }
        
        public int? Participant2Id { get; set; }
        public virtual Participant Participant2 { get; set; }
        
        public int? WinnerId { get; set; }
        public virtual Participant Winner { get; set; }
        
        public int Round { get; set; }
        
        public int MatchNumber { get; set; }
        
        public bool IsCompleted { get; set; } = false;
    }
}

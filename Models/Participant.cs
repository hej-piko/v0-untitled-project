using System;
using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class Participant
    {
        public int Id { get; set; }
        
        [Required]
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        
        [Required]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }
        
        public int? Seed { get; set; }
        
        public DateTime JoinedAt { get; set; } = DateTime.Now;
    }
}

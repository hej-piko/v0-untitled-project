using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class Tournament
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        public string Game { get; set; }
        
        public string Description { get; set; }
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public int MaxParticipants { get; set; }
        
        public bool IsOpen { get; set; } = true;
        
        [Required]
        public string CreatorId { get; set; }
        public virtual ApplicationUser Creator { get; set; }
        
        public virtual ICollection<Participant> Participants { get; set; }
        public virtual ICollection<Match> Matches { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class Tournament
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tournament name is required")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Game is required")]
        public string Game { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Max participants is required")]
        [Range(1, 1000, ErrorMessage = "Max participants must be between 1 and 1000")]
        public int MaxParticipants { get; set; }

        [Required(ErrorMessage = "Creator ID is required")]
        public string CreatorId { get; set; }
        public virtual AspNetUsers Creator { get; set; }

        public bool IsOpen { get; set; }

        public ICollection<Match>? Matches { get; set; }

        public ICollection<Participant>? Participants { get; set; }
    }
}

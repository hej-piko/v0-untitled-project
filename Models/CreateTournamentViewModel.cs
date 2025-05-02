// CreateTournamentViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class CreateTournamentViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "Tournament name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; }

        [Required]
        public string Game { get; set; }  // Dropdown or text input for the game type

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Max participants must be between 1 and 100.")]
        public int MaxParticipants { get; set; }

        [Required]
        public bool IsOpen { get; set; }  // Checkbox for whether the tournament is open for registration
    }

}

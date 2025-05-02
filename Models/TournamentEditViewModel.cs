using System.ComponentModel.DataAnnotations;

namespace EsportsTournament.Models
{
    public class TournamentEditViewModel
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string Game { get; set; }
        public string Description { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; }
        public int MaxParticipants { get; set; }
        public bool IsOpen { get; set; }
    }

}

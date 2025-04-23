using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace EsportsTournament.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }
        public virtual ICollection<Tournament> CreatedTournaments { get; set; }
        public virtual ICollection<Participant> Participations { get; set; }
    }
}

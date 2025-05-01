
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace EsportsTournament.Models
{
    public class AspNetUsers : IdentityUser
    {
        // Custom properties for your application
        public string DisplayName { get; set; } // Custom field for user display name

        // Navigation properties (relations)
        public virtual ICollection<Tournament> CreatedTournaments { get; set; } // Tournaments created by the user
        public virtual ICollection<Participant> Participations { get; set; } // Tournaments the user has participated in
    }
}

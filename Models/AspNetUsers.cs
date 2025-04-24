using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EsportsTournament.Models
{
    public class AspNetUsers : IdentityUser
    {
        //[Key]
        public string Id { get; set; }
        //[Column("UserName")]
        public string UserName { get; set; }

        //[Column("NormalizedUserName")]
        public string NormalizedUserName { get; set; }
        //[Column("Email")]
        public string Email { get; set; }
        //[Column("NormalizedEmail")]
        public string NormalizedEmail { get; set; }
        //[Column("EmailConfirmed")]
        public bool EmailConfirmed { get; set; }
        //[Column("PasswordHash")]
        public string PasswordHash { get; set; }
        //[Column("SecurityStamp")]
        public string SecurityStamp { get; set; }
        //[Column("ConcurrencyStamp")]
        public string ConcurrencyStamp { get; set; }
        //[Column("PhoneNumber")]
        public string PhoneNumber { get; set; }
        //[Column("PhoneNumberConfirmed")]
        public bool PhoneNumberConfirmed { get; set; }

        //[Column("TwoFactorEnabled")]
        public bool TwoFactorEnabled { get; set; }
        //[Column("LockoutEnd")]
        public DateTime? LockoutEnd { get; set; }
        //[Column("LockoutEnabled")]
        public bool LockoutEnabled { get; set; }
        //[Column("AccessFailedCount")]
        public int AccessFailedCount { get; set; }
        //[Column("DisplayName")]
        public string DisplayName { get; set; }

        public virtual ICollection<Tournament> CreatedTournaments { get; set; }
        public virtual ICollection<Participant> Participations { get; set; }
    }
}

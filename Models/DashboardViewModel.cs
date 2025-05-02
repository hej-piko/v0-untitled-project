using System.Collections.Generic;

namespace EsportsTournament.Models
{
    public class DashboardViewModel
    {
        public List<Tournament> HostedTournaments { get; set; }
        public List<Tournament> JoinedTournaments { get; set; }

        public DashboardViewModel()
        {
            HostedTournaments = new List<Tournament>();
            JoinedTournaments = new List<Tournament>();
        }
    }
}

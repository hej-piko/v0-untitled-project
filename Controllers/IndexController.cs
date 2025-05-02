using Microsoft.AspNetCore.Mvc;

namespace EsportsTournament.Controllers
{
    public class IndexController : Controller
    {
        public IActionResult Landing()
        {
            return View();
        }
    }
}

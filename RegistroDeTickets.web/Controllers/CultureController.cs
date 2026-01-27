using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;

namespace RegistroDeTickets.web.Controllers
{
    public class CultureController : Controller
    {
        [HttpPost]
        public IActionResult SetIdioma(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            
            //if (User.Identity.IsAuthenticated)
            //{
            //}

            return LocalRedirect(returnUrl);
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}

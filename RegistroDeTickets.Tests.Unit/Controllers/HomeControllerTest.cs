using Microsoft.AspNetCore.Mvc;
using RegistroDeTickets.web.Controllers;

namespace RegistroDeTickets.Tests.Unit.Controllers
{
    public class HomeControllerTests
    {
        [Fact]
        public void Index_DebeRetornarLaVistaPredeterminada()
        {
            var controller = new HomeController();

            var result = controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Null(viewResult.ViewName);
        }
    }
}

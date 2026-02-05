using Microsoft.AspNetCore.Mvc;
using RegistroDeTickets.web.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RegistroDeTickets.Data.Entidades;
using RegistroDeTickets.Service;
using RegistroDeTickets.web.Controllers;

namespace RegistroDeTickets.Tests.Unit.Controllers
{
    public class UsuarioControllerTest
    {
        private readonly Mock<IUsuarioService> _usuarioServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<ITelemetryService> _telemetryServiceMock;
        private readonly Mock<IPasswordHasher<Usuario>> _passwordHasherMock;
        private readonly UserManager<Usuario> _userManager;

        private readonly UsuarioController _controller;

        public UsuarioControllerTest()
        {
            _usuarioServiceMock = new Mock<IUsuarioService>();
            _emailServiceMock = new Mock<IEmailService>();
            _tokenServiceMock = new Mock<ITokenService>();
            _telemetryServiceMock = new Mock<ITelemetryService>();
            _passwordHasherMock = new Mock<IPasswordHasher<Usuario>>();

            _userManager = CrearUserManagerMock();

            _controller = new UsuarioController(
                _usuarioServiceMock.Object,
                _emailServiceMock.Object,
                _tokenServiceMock.Object,
                _telemetryServiceMock.Object,
                _userManager,
                _passwordHasherMock.Object
            );
        }

        [Fact]
        public void Registrar_Get_DebeRetornarVistaDeRegistro()
        {
            var result = _controller.Registrar();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Null(viewResult.ViewName);
        }

        private static UserManager<Usuario> CrearUserManagerMock()
        {
            var store = new Mock<IUserStore<Usuario>>();

            return new UserManager<Usuario>(
                store.Object,null,null,null,null,null,null,null,null
            );
        }

        [Fact]
        public void IniciarSesion_RetornaVistaConGoogleClientId()
        {
            string esperado = "cliente-id-google-123";
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", esperado);

            var result = _controller.IniciarSesion();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(esperado, viewResult.ViewData["GoogleClientId"]);
        }

        [Fact]
        public void IniciarSesion_VariableEntornoNoConfigurada_RetornaViewBagNulo()
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", null);

            var result = _controller.IniciarSesion();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Null(viewResult.ViewData["GoogleClientId"]);
        }
     
    }
}
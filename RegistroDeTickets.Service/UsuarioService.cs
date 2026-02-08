using RegistroDeTickets.Data.Entidades;
using RegistroDeTickets.Repository;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace RegistroDeTickets.Service
{
    public interface IUsuarioService
    {
        void AgregarUsuario(Usuario usuario);

        Task<string> RegistrarUsuario(string username, string email, string password);

        Task<(bool exito, string mensajeError, string token, string rol)> IniciarSesion(string email, string password);

        List<Usuario> ObtenerUsuarios();

        void EditarUsuario(Usuario usuario);

        void EliminarUsuario(Usuario usuario);

        Usuario BuscarPorEmail(string email);

        Task<bool> EnviarEmailRecuperacionAsync(string email, string esquema, string host);

        bool RestablecerContrasenia(string email, string token, string nuevaContrasenia);

        void DesignarUsuarioComoTecnico(Usuario usuario);


        List<Usuario> ObtenerTecnicos();

        Usuario ObtenerUsuarioPorId(int id);

        string renombrarUsuarioGoogle(string email, string nombreCompleto);

    }

    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly ITelemetryService _telemetryService;
        private readonly ITokenService _tokenService;   
        private readonly UserManager<Usuario> _userManager;


        public UsuarioService(IUsuarioRepository usuarioRepository, IPasswordHasher<Usuario> passwordHasher, IEmailService emailService, ITelemetryService telemetryService, ITokenService tokenService, UserManager<Usuario> userManager)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _telemetryService = telemetryService;
            _tokenService = tokenService;
            _userManager = userManager;
        }

        public void AgregarUsuario(Usuario usuario)
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(usuario, usuario.PasswordHash);

            _usuarioRepository.AgregarUsuario(usuario);
        }

        public async Task<string> RegistrarUsuario(string username, string email, string password)
        {
            var nuevoUsuario = crearUsuarioVM(username, email, password);

            var resultado = await _userManager.CreateAsync(nuevoUsuario, password);

            if(!resultado.Succeeded)
            {
                return string.Join(" ", resultado.Errors.Select(e => e.Description));
            }
            await _userManager.AddToRoleAsync(nuevoUsuario, "Cliente");

            return null;
        }

        private Usuario crearUsuarioVM(string username, string email, string password)
        {
            var nuevoUsuario = new Data.Entidades.Usuario
            {
                UserName = username,
                Email = email,
                Estado = "Activo",
                Cliente = new Cliente()

            };
            return nuevoUsuario;
        }

        public async Task<(bool exito, string mensajeError, string token, string rol)> IniciarSesion(
        string email,
        string password)
        {
            var usuarioEncontrado = BuscarPorEmail(email);

            if (usuarioEncontrado == null)
            {
                _telemetryService.RegistrarEvento("InicioSesionFallidoPorEmail", null);
                return (false, "Credenciales incorrectas. Intentalo nuevamente", null, null);
            }

            var resultadoVerificacion = _passwordHasher.VerifyHashedPassword(
                usuarioEncontrado,
                usuarioEncontrado.PasswordHash,
                password
            );

            if (resultadoVerificacion == PasswordVerificationResult.Failed)
            {
                _telemetryService.RegistrarEvento("InicioSesionFallidoPorContraseña", usuarioEncontrado);
                return (false, "Credenciales incorrectas. Intentalo nuevamente", null, null);
            }

            var rolesDelUsuario = await _userManager.GetRolesAsync(usuarioEncontrado);
            var claimsAdicionales = await _userManager.GetClaimsAsync(usuarioEncontrado);

            var token = _tokenService.GenerateToken(
                usuarioEncontrado.UserName,
                rolesDelUsuario,
                usuarioEncontrado.Id,
                claimsAdicionales
            );

            string rolPrincipal = DeterminarRolPrincipal(rolesDelUsuario);

            _telemetryService.RegistrarEvento("InicioSesionExitoso", usuarioEncontrado);

            return (true, null, token, rolPrincipal);
        }

        private string DeterminarRolPrincipal(IList<string> roles)
        {
            if (roles.Contains("Admin")) return "Admin";
            if (roles.Contains("Tecnico")) return "Tecnico";
            return "Cliente";
        }

        public List<Usuario> ObtenerUsuarios()
        {
            return _usuarioRepository.ObtenerUsuarios();
        }

        public void EditarUsuario(Usuario usuario)
        {
            _usuarioRepository.EditarUsuario(usuario);
        }
        
        public async Task<bool> EnviarEmailRecuperacionAsync(string email, string esquema, string host)
        {
            var usuario = _usuarioRepository.BuscarUsuarioPorEmail(email);
            if (usuario == null) return false;

            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            usuario.TokenHashRecuperacion = HashearToken(token);
            usuario.TokenHashRecuperacionExpiracion = DateTime.UtcNow.AddMinutes(30);
            _usuarioRepository.EditarUsuario(usuario);

            var link = $"{esquema}://{host}/Usuario/RestablecerContrasenia?email={email}&token={token}";

            await _emailService.EnviarEmailRecuperacion(email, link);

            return true;
        }

        public bool RestablecerContrasenia(string email, string token, string nuevaContrasenia)
        {
            var usuario = _usuarioRepository.BuscarUsuarioPorEmail(email);
            if (usuario == null)
            {
                return false;
            }

            var hashTokenRecibido = HashearToken(token);
            if (string.IsNullOrEmpty(usuario.TokenHashRecuperacion) || usuario.TokenHashRecuperacion != hashTokenRecibido)
            {
                return false;
            }

            usuario.PasswordHash = _passwordHasher.HashPassword(usuario, nuevaContrasenia);

            _usuarioRepository.EditarUsuario(usuario);

            return true;
        }

        private string HashearToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToHexString(bytes);
            }
        }

        public void EliminarUsuario(Usuario usuario)
        {
            _usuarioRepository.EliminarUsuario(usuario);
        }

        public Usuario BuscarPorEmail(string email)
        {
            return _usuarioRepository.BuscarPorEmail(email);
        }

        public string renombrarUsuarioGoogle(string email, string nombreCompleto)
        {
           
            return (nombreCompleto ?? email).Split(' ')[0]; ;
        }

        public Usuario ObtenerUsuarioPorId(int id)
        {
            return _usuarioRepository.ObtenerUsuarioPorId(id);
        }

        public void DesignarUsuarioComoTecnico(Usuario usuario)
        {
            if (usuario.Tecnico == null)
            {
                usuario.Tecnico = new Tecnico { IdNavigation = usuario };
                _usuarioRepository.AgregarTecnico(usuario.Tecnico);
                _usuarioRepository.EditarUsuario(usuario);
            }
        }

        public List<Usuario> ObtenerTecnicos()
        {
            return _usuarioRepository.ObtenerTecnicos();
        }
    }
}

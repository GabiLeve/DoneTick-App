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

        public UsuarioService(IUsuarioRepository usuarioRepository, IPasswordHasher<Usuario> passwordHasher, IEmailService emailService)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public void AgregarUsuario(Usuario usuario)
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(usuario, usuario.PasswordHash);

            _usuarioRepository.AgregarUsuario(usuario);
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

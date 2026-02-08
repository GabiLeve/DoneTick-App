using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RegistroDeTickets.Data.Entidades;
using RegistroDeTickets.Service;
using RegistroDeTickets.web.Models;
using Sprache;
using Usuario = RegistroDeTickets.Data.Entidades.Usuario;

namespace RegistroDeTickets.web.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly TokenService _tokenService;
        private readonly IUsuarioService _usuarioService;
        private readonly ITelemetryService _telemetryService;
        private string UsuarioE;
        private readonly UserManager<Usuario> _userManager;
        private readonly IPasswordHasher<Usuario> _passwordHasher;

        public UsuarioController(IUsuarioService usuarioService, TokenService tokenService, ITelemetryService telemetryService, UserManager<Usuario> userManager,
        IPasswordHasher<Usuario> passwordHasher)
        {
            _usuarioService = usuarioService;
            _tokenService = tokenService;
            _telemetryService = telemetryService;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
        }


        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult Registrar()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Registrar(UsuarioViewModel usuarioVM)
        {
            if (!ModelState.IsValid)
            {
                return View(usuarioVM);
            }

            string mensajeError = await _usuarioService.RegistrarUsuario(usuarioVM.Username, usuarioVM.Email, usuarioVM.PasswordHash);
            {
                if(string.IsNullOrEmpty(mensajeError))
                {
                    return RedirectToAction("IniciarSesion");
                }
                TempData["MensajeErrorE"] = mensajeError;
                return RedirectToAction("Registrar");
            }
        }
    
        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult IniciarSesion()
        {
            ViewBag.GoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> IniciarSesion(LoginViewModel usuario)
        {
            if (!ModelState.IsValid)
            {
                return View(usuario);
            }

            var (exito, mensajeError, token, rol) = await _usuarioService.IniciarSesion(
                usuario.Email,
                usuario.PasswordHash
            );

            if (!exito)
            {
                TempData["MensajeErrorE"] = mensajeError;
                return View(usuario);
            }

            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddHours(1)
            });

            return rol switch
            {
                "Tecnico" => RedirectToAction("Inicio", "Tecnico"),
                "Admin" => RedirectToAction("Inicio", "Administrador"),
                _ => RedirectToAction("Inicio", "Cliente")
            };
        }

        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult GoogleSignIn()
        {
            ViewBag.GoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            return RedirectToAction("IniciarSesion");
        }

        [HttpPost]
        public async Task<IActionResult> GoogleSignIn([FromBody] GoogleTokenDto data)
        {
            if (string.IsNullOrEmpty(data?.Credential))
                return BadRequest(new { success = false, message = "Token inválido o vacío." });

            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(
                    data.Credential,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") }
                    });

                var usuarioEncontradoPorMail = _usuarioService.BuscarPorEmail(payload.Email);

                // 1. VERIFICACIÓN DE EMAIL (Devuelve JSON)
                if (usuarioEncontradoPorMail != null)
                {   
                    _telemetryService.RegistrarEvento("InicioSesionExitoso", usuarioEncontradoPorMail);
                                       
                    var rolesDelUsuario = await _userManager.GetRolesAsync(usuarioEncontradoPorMail);
                    var claimsAdicionales = await _userManager.GetClaimsAsync(usuarioEncontradoPorMail);

                    TempData["UsuarioE"] = usuarioEncontradoPorMail.UserName;
                    /*usuarioEncontrado.UserName, rolesDelUsuario,usuarioEncontrado.Id,claimsAdicionales*/
                    var token = _tokenService.GenerateToken(usuarioEncontradoPorMail.UserName, rolesDelUsuario, usuarioEncontradoPorMail.Id, claimsAdicionales);
                    
                    Response.Cookies.Append("jwt", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.Now.AddHours(1)
                    });

                    return Ok(new
                    {
                        success = true,
                        redirectUrl = Url.Action("Listar", "Cliente")
                    });
                }

                string nombreUsuarioGoogle = _usuarioService.renombrarUsuarioGoogle(payload.Email, payload.Name);

                var nuevoUsuario = new Usuario
                {
                    UserName = nombreUsuarioGoogle,
                    Email = payload.Email,
                    Estado = "Activo",
                    Cliente = new Cliente()
                };

                // 2. CREACIÓN DE USUARIO (SIN PASSWORD)
                IdentityResult result = await _userManager.CreateAsync(nuevoUsuario);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(nuevoUsuario, "Cliente");

                    _telemetryService.RegistrarEvento("InicioSesionExitoso", nuevoUsuario);

                    var rolesDelUsuario = await _userManager.GetRolesAsync(nuevoUsuario);
                    var claimsAdicionales = await _userManager.GetClaimsAsync(nuevoUsuario);
                    TempData["UsuarioE"] = nuevoUsuario.UserName;
                    //var token = _tokenService.GenerateToken(usuarioEncontradoPorMail.UserName, rolesDelUsuario, usuarioEncontradoPorMail.Id, claimsAdicionales);
                    var token = _tokenService.GenerateToken(nuevoUsuario.UserName, rolesDelUsuario, nuevoUsuario.Id, claimsAdicionales);

                    Response.Cookies.Append("jwt", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.Now.AddHours(1)
                    });

                    // 3. RESPUESTA DE ÉXITO (Devuelve JSON)
                    return Ok(new
                    {
                        success = true,
                        redirectUrl = Url.Action("Listar", "Cliente")
                    });
                }
                else
                {
                    // 4. RESPUESTA DE ERROR (Devuelve JSON)
                    string errores = string.Join(" ", result.Errors.Select(e => e.Description));
                    return BadRequest(new { success = false, message = errores });
                }
            }
            catch (InvalidJwtException)
            {
                return BadRequest(new { success = false, message = "Token de Google inválido." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error interno del servidor", error = ex.Message });
            }
        }

        public IActionResult CerrarSesion()
        {
            Response.Cookies.Delete("jwt");
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return RedirectToAction("IniciarSesion", "Usuario");

        }

        public IActionResult Listar()
        {
            return RedirectToAction("Registrar");
        }

        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult SolicitarRecuperacion()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> SolicitarRecuperacion(SolicitarRecuperacionViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            await _usuarioService.EnviarEmailRecuperacionAsync(vm.Email, Request.Scheme, Request.Host.ToString());
            return RedirectToAction("SolicitarRecuperacionConfirmacion");
        }
       
        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult SolicitarRecuperacionConfirmacion()
        {
            return View();
        }

        [HttpGet]
        [AutoValidateAntiforgeryToken]
        public IActionResult RestablecerContrasenia(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("IniciarSesion");
            }

            var vm = new RestablecerContraseniaViewModel
            {
                Email = email,
                Token = token
            };
            return View(vm);
        }

        [HttpPost]
        public IActionResult RestablecerContrasenia(RestablecerContraseniaViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var exito = _usuarioService.RestablecerContrasenia(
                vm.Email,
                vm.Token,
                vm.NuevaContrasenia
            );

            if (exito)
            {
                TempData["MensajeExito"] = "¡Tu contraseña ha sido actualizada con éxito!";
                return RedirectToAction("IniciarSesion");
            }

            ModelState.AddModelError(string.Empty, "El enlace de recuperación no es válido o ha expirado. Por favor, solicitá uno nuevo.");
            return View(vm);
        }
    }
}


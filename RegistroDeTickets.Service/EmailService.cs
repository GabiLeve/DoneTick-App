using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace RegistroDeTickets.Service
{
    public interface IEmailService
    {
        Task EnviarEmailRecuperacion(string emailReceptor, string link);
        Task EnviarEmail(string emailReceptor, string tema, string cuerpo);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration configuration;

        public EmailService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task EnviarEmail(string emailReceptor, string tema, string cuerpo)
        {
            var emailEmisor = configuration.GetSection("CONFIGURACIONES_EMAIL")["EMAIL"];
            var password = configuration.GetSection("CONFIGURACIONES_EMAIL")["PASSWORD"];
            var host = configuration.GetSection("CONFIGURACIONES_EMAIL")["HOST"];
            var puerto = Int32.Parse(configuration.GetSection("CONFIGURACIONES_EMAIL")["PUERTO"]);

            var smtpCliente = new SmtpClient(host, puerto);
            smtpCliente.EnableSsl = true;
            smtpCliente.UseDefaultCredentials = false;

            smtpCliente.Credentials = new NetworkCredential(emailEmisor, password);
            var mensaje = new MailMessage(emailEmisor!, emailReceptor, tema, cuerpo);
            mensaje.IsBodyHtml = true;

            await smtpCliente.SendMailAsync(mensaje);
        }

        public async Task EnviarEmailRecuperacion(string emailReceptor, string link)
        {
            var cuerpo = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h1>Recuperación de Contraseña</h1>
                <p>Hacé click en el botón para restablecer tu contraseña:</p>
                <a href='{link}' style='background-color: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Restablecer mi contraseña
                </a>
                <p style='color: #7f8c8d;'>El enlace expirará en 30 minutos.</p>
            </body>
            </html>";

            await EnviarEmail(emailReceptor, "Recuperación de Contraseña", cuerpo);
        }
    }
}
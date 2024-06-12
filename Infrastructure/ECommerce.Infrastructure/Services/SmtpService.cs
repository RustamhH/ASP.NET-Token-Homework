using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Services
{
    public class SmtpService
    {

        public static bool isHtml = true;
        public static MailAddress to;
        private static MailAddress from = new MailAddress("reeljett@gmail.com");
        private static MailMessage email;

        public static async Task<bool> SendMail(string mail,string title,string text)
        {
            try
            {
                to = new MailAddress(mail);
                email = new MailMessage(from, to);
            }
            catch (Exception ex)
            {
                return false;
            }

            email.IsBodyHtml = isHtml;
            email.Subject = title;
            email.Body = text;

            SmtpClient smtp = new SmtpClient();
            smtp.Host = "smtp.gmail.com";
            smtp.Port = 25;
            smtp.Credentials = new NetworkCredential("reeljett@gmail.com", "kyzrgjrzqyhxknua");
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.EnableSsl = true;

            try
            {
                await smtp.SendMailAsync(email);
                isHtml = true;
            }
            catch (SmtpException ex)
            {
                return false;
            }
            return true;

        }
    }
}

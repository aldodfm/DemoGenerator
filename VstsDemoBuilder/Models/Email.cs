using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Web;

namespace VstsDemoBuilder.Models
{
	public class Email
	{
		public string EmailAddress { get; set; }
		public string AccountName { get; set; }
		public string ErrorLog { get; set; }

		public bool sendEmail(string toEmail, string body, string subject)
		{
			MailMessage newmsg = new MailMessage(ConfigurationManager.AppSettings["from"], toEmail);
			//newmsg.From = new MailAddress(ConfigurationManager.AppSettings["from"]);
			newmsg.IsBodyHtml = true;
			newmsg.Subject = subject;

			//newmsg.To.Add(toEmail);
			newmsg.Body = body;
			SmtpClient smtp = new SmtpClient();

			//smtp.Host = Convert.ToString(ConfigurationManager.AppSettings["mailhost"]);
			smtp.Host = "smtp.gmail.com";
			smtp.Port = 587;
			//smtp.Port = Convert.ToInt16(ConfigurationManager.AppSettings["port"]);
			smtp.UseDefaultCredentials = false;
			smtp.Credentials = new System.Net.NetworkCredential
		  (Convert.ToString(ConfigurationManager.AppSettings["username"]), Convert.ToString(ConfigurationManager.AppSettings["password"]));

			smtp.EnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["enableSSL"]);
			try
			{
				ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
				{ return true; };
				smtp.Send(newmsg);
			}
			catch (Exception e)
			{
				return false;
			}
			return true;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace gsmtCampPlacing
{
    internal sealed class Utility
    {
        //Get the connection string from App config file.  
        internal static string GetConnectionString()
        {
            //Assume failure.  
            string returnValue = null;

            //Look for the name in the connectionStrings section.  
            ConnectionStringSettings settings =
            ConfigurationManager.ConnectionStrings["gsmtCampPlacing.Properties.Settings.connString"];

            //If found, return the connection string.  
            if (settings != null)
                returnValue = settings.ConnectionString;

            return returnValue;
        }

        //Get the CouncilID string from App config file.  
        internal static int GetCouncilID()
        {
            return Properties.Settings.Default.CouncilID;
        }

        internal static bool GetIsDemoMode()
        {
            return Properties.Settings.Default.isDemoMode;
        }

        internal static string GetCouncilEmail()
        {
            return Properties.Settings.Default.CouncilEmail;
        }

        internal static string GetSwiApiKey()
        {
            return Properties.Settings.Default.swi_API_key;
        }
        internal static string GetSwiUsername()
        {
            return Properties.Settings.Default.swi_username;
        }
        internal static string GetSwiPassword()
        {
            return Properties.Settings.Default.swi_password;
        }
        internal static string GetSwiCallerID()
        {
            return Properties.Settings.Default.swi_callerID;
        }
        internal static string GetSwiSmsFromName()
        {
            return Properties.Settings.Default.swi_smsFromName;
        }
        internal static string GetSwiTimeZone()
        {
            return Properties.Settings.Default.swi_timeZone;
        }
        
        //Get the SMTP Host string from App config file.  
        internal static string GetSMTPhost()
        {
            return Properties.Settings.Default.SMTPhost;
        }
        internal static string GetSendGridUsername()
        {
            return Properties.Settings.Default.sendgrid_username;
        }
        internal static string GetSendGridPassword()
        {
            return Properties.Settings.Default.sendgrid_password;
        }


        internal static string GetTestMobilePhoneNo()
        {
            return Properties.Settings.Default.TestMobilePhoneNo;
        }

        internal static string GetTestEmail()
        {
            return Properties.Settings.Default.TestEmail;
        }

        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) { return value; }

            return value.Substring(0, Math.Min(value.Length, maxLength));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmtCampPlacing.Models
{
    [Serializable]
    public class Council
    {
        public string CouncilCode { get; set; }
        public string CouncilName { get; set; }
        public string CouncilEmail { get; set; }
        public string swi_api_key { get; set; }
        public string swi_username { get; set; }
        public string swi_password { get; set; }
        public string swi_callerid { get; set; }
        public string swi_smsfromname { get; set; }
        public string swi_timeZone { get; set; }
         
        public string sendgrid_username { get; set; }
        public string sendgrid_password { get; set; }
    }
}

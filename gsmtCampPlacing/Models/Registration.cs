using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmtCampPlacing.Models
{
    [Serializable]
    public class Registration
    {
        public int CampRegistrationID { get; set; }
        public string Code { get; set; }
        public int ResponsibleProfileID { get; set; }
        public bool isPaid { get; set; }
        public DateTime dts { get; set; }
        public int AttendingProfileCnt { get; set; }
        public int RegistrationCnt { get; set; }
        public int PlacedRegistrationCnt { get; set; }

        public int Priority { get; set; }
    }




}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmtCampPlacing.Models
{
    [Serializable]
    public class PlacedRegistrationCnt
    {
        public string Code { get; set; }
        public int AttendingProfileCnt { get; set; }
    }
}

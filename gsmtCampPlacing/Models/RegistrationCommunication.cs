using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmtCampPlacing.Models
{
    [Serializable]
    public class RegistrationCommunication
    {
        public int CampRegistrationID { get; set; }
        public int CampSessionCampProgramID { get; set; }
        public string CurrentDate { get; set; }
        public string CampSessionName { get; set; }
        public string CampProgramName { get; set; }
        public string CampName { get; set; }
        public string CampBuddy { get; set; }
        public string BeginDate { get; set; }
        public string EndDate { get; set; }
        public string BeginTime { get; set; }
        public string EndTime { get; set; }

        public string GirlFee { get; set; }
        public string GirlNonMemberFee { get; set; }
        public string AdultFee { get; set; }
        public string AdultNonMemberFee { get; set; }
        public string CampSubProgramGirlFee { get; set; }
        public string CampSubProgramAdultFee { get; set; }
        public string TotalFee { get; set; }
        public string TradingPost { get; set; }
        public string Deposit { get; set; }
        public string CurrentBalanceDue { get; set; }

        public string NumberOfAdults { get; set; }
        public string NumberOfGirls { get; set; }

        public string NumberOfGirlsRegistered { get; set; }
        public string NumberOfNonGirlScoutsRegistered { get; set; }

        public string NumberOfAdultsRegistered { get; set; }
        public string NumberOfNonMemberAdultsRegistered { get; set; }
               
        public int ResponsibleProfileID { get; set; }
        public string CurrentRegistrationStatusCode { get; set; }
        public DateTime CurrentRegistrationStatusDTS { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string MobilePhone { get; set; }
        public string HomePhone { get; set; }
        public string WorkPhone { get; set; }
        public string HomeEmail { get; set; }
        public string WorkEmail { get; set; }
        public string HomeAddress1 { get; set; }
        public string HomeAddress2 { get; set; }
        public string HomeCity { get; set; }
        public string HomeStateAbbr { get; set; }
        public string HomeZip5 { get; set; }

        public string GirlFirstName { get; set; }
        public string GirlLastName { get; set; }
        public string TroopNumber { get; set; }

        public string CommunicationTypeCode { get; set; }

        public bool isCustomMessage { get; set; }

        public bool isEMA { get; set; }
        public bool isTEX { get; set; }
        public bool isMES { get; set; }
        public bool isVOI { get; set; }

        public string EMAmessageText { get; set; }
        public string TEXmessageText { get; set; }
        public string MESmessageText { get; set; }
        public string VOImessageText { get; set; }
        
        public TimeSpan EMAsendTime { get; set; }
        public TimeSpan TEXsendTime { get; set; }
        public TimeSpan MESsendTime { get; set; }
        public TimeSpan VOIsendTime { get; set; }
               
    }




}

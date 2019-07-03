using DasSwiftReachAPIclient;
using System;

namespace gsmtCampPlacing
{
    public class DasSwiftReach
    {
        public bool isTTS = false;
        public bool isSMS = false;
        public int CouncilID;
        
        public string APIkey;
        public string APIUrl;        
        public string CallerID;
        public string TimeZone;
        public string SmsFromName;
        public string FullPathToCSV;

        public string ContactListCampaignName;

        public DateTime SMSrunOnDateTime;
        public string SMSmessageText = String.Empty;
        public string SMScampaignName;
        public string SMSscheduledAlertCode;

        public DateTime TTSrunOnDateTime;
        public string TTSmessageText = String.Empty;
        public string TTScampaignName;
        public string TTSscheduledAlertCode;

        public bool isError = false;
        public string ErrorMessage = String.Empty;
        public string JobCode = String.Empty;

        private string ListCode = String.Empty;
        private string UploadCSVstatusCode = String.Empty;
        private string VoiceCode = "0"; //must default to zero
        private string SmsCode = "0";  //must default to zero
        private string VoiceJsonData;
        private string SmsJsonData;
        private string DefaultLanguage = "English";
        private string autoReplay = "1";
        private string autoRetry = "1";
        private string detectAnsweringMachine = "true";

        public Communications swi;
                
        public bool ScheduleBroadcast()
        {
            swi = new Communications();
            swi.APIkey = APIkey;
            swi.APIUrl = APIUrl;
            
            bool isProceed = false;

            if (ListCode == String.Empty)
                ListCode = swi.CreateContactList(ContactListCampaignName, String.Format("CouncilID: {0}", CouncilID));

            if(ListCode != String.Empty && !swi.isError)
            {
                if (UploadCSVstatusCode == String.Empty)
                    UploadCSVstatusCode = swi.UploadAndImportCSV(ListCode, FullPathToCSV, "true");

                string runDateTime;

                if (!swi.isError)
                {
                    if (isTTS)
                    {
                        isProceed = CreateVoiceCode();

                        if (isProceed)
                        {
                            //Schedule the TTS
                            //Convert datetime to Council's timezone
                            runDateTime = TTSrunOnDateTime.ToString();
                            if (TimeZone != "EST")
                            {
                                string fromTZ = TimeZone;
                                string toTZ = "EST";
                                runDateTime = swi.TZconvert(TTSrunOnDateTime.ToString(), fromTZ, toTZ);
                            }

                            TTSscheduledAlertCode = swi.CreateScheduledAlert(ListCode, VoiceCode, "0", runDateTime, TTScampaignName);

                            if (swi.isError)
                            {
                                isError = swi.isError;
                                ErrorMessage = swi.errorMessage;
                                isProceed = false;
                            }
                        }
                    }
                    else
                    {
                        VoiceCode = "0";
                    }
                    
                    if (isSMS)
                    {
                        isProceed = CreateSmsCode();

                        if (isProceed)
                        {
                            //Schedule the SMS
                            //Convert datetime to Council's timezone
                            runDateTime = SMSrunOnDateTime.ToString();
                            if (TimeZone != "EST")
                            {
                                string fromTZ = TimeZone;
                                string toTZ = "EST";
                                runDateTime = swi.TZconvert(SMSrunOnDateTime.ToString(), fromTZ, toTZ);
                            }

                            SMSscheduledAlertCode = swi.CreateScheduledAlert(ListCode, "0", SmsCode, runDateTime, SMScampaignName);

                            if (swi.isError)
                            {
                                isError = swi.isError;
                                ErrorMessage = swi.errorMessage;
                                isProceed = false;
                            }
                        }
                    }
                    else
                    {
                        SmsCode = "0";
                    }
                    
                }
                else if (swi.isError)
                {
                    isError = swi.isError;
                    ErrorMessage = swi.errorMessage;
                }
            }
            else if (swi.isError)
            {
                isError = swi.isError;
                ErrorMessage = swi.errorMessage;
            }

            return isProceed;
        }

        public bool CreateVoiceCode()
        {
            swi = new Communications();
            swi.APIkey = APIkey;
            swi.APIUrl = APIUrl;

            swi.MakeAcontentProfile(TTSmessageText.Trim(), DefaultLanguage);
            bool rslt = swi.CreateCustomVoiceTTSmessage(TTScampaignName, CallerID, autoRetry, autoReplay, detectAnsweringMachine);
            if (rslt)
            {
                VoiceCode = swi.VoiceCode;
                VoiceJsonData = swi.JsonData;
            }
            else
            {
                isError = swi.isError;
                ErrorMessage = swi.errorMessage;
            }
            return rslt;
        }

        public bool CreateSmsCode()
        {
            swi = new Communications();
            swi.APIkey = APIkey;
            swi.APIUrl = APIUrl;

            swi.MakeAnSmsProfile(Utility.Truncate(SMSmessageText, 140), DefaultLanguage);
            bool rslt = swi.CreateCustomSmsMessage(SMScampaignName, SmsFromName);
            if (rslt)
            {
                SmsCode = swi.SmsCode;
                SmsJsonData = swi.JsonData;
            }
            else
            {
                isError = swi.isError;
                ErrorMessage = swi.errorMessage;
            }
            return rslt;
        }
    }
}

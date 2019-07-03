using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Data.Sql;
using GenericPopulator;
using System.Reflection;
using System.Net.Mail;
using System.Net;
using gsmtCampPlacing.Models;
using System.IO;

namespace gsmtCampPlacing
{
    public static class Program
    {
        /*  ISDEMOMODE
         *  IF TRUE - 1. THE MOBILEPHONE USED FOR ALL CONTACT LIST WILL SWITCH TO THE MOBILE PHONE DEFINED IN THE APP SETTINGS
         *            2. EMAILS WILL BE DIRECTED TO THE TEST EMAIL ACCOUNT DEFINED IN THE APP SETTINGS
         *  ISDEBUGMODE
         *  IF TRUE
         *            1. SWAPPASTDUEWITHWAITINGLIST, PLACING IS SKIPED AND WILL USE DATA IN THE _TMP TABLES 
         *            2. COMMUNICATIONS IS SKIPPED
        */
        private static bool isDemoMode = Utility.GetIsDemoMode();

        private static Council council = new Council();      

        static void Main(string[] args)
        {
            LogEvent.Log(String.Format("Job Run. Council: {0}", Utility.GetCouncilID()));

            Init();
                       
            //Process Forced Placement
            ForcedPlace();

            //Begin Placing
            BeginActivityRegPlacing();

            //Begin sending communications
            SendCommunications();                       
        }

        private static void Init()
        {
            council.CouncilEmail = Utility.GetCouncilEmail();
            council.swi_api_key = Utility.GetSwiApiKey();
            council.swi_username = Utility.GetSwiUsername();
            council.swi_password = Utility.GetSwiPassword();
            council.swi_callerid = Utility.GetSwiCallerID();
            council.swi_smsfromname = Utility.GetSwiSmsFromName();
            council.swi_timeZone = Utility.GetSwiTimeZone();

            council.sendgrid_username = Utility.GetSendGridUsername();
            council.sendgrid_password = Utility.GetSendGridPassword();            
        }

        private static void BeginActivityRegPlacing()
        {
            using (SqlConnection connection = new SqlConnection())
            {
                int cnt = 0;

                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                //Get all eligible activities and their current count
                SqlCommand command = new SqlCommand("dbo.uspGetCampRegistrationCount", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        //Loop over each Activity
                        while (reader.Read())
                        {
                            int campSessionCampProgramID = (int)reader["CampSessionCampProgramID"];
                            bool hasPlacedRegistrations = (bool)reader["HasPlacedRegistrations"];
                            int registrationCnt = (int)reader["RegistrationCnt"];
                            int minimumCapacity = (int)reader["MinimumCapacity"];
                            int maximumCapacity = (int)reader["MaximumCapacity"];
                            DateTime? placedDate = null;
                            if (!reader.IsDBNull(1))
                            {
                                placedDate = (DateTime?)reader["PlacedDate"];
                            }

                            LogEvent.Log(String.Format("Processing CampSessionCampProgramID: {0} , Count: {1}", campSessionCampProgramID, registrationCnt));

                            //Check if there are any "Placed" registrations for this activity
                            if (hasPlacedRegistrations == false)
                            {
                                //Determine if it's time to start placing.  

                                //Check if the Count >= Minimum Capacity 
                                if (registrationCnt >= minimumCapacity)
                                {
                                    //Is there a Placed Date
                                    if (placedDate != null)
                                    {
                                        //There is a Placed Date. Is the Placed Date less than or equal to today?
                                        if (placedDate <= DateTime.Now)
                                        {
                                            StartPlacing(campSessionCampProgramID, maximumCapacity, "WFP"); //Waiting for Placement
                                        }
                                        else
                                        {
                                            //Do nothing.  We haven't reached the Placed Date yet..
                                            LogEvent.Log(String.Format("No action taken on CampSessionCampProgramID: {0} , Reason: PlacedDate is greater than today.", campSessionCampProgramID));
                                        }
                                    }
                                    else
                                    {
                                        //There is no Placed Date 
                                        StartPlacing(campSessionCampProgramID, maximumCapacity, "WFP"); //Waiting for Placement
                                    }
                                }
                                else
                                {
                                    //Do nothing.  Registration count hasn't reached the minimum capacity
                                    LogEvent.Log(String.Format("No action taken on CampSessionCampProgramID: {0} , Reason: Registration count hasn't reached the minimum capacity.", campSessionCampProgramID));
                                }
                            }
                            else
                            {
                                //Activity has placed registrations already.  Perform swapping the waiting list registrations with the past due, un-paid registrations
                                //This is were we will call the routine to swap those in the waiting list with those with past due bills

                                LogEvent.Log(String.Format("CampSessionCampProgramID: {0} has placed registrations already. Performing waiting list swap.", campSessionCampProgramID));

                                //Place WL and WFP in case there's an opening
                                
                                StartPlacing(campSessionCampProgramID, maximumCapacity, "WL"); //Waiting List - In case we have room due to cancellations
                                StartPlacing(campSessionCampProgramID, maximumCapacity, "WFP"); //Waiting for Placement - New registrations, placed on WL or PLA if there's room
                                StartPlacing(campSessionCampProgramID, maximumCapacity, "NOT"); //Not Placed - Check if Past-Due on other registration was updated to REM, if so, update to WFP
                               

                                //Get all registrations for this activity where current status = Placed or PLA and isPaid = false and check if past due
                                //Rule: If current registration status DTS plus the UnpaidAfterPlacedDate is greater than today, then change the status to "Past Due"
                                CheckIfPassDue(campSessionCampProgramID);
                                
                                //Begin the swap process
                                SwapPastDueWithWaitingList(campSessionCampProgramID, minimumCapacity, maximumCapacity);
                                
                            }

                            //increment counter
                            cnt = cnt + 1;
                        }
                    }
                    else
                    {
                        LogEvent.Log("There are no activities to place.");
                    }
                }
                
                LogEvent.Log(String.Format("Processing completed. Total Camp Session Camp Programs processed: {0}", cnt));

                connection.Close();
            }

        }

        private static void ForcedPlace()
        {
            LogEvent.Log("Processing Forced Placement");

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelectByCode", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "FP";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int campRegistrationID;
                    int responsibleProfileID;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            campRegistrationID = (int)reader["CampRegistrationID"];
                            responsibleProfileID = (int)reader["ResponsibleProfileID"];
                            
                            LogEvent.Log(String.Format("Placing CampRegistrationID: {0} , ResponsibleProfileID: {1}", campRegistrationID, responsibleProfileID));
                                                       
                            //Update status to 'PLA' or Placed
                            UpdateStatus(campRegistrationID, "PLA");
                            
                        }
                    }
                    else
                    {
                        LogEvent.Log("No Forced Placement status found");
                    }
                }

                connection.Close();
            }
        }

        private static void PlaceFromWaitingList()
        {
            LogEvent.Log("Processing Placed form Waiting List to Placed");

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelectByCode", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "PWL";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int campRegistrationID;
                    int responsibleProfileID;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            campRegistrationID = (int)reader["CampRegistrationID"];
                            responsibleProfileID = (int)reader["ResponsibleProfileID"];

                            LogEvent.Log(String.Format("Updating status from PWL to PLA - CampRegistrationID: {0} , ResponsibleProfileID: {1}", campRegistrationID, responsibleProfileID));

                            //Update status to 'PLA' or Placed
                            UpdateStatus(campRegistrationID, "PLA");

                        }
                    }
                    else
                    {
                        LogEvent.Log("No Placed from Waiting List status found");
                    }
                }

                connection.Close();
            }
        }

        private static void StartPlacing(int CampSessionCampProgramID, int MaximumCapacity, string Code)
        {
            //Start Placing each "Un-placed" registration in the order they came in but DO NOT exceed the Maximum Capacity
            //"Un-placed" are WFP or WL
            //Check for unpaid bill.  Change the registration status to 'Unpaid Bill'
            //If the Maximum Capacity is reached then change the registration status to 'Waiting List'

            if (Code == "WFP" || Code == "WL" || Code == "NOT")
            {
                LogEvent.Log(String.Format("Placing for CampSessionCampProgramID {0}", CampSessionCampProgramID));

                using (SqlConnection connection = new SqlConnection())
                {
                    connection.ConnectionString = Utility.GetConnectionString();
                    connection.Open();

                    SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelect", connection);
                    command.CommandTimeout = 999999999;
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = Code;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int campRegistrationID;
                        bool isPaid;
                        int responsibleProfileID;
                        DateTime dts;
                        int attendingProfileCnt;
                        int registrationCnt; //excludes teen mentor and accounts for if adults count towards capacity
                        int placedAttendingProfileCnt;
                        
                        //Loop over each Activity Registration
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                campRegistrationID = (int)reader["CampRegistrationID"];
                                isPaid = (bool)reader["isPaid"];
                                responsibleProfileID = (int)reader["ResponsibleProfileID"];
                                dts = (DateTime)reader["dts"];
                                attendingProfileCnt = (int)reader["AttendingProfileCnt"];
                                registrationCnt = (int)reader["RegistrationCnt"];
                                //Get the total Placed and Placed-Past Due
                                placedAttendingProfileCnt = GetCurrentCount(CampSessionCampProgramID, "PLA") 
                                                            + GetCurrentCount(CampSessionCampProgramID, "PAS")
                                                            + GetCurrentCount(CampSessionCampProgramID, "PWL");

                                LogEvent.Log(String.Format("Placing CampRegistrationID: {0} , ResponsibleProfileID: {1}", campRegistrationID, responsibleProfileID));

                                
                                //Check if has unpaid bills
                                if (HasPastDueUnPaidBills(CampSessionCampProgramID, responsibleProfileID))
                                {
                                    if (Code != "NOT")
                                    {
                                        //Update status to 'NOT' or Not Placed - Previous Past Due Bill
                                        UpdateStatus(campRegistrationID, "NOT");
                                    }
                                }
                                else
                                {
                                    //Will we go over the maximum capacity for this Activity if we placed this registration? If Yes, update the status to Waiting List
                                    if ((placedAttendingProfileCnt + registrationCnt) > MaximumCapacity)
                                    {
                                        if (Code != "WL")
                                        {
                                            //Update status to 'WL' or Waiting List
                                            UpdateStatus(campRegistrationID, "WL");
                                        }
                                    }
                                    else
                                    {
                                        if (Code == "NOT")
                                        {
                                            //Update status to 'WFP'
                                            UpdateStatus(campRegistrationID, "WFP");
                                        }
                                        else
                                        {
                                            if (Code == "WL")
                                            {
                                                //Update status to 'PWL' or Placed from Waiting List
                                                UpdateStatus(campRegistrationID, "PWL");
                                            }
                                            else
                                            {
                                                //Update status to 'PLA' or Placed
                                                UpdateStatus(campRegistrationID, "PLA");
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                    connection.Close();
                }
            }
        }

        public static int GetCurrentCount(int CampSessionCampProgramID, string Code)
        {
            int retVal = 0;

            List<RegistrationCommunication> ListOfActAndRegs = new List<RegistrationCommunication>();
            ListHelper<RegistrationCommunication> _rc = new ListHelper<RegistrationCommunication>();

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationCurrentStatusCount", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = Code;

                using (SqlDataReader reader = command.ExecuteReader())
                {                   

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                retVal = (int)reader["PlacedRegistrationCnt"];
                            }
                        }
                    }
                }

                connection.Close();
            }

            return retVal;
        }

        private static bool HasPastDueUnPaidBills(int CampSessionCampProgramID, int ResponsibleProfileID)
        {
            bool retVal = false;

            //Query for past due un-paid registrations 

            LogEvent.Log(String.Format("Checking for past due, up-paid registrations for CampSessionCampProgramID: {0} , ResponsibleProfileID: {1}", CampSessionCampProgramID, ResponsibleProfileID));

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationUnpaidBill", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@ResponsibleProfileID", SqlDbType.Int)).Value = ResponsibleProfileID;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int cntOfUnPaidBills;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                cntOfUnPaidBills = (int)reader["CntOfUnPaidBills"];

                                if (cntOfUnPaidBills > 0)
                                    retVal = true;
                                else
                                    retVal = false;
                            }
                            else
                            {
                                LogEvent.Log(String.Format("Error occurred checking for unpaid bills for CampSessionCampProgramID: {0} , ResponsibleProfileID: {1}. A NULL value was returned.", CampSessionCampProgramID, ResponsibleProfileID));
                            }
                        }
                    }
                    else
                    {
                        LogEvent.Log(String.Format("Error occurred checking for unpaid bills for CampSessionCampProgramID: {0} , ResponsibleProfileID: {1}. An empty resultset was returned.", CampSessionCampProgramID, ResponsibleProfileID));
                    }
                }

                connection.Close();
            }

            return retVal;
        }

        private static void UpdateStatus(int CampRegistrationID, string Code)
        {
            LogEvent.Log(String.Format("Updating status for CampRegistrationID: {0} , Code: {1}", CampRegistrationID, Code));

            //Query to insert the registration status

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationStatusInsert", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampRegistrationID", SqlDbType.Int)).Value = CampRegistrationID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = Code;
                command.Parameters.Add(new SqlParameter("@ChgdBy", SqlDbType.NVarChar, 128)).Value = "autoproc";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int errorNumber;
                    int errorLine;
                    string errorMessage;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                errorNumber = (int)reader["ErrorNumber"];
                                errorLine = (int)reader["ErrorLine"];
                                errorMessage = (string)reader["ErrorMessage"];

                                LogEvent.Log(String.Format("Error occured while updating status for CampRegistrationID: {0} , Code: {1} , Error No: {2} , Line No.: {3} , Error Msg: {4}", CampRegistrationID, Code, errorNumber, errorLine, errorMessage));
                            }
                            else
                            {
                                LogEvent.Log(String.Format("Status updated for CampRegistrationID: {0} , Code: {1}", CampRegistrationID, Code));
                            }
                        }
                    }
                }

                connection.Close();
            }
        }

        public static void CheckIfPassDue(int CampSessionCampProgramID)
        {
            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                //Check for placed, pass due, unpaid registrations for this activity and update their status to 'Past Due'
                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationUpdatePlacedUnpaidToPastDue_batch", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int errorNumber;
                    int errorLine;
                    string errorMessage;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                errorNumber = (int)reader["ErrorNumber"];
                                errorLine = (int)reader["ErrorLine"];
                                errorMessage = (string)reader["ErrorMessage"];

                                LogEvent.Log(String.Format("Error occured while updating status from placed to past due for CampSessionCampProgramID: {0} , Error No: {1} , Line No.: {2} , Error Msg: {3}", CampSessionCampProgramID, errorNumber, errorLine, errorMessage));
                            }
                            else
                            {
                                LogEvent.Log(String.Format("Check for placed, past due, unpaid registrations completed for CampSessionCampProgramID: {0}", CampSessionCampProgramID));
                            }
                        }
                    }
                }

                connection.Close();
            }
        }

        public static void SwapPastDueWithWaitingList(int CampSessionCampProgramID, int MinimumCapacity, int MaximumCapacity)
        {
            //Initialize method Properties
            int _placedCnt = 0;
            int _pastDueCnt = 0;
            int _available = 0;
            int _actualAvailable = 0;

            //Get a list of placed from waiting list registrations for this activity
            List<Registration> ListOfPlacedWaitingListReg = new List<Registration>();
            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelect", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "PWL";

                ListHelper<Registration> m = new ListHelper<Registration>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfPlacedWaitingListReg = m.CreateList(reader);
                }

                connection.Close();
            }

            //Get a list of placed registrations for this activity
            List<Registration> ListOfPlacedReg = new List<Registration>();
            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelect", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "PLA";

                ListHelper<Registration> m = new ListHelper<Registration>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfPlacedReg = m.CreateList(reader);
                }

                connection.Close();
            }

            //Get a list of placed - past due registrations for this activity
            List<Registration> ListOfPastDueReg = new List<Registration>();
            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelect", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "PAS";

                ListHelper<Registration> m = new ListHelper<Registration>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfPastDueReg = m.CreateList(reader);
                }

                connection.Close();
            }

            //Get a list of waiting list registrations for this activity
            List<Registration> ListOfWaitingListReg = new List<Registration>();
            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationSelect", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampSessionCampProgramID", SqlDbType.Int)).Value = CampSessionCampProgramID;
                command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5)).Value = "WL";

                ListHelper<Registration> m = new ListHelper<Registration>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfWaitingListReg = m.CreateList(reader);
                }

                connection.Close();
            }

            //Compute counts
            _pastDueCnt = ListOfPastDueReg.Sum(x => x.RegistrationCnt);
            _placedCnt = ListOfPlacedReg.Sum(x => x.RegistrationCnt) + ListOfPlacedWaitingListReg.Sum(x => x.RegistrationCnt) + _pastDueCnt;
            _available = (MaximumCapacity - _placedCnt);
            _actualAvailable = _pastDueCnt + _available;

            //Pre-Qualify the Waiting List (AttendingProfileCnt must be less than or equal _actualAvailable)
            //Remove all registrations in the Waiting List where the AttendingProfileCnt > the _actualAvailable
            ListOfWaitingListReg.RemoveAll(x => x.RegistrationCnt > _actualAvailable);

            //Sort the list by DTS ("First Come, First Serve")
            ListHelper<Registration> wlReg = new ListHelper<Registration>();
            ListOfWaitingListReg = wlReg.SortList(ListOfWaitingListReg, "dts", true);            
            ListOfPastDueReg = wlReg.SortList(ListOfPastDueReg, "dts", true);

            //Is there a waiting list? If so, continue...
            if (ListOfWaitingListReg.Count > 0)
            {
                LogEvent.Log(String.Format("Determining best swap combination for CampSessionCampProgramID: {0}", CampSessionCampProgramID));

                //"Subset Sum Problem Algorithm" -- rroque 8/25/2017
                //Note: This algorithm will figure out the best combination to fill the activity with attendance, replacing the placed-past-due with those in the waiting list
                //but still attempt to achieve the maximum capacity while honoring the priority in which the registrations were placed ("First Come, First Serve")
                var rslt = ListOfWaitingListReg.Union(ListOfPastDueReg)
                    .Select(x => new { x.CampRegistrationID, x.Code, x.RegistrationCnt, x.Priority, x.dts })
                    .Subsequences()
                    .Where(ss => ss.Sum(item => item.RegistrationCnt) >= _pastDueCnt && ss.Sum(item => item.RegistrationCnt) <= _actualAvailable)
                //.OrderByDescending(x => x.Sum(item => item.AttendingProfileCnt)).ToList();
                .OrderBy(x => x.First().Priority).ThenBy(x => x.First().dts).ToList();

                //List to hold results of "subset sum problem"
                List<Registration> TempList = new List<Registration>();

                if (rslt.Count > 0)
                {
                    Registration r;
                    
                    //use the best combination which is the first row in the rslt list
                    foreach (var item in rslt[0].ToList())
                    {
                        r = new Registration();
                        r.CampRegistrationID = item.CampRegistrationID;
                        r.Code = item.Code == "WL" ? "PWL" : item.Code;  //Placed from Waiting List
                        r.RegistrationCnt = item.RegistrationCnt;
                        TempList.Add(r);
                    }

                    //determine which placed-past-due registrations will be removed
                    foreach (var item in ListOfPastDueReg)
                    {
                        //If not found in the TempList, Add with the Status 'REM'
                        if (!(TempList.Where(x => x.CampRegistrationID == item.CampRegistrationID).ToList().Count > 0))
                        {
                            //change code to removed
                            item.Code = "REM";
                            //Add to TempList with the status 'REM'
                            TempList.Add(item);
                        }
                    }

                    LogEvent.Log(String.Format("Best swap combination achived successfully for CampSessionCampProgramID: {0}", CampSessionCampProgramID));
                    
                    //Begin placing those in the waiting list that are eligible to replace the placed-past-due registrations. 
                    //Note: Depending on the results, some PAS may remain as PAS if not enough registrations in the waiting list are sufficient to replace PAS.
                    CommitSwapResults(TempList, CampSessionCampProgramID);
                }
                else
                {
                    LogEvent.Log(String.Format("A best swap combination cannot be determined for CampSessionCampProgramID: {0}. CampSessionCampProgramID skipped.", CampSessionCampProgramID));
                }
            }

        }
               

        public static void CommitSwapResults(List<Registration> TempList, int CampSessionCampProgramID)
        {
            LogEvent.Log(String.Format("Saving swap combination results for CampSessionCampProgramID: {0}", CampSessionCampProgramID));

            foreach (var item in TempList)
            {
                UpdateStatus(item.CampRegistrationID, item.Code);
            }
        }

        public static void SendCommunications()
        {
            //Placed from Waiting List
            GetAndSendCommunicationData("PWL", "PWL", "Reg Placed from Waiting List Notice");
            //Placed
            GetAndSendCommunicationData("PLA", "PLA", "Reg Placed Notice");
            //Past Due
            GetAndSendCommunicationData("PAS", "PAS", "Reg Past Due Notice");
            //Waiting List
            GetAndSendCommunicationData("WL", "WL", "Reg Waiting List Notice");
            //Removed
            GetAndSendCommunicationData("REM", "REM", "Reg Removed Notice");
            //Not Placed - Previous Past Due Bill
            GetAndSendCommunicationData("NOT", "NOT", "Reg Not Placed Notice");

            //Update PWL to PLA (PWL is for communication purposes only.  They need to be updated to PLA after communication is sent out.
            PlaceFromWaitingList();

            //Info Packets
            GetAndSendInfoPacketCommunicationData("Activity Registration Information Packet");
            //Payment Reminder
            GetAndSendPaymentReminderCommunicationData("Activity Registration Payment Reminder");
        }

        public static void GetAndSendCommunicationData(string RegStatusCode, string CommTypeCode, string Title)
        {
            List<RegistrationCommunication> ListOfActAndRegs = new List<RegistrationCommunication>();
            ListHelper<RegistrationCommunication> _rc = new ListHelper<RegistrationCommunication>();

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspGetCampRegistrationCommunication", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CurrentRegistrationStatusCode", SqlDbType.VarChar, 5)).Value = RegStatusCode;
                command.Parameters.Add(new SqlParameter("@CommunicationTypeCode", SqlDbType.VarChar, 5)).Value = CommTypeCode;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfActAndRegs = _rc.CreateList(reader);
                }

                connection.Close();
            }

            if (ListOfActAndRegs.Count > 0)
            {
                SendIt(ListOfActAndRegs, Title);
            }
            else
            {
                LogEvent.Log(String.Format("No records found for RegStatusCode: {0} CommTypeCode: {1} Title: {2}", RegStatusCode, CommTypeCode, Title));
            }
            
        }

        //INFO PACKET
        public static void GetAndSendInfoPacketCommunicationData(string Title)
        {
            List<RegistrationCommunication> ListOfActAndRegs = new List<RegistrationCommunication>();
            ListHelper<RegistrationCommunication> _rc = new ListHelper<RegistrationCommunication>();

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspGetCampRegistrationInfoPacketCommunication", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
               
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfActAndRegs = _rc.CreateList(reader);
                }

                connection.Close();
            }

            if (ListOfActAndRegs.Count > 0)
            {
                SendIt(ListOfActAndRegs, Title);
            }
            else
            {
                LogEvent.Log(String.Format("No Information Packet records found."));
            }

        }
        
        
        //Payment Reminder
        public static void GetAndSendPaymentReminderCommunicationData(string Title)
        {
            List<RegistrationCommunication> ListOfActAndRegs = new List<RegistrationCommunication>();
            ListHelper<RegistrationCommunication> _rc = new ListHelper<RegistrationCommunication>();

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspGetCampRegistrationPaymentReminderCommunication", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    ListOfActAndRegs = _rc.CreateList(reader);
                }

                connection.Close();
            }

            if (ListOfActAndRegs.Count > 0)
            {
                SendIt(ListOfActAndRegs, Title);
            }
            else
            {
                LogEvent.Log(String.Format("No Information Packet records found."));
            }

        }


        public static void SendIt(List<RegistrationCommunication> ListOfCampRegs, string Title)
        {           
            //Select Distinct by CampRegistrationID
            var ListOfCampSessionCampProgramIDs = ListOfCampRegs.GroupBy(x => x.CampSessionCampProgramID).Select(x => x.First()).ToList();

            //Loop over each CampRegistrationID
            foreach (var actItem in ListOfCampSessionCampProgramIDs)
            {                
                //Get all the registrations for this CampRegistrationID
                var ListOfRegsForThisCampRegistrationID = ListOfCampRegs.Where(x => x.CampSessionCampProgramID == actItem.CampSessionCampProgramID).ToList();

                foreach (var regItem in ListOfRegsForThisCampRegistrationID)
                {
                    if (regItem.isTEX || regItem.isVOI)
                    {
                        DasSwiftReach dasSwi = new DasSwiftReach();
                        dasSwi.APIkey = council.swi_api_key;
                        dasSwi.APIUrl = "http://api.v4.swiftreach.com/api";
                        dasSwi.SmsFromName = council.swi_smsfromname;
                        dasSwi.CouncilID = Utility.GetCouncilID();
                        dasSwi.TimeZone = council.swi_timeZone;
                        dasSwi.CallerID = council.swi_callerid;
                        dasSwi.isSMS = false;
                        dasSwi.isTTS = false;

                        DateTime date = DateTime.Now.Date.AddDays(1);
                        TimeSpan time;
                        DateTime combined;

                        string fileName;
                        string fullName;
                        string mobilePhoneCleaned;
                        ExportFileCreator efc;

                        //Create the contact list
                        fileName = String.Format("gsmtCntct{0}-{1}-{2}.csv", DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"), regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID.ToString());

                        //Generate the CSV file
                        efc = new ExportFileCreator();
                        efc.Write("Name,Phone,OptInSMS,PhoneType,Tag,SpokenLanguage", fileName);

                        dasSwi.FullPathToCSV = efc.FullPath;
                        dasSwi.ContactListCampaignName = String.Format("CampSessCampProgID: {0} CampRegID: {1} - {2}", regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID, Title);

                        fullName = String.Format("{0} {1}", regItem.FirstName, regItem.LastName);
                        mobilePhoneCleaned = new String(regItem.MobilePhone.Where(char.IsDigit).ToArray());
                        efc.Write(String.Format("{0},{1},{2},{3},{4},{5}", fullName, (isDemoMode ? Utility.GetTestMobilePhoneNo() : regItem.MobilePhone), "true", "Mobile", regItem.ResponsibleProfileID.ToString(), "English"), fileName);
                                                

                        if (regItem.isTEX)
                        {
                            //Replace placeholders with data
                            regItem.TEXmessageText = FormatWith(regItem.TEXmessageText, regItem);

                            //Text messages has a limit of 140 characters
                            dasSwi.SMSmessageText = Utility.Truncate(regItem.TEXmessageText, 140);

                            time = regItem.TEXsendTime;
                            combined = date.Add(time);

                            dasSwi.SMSrunOnDateTime = combined;

                            dasSwi.SMScampaignName = String.Format("CampSessCampProgID: {0} - {1} {2} - {3} - CampRegID: {4}", regItem.CampSessionCampProgramID.ToString(), Title, fullName, "(SMS)", regItem.CampRegistrationID);

                            dasSwi.isSMS = true;
                            dasSwi.isTTS = false;

                            //Broadcast TTS
                            dasSwi.ScheduleBroadcast();

                            if (!dasSwi.isError)
                            {
                                LogEvent.Log(String.Format("A SwiftReach SMS scheduled alert was created. CampSessCampProgID: {0} CampRegID: {1} JobCode: {2}", regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID, dasSwi.SMSscheduledAlertCode));
                                LogSentMessage(regItem.CampRegistrationID, regItem.CommunicationTypeCode, "TEX", dasSwi.SMSmessageText, dasSwi.SMSscheduledAlertCode);
                            }
                            else
                                LogEvent.Log(String.Format("An error occurred on creating a SwiftReach SMS scheduled alert for CampSessCampProgID: {0} | Responsible Person: {1} | CampRegID: {2} | Error: {3}", regItem.CampSessionCampProgramID.ToString(), fullName, regItem.CampRegistrationID.ToString(), dasSwi.ErrorMessage));
                        }
                                                

                        if (regItem.isVOI)
                        {
                            //Replace placeholders with data
                            regItem.VOImessageText = FormatWith(regItem.VOImessageText, regItem);

                            dasSwi.TTSmessageText = Utility.Truncate(regItem.VOImessageText, 140);

                            time = regItem.VOIsendTime;
                            combined = date.Add(time);

                            dasSwi.TTSrunOnDateTime = combined;

                            dasSwi.TTScampaignName = String.Format("CampSessCampProgID: {0} - {1} {2} - {3} - CampRegID: {4}", regItem.CampSessionCampProgramID.ToString(), Title, fullName, "(TTS)", regItem.CampRegistrationID.ToString());

                            dasSwi.isTTS = true;
                            dasSwi.isSMS = false;

                            //Broadcast SMS
                            dasSwi.ScheduleBroadcast();

                            if (!dasSwi.isError)
                            {                                
                                    LogEvent.Log(String.Format("A SwiftReach TTS scheduled alert was created. CampSessCampProgID: {0} CampRegID: {1} JobCode: {2}", regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID.ToString(), dasSwi.TTSscheduledAlertCode));
                                    LogSentMessage(regItem.CampRegistrationID, regItem.CommunicationTypeCode, "VOI", dasSwi.TTSmessageText, dasSwi.TTSscheduledAlertCode);
                            }
                            else
                                LogEvent.Log(String.Format("An error occurred on creating a SwiftReach TTS scheduled alert for CampSessCampProgID: {0} | Responsible Person: {1} | CampRegID: {2} | Error: {3}", regItem.CampSessionCampProgramID.ToString(), fullName, regItem.CampRegistrationID.ToString(), dasSwi.ErrorMessage));
                        }
                              
                    }
                        
                    //Email
                    if (regItem.isEMA)
                    {
                        //NOTE: EMAILS ARE SENT IMMEDIATELY

                        //Replace placeholders with data
                        regItem.EMAmessageText = FormatWith(regItem.EMAmessageText, regItem);
                        
                        //Construct email message
                        MailMessage message = new MailMessage();
                        message.To.Add(isDemoMode ? Utility.GetTestEmail() : regItem.HomeEmail);
                        message.Subject = String.Format("CouncilAlignMENT {0}", Title);
                        message.From = new MailAddress(council.CouncilEmail);
                        message.Body = regItem.EMAmessageText;
                        message.IsBodyHtml = true;

                        //Setup the SMTPClient          
                        SmtpClient smtp = new SmtpClient();
                        smtp.Host = Utility.GetSMTPhost();
                        smtp.UseDefaultCredentials = false;
                        smtp.Timeout = 20000;
                        smtp.Port = 587;

                        //Pass in credentials
                        NetworkCredential creds = new NetworkCredential(council.sendgrid_username, council.sendgrid_password);
                        smtp.Credentials = creds;

                        //Add Attachments
                        byte[] bytes;
                        string fileName;

                        //Query for file attachments
                        using (SqlConnection connection = new SqlConnection())
                        {
                            connection.ConnectionString = Utility.GetConnectionString();
                            connection.Open();

                            SqlCommand command = new SqlCommand("dbo.uspGetCampRegistrationCommunicationFileAttachments", connection);
                            command.CommandTimeout = 999999999;
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add(new SqlParameter("@CampRegistrationID", SqlDbType.Int)).Value = regItem.CampRegistrationID;
                            command.Parameters.Add(new SqlParameter("@isCustomMessage", SqlDbType.Bit)).Value = regItem.isCustomMessage;

                            using (SqlDataReader reader = command.ExecuteReader())
                            {                                
                                //Loop over each file attachment and attach to message
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        if (!reader.IsDBNull(0))
                                        {
                                            bytes = (byte[])reader["attachment"];
                                            fileName = reader["filename"].ToString();

                                            MemoryStream f = new MemoryStream(bytes);
                                            Attachment data = new Attachment(f, fileName);
                                            message.Attachments.Add(data);
                                            
                                        }                                        
                                    }
                                }
                            }

                            connection.Close();
                        }

                        try
                        {
                            smtp.Send(message);
                            LogEvent.Log(String.Format("An Email was sent successfully. Email: {0} | CampSessCampProgID: {1} | CampRegID: {2}", regItem.HomeEmail, regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID.ToString()));
                            LogSentMessage(regItem.CampRegistrationID, regItem.CommunicationTypeCode, "EMA", actItem.EMAmessageText, "NA");
                        }
                        catch (Exception ex)
                        {
                            LogEvent.Log(String.Format("An SMTP error occurred. CampSessionCampProgramID: {0} | Email: {1} | Error: {2}", regItem.CampSessionCampProgramID.ToString(), regItem.HomeEmail, ex.Message));
                        }
                            
                    }

                    //Landing Page Notification
                    if (regItem.isMES)
                    {
                        //Query for file attachments
                        using (SqlConnection connection = new SqlConnection())
                        {
                            connection.ConnectionString = Utility.GetConnectionString();
                            connection.Open();

                            SqlCommand command = new SqlCommand("dbo.uspCommunicationMessageInsert", connection);
                            command.CommandTimeout = 999999999;
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add(new SqlParameter("@CommunicationMethodCode", SqlDbType.VarChar, 3)).Value = "MES";
                            command.Parameters.Add(new SqlParameter("@MsgContent", SqlDbType.VarChar, 140)).Value = FormatWith(regItem.MESmessageText, regItem);
                            command.Parameters.Add(new SqlParameter("@ProfileIDXML", SqlDbType.Xml)).Value = String.Format("<root><row><ProfileID>{0}</ProfileID></row></root>", regItem.ResponsibleProfileID);
                            command.Parameters.Add(new SqlParameter("@ChgdBy", SqlDbType.NVarChar, 128)).Value = "autoproc";
                           
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                int errorNumber;
                                int errorLine;
                                string errorMessage;
                                                                
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        if (!reader.IsDBNull(0))
                                        {
                                            errorNumber = (int)reader["ErrorNumber"];
                                            errorLine = (int)reader["ErrorLine"];
                                            errorMessage = (string)reader["ErrorMessage"];

                                            LogEvent.Log(String.Format("Error occured while sending a message notification for CampRegistrationID: {0} , Code: {1} , Error No: {2} , Line No.: {3} , Error Msg: {4}", regItem.CampRegistrationID, "MES", errorNumber, errorLine, errorMessage));
                                        }
                                        else
                                        {
                                            LogEvent.Log(String.Format("A message notification was sent successfully. ResponsibleProfileID: {0} | CampSessionCampProgram ID: {1} | CampRegistrationID: {2}", regItem.ResponsibleProfileID, regItem.CampSessionCampProgramID.ToString(), regItem.CampRegistrationID));
                                            LogSentMessage(regItem.CampRegistrationID, regItem.CommunicationTypeCode, "MES", actItem.MESmessageText, "NA");
                                        }
                                    }
                                }
                            }

                            connection.Close();
                        }

                                                  
                    }

                }
                                
            }
        }

        public static void LogSentMessage(int CampRegistrationID, string CommunicationTypeCode, string CommunicationMethodCode, string MessageText, string SwiftReachJobCode)
        {
            LogEvent.Log(String.Format("Record message sent for CampRegistrationID: {0}", CampRegistrationID));

            using (SqlConnection connection = new SqlConnection())
            {
                connection.ConnectionString = Utility.GetConnectionString();
                connection.Open();

                SqlCommand command = new SqlCommand("dbo.uspCampRegistrationCommunicationTypeMethodInsert", connection);
                command.CommandTimeout = 999999999;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@CampRegistrationID", SqlDbType.Int)).Value = CampRegistrationID;

                //If PWL, use PLA instead so they don't get 2 placed notices
                if (CommunicationTypeCode == "PWL")
                {
                    CommunicationTypeCode = "PLA";
                }

                command.Parameters.Add(new SqlParameter("@CommunicationTypeCode", SqlDbType.VarChar, 5)).Value = CommunicationTypeCode;
                command.Parameters.Add(new SqlParameter("@CommunicationMethodCode", SqlDbType.VarChar, 5)).Value = CommunicationMethodCode;
                command.Parameters.Add(new SqlParameter("@MessageText", SqlDbType.VarChar, 500)).Value = MessageText;
                command.Parameters.Add(new SqlParameter("@isSent", SqlDbType.Bit)).Value = true;
                command.Parameters.Add(new SqlParameter("@SwiftReachJobCode", SqlDbType.VarChar, 25)).Value = SwiftReachJobCode;
                command.Parameters.Add(new SqlParameter("@ChgdBy", SqlDbType.NVarChar, 128)).Value = "autoproc";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int errorNumber;
                    int errorLine;
                    string errorMessage;

                    //Loop over each Activity Registration
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                errorNumber = (int)reader["ErrorNumber"];
                                errorLine = (int)reader["ErrorLine"];
                                errorMessage = (string)reader["ErrorMessage"];

                                LogEvent.Log(String.Format("Error occured on dbo.uspCampRegistrationCommunicationTypeMethodInsert for CampRegistrationID: {0} , SwiftReachJobCode: {1} , Error No: {2} , Line No.: {3} , Error Msg: {4}", CampRegistrationID, SwiftReachJobCode, errorNumber, errorLine, errorMessage));
                            }
                            else
                            {
                                LogEvent.Log(String.Format("Message sent status updated for CampRegistrationID: {0} , SwiftReachJobCode: {1}", CampRegistrationID, SwiftReachJobCode));
                            }
                        }
                    }
                }

                connection.Close();
            }
        }


        public static string FormatWith(this string format, object source)
        {
            try
            {
                //This method will replace placeholders with data from the source
                StringBuilder sbResult = new StringBuilder(format.Length);
                StringBuilder sbCurrentTerm = new StringBuilder();
                char[] formatChars = format.ToCharArray();
                bool inTerm = false;
                object currentPropValue = source;

                for (int i = 0; i < format.Length; i++)
                {
                    if (formatChars[i] == '[')
                        inTerm = true;
                    else if (formatChars[i] == ']')
                    {
                        PropertyInfo pi = currentPropValue.GetType().GetProperty(sbCurrentTerm.ToString());
                        sbResult.Append((string)(pi.PropertyType.GetMethod("ToString", new Type[] { }).Invoke(pi.GetValue(currentPropValue, null), null)));
                        sbCurrentTerm.Clear();
                        inTerm = false;
                        currentPropValue = source;
                    }
                    else if (inTerm)
                    {
                        if (formatChars[i] == '.')
                        {
                            PropertyInfo pi = currentPropValue.GetType().GetProperty(sbCurrentTerm.ToString());
                            currentPropValue = pi.GetValue(source, null);
                            sbCurrentTerm.Clear();
                        }
                        else
                            sbCurrentTerm.Append(formatChars[i]);
                    }
                    else
                        sbResult.Append(formatChars[i]);
                }
                return sbResult.ToString();
            }
            catch
            {
                return format;
            }
        }

        public static IEnumerable<IEnumerable<T>> Subsequences<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            // Ensure that the source IEnumerable is evaluated only once
            return subsequences(source.ToArray());
        }

        private static IEnumerable<IEnumerable<T>> subsequences<T>(IEnumerable<T> source)
        {
            if (source.Any())
            {
                foreach (var comb in subsequences(source.Skip(1)))
                {
                    yield return comb;
                    yield return source.Take(1).Concat(comb);
                }
            }
            else
            {
                yield return Enumerable.Empty<T>();
            }
        }
                

    }
}

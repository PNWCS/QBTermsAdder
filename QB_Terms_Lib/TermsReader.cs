using QBFC16Lib;
using Serilog;
// testing file update
namespace QB_Terms_Lib
{
    public class TermsReader
    {
        // Static constructor runs ONCE when the class is first used
        static TermsReader()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("TermsReader Initialized.");
        }

        public static List<PaymentTerm> QueryAllTerms()
        {
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager? sessionManager = null;

            try
            {
                //Create the session Manager object
                sessionManager = new QBSessionManager();

                //Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                IStandardTermsQuery StandardTermsQueryRq = requestMsgSet.AppendStandardTermsQueryRq();

                //Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", AppConfig.QB_APP_NAME);
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                //Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                //End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;

                Log.Information("TermsReader Completed");
                return WalkStandardTermsQueryRs(responseMsgSet);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                if (sessionBegun && sessionManager != null)
                {
                    sessionManager.EndSession();
                }
                if (connectionOpen && sessionManager != null)
                {
                    sessionManager.CloseConnection();
                }
                return new List<PaymentTerm>();
            }
        }

        static List<PaymentTerm> WalkStandardTermsQueryRs(IMsgSetResponse responseMsgSet)
        {
            List<PaymentTerm> results = new List<PaymentTerm>();
            if (responseMsgSet == null) return results;
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return results;
            //if we sent only one request, there is only one response, we'll walk the list for this sample
            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                //check the status code of the response, 0=ok, >0 is warning
                if (response.StatusCode == 0 || response.StatusCode == 1)
                {
                    //the request-specific response is in the details, make sure we have some
                    if (response.Detail != null)
                    {
                        //make sure the response is the type we're expecting
                        ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                        if (responseType == ENResponseType.rtStandardTermsQueryRs)
                        {
                            //upcast to more specific type here, this is safe because we checked with response.Type check above
                            IStandardTermsRetList StandardTermsRet = (IStandardTermsRetList)response.Detail;
                            return WalkStandardTermsRet(StandardTermsRet);
                        }
                    }
                }
                else
                {
                    throw new Exception($"QB API Returned Error Code {response.StatusCode}");
                }
            }
            return results;
        }
        static List<PaymentTerm> WalkStandardTermsRet(IStandardTermsRetList StandardTermsRetList)
        {
            List<PaymentTerm> terms = new List<PaymentTerm>();
            for (int i = 0; i < StandardTermsRetList.Count; i++)
            {
                var StandardTermsRet = StandardTermsRetList.GetAt(i);

                if (StandardTermsRet == null) return terms;
                //Go through all the elements of IStandardTermsRetList
                //Get value of QB ID
                string qbID = (string)StandardTermsRet.ListID.GetValue();
                //Get value of Name
                string name = (string)StandardTermsRet.Name.GetValue();
                //Get value of StdDiscountDays
                int companyID = -1; // indicate no companyID
                if (StandardTermsRet.StdDiscountDays != null)
                {
                    companyID = (int)StandardTermsRet.StdDiscountDays.GetValue();
                }
                Log.Debug($"Name: {name}, CompanyID: {companyID}");
                terms.Add(new PaymentTerm(qbID, name, companyID));
            }
            return terms;
        }
    }
}

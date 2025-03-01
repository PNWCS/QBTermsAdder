using QBFC16Lib;
using Serilog;

namespace QB_Terms_Lib
{
    public class TermsAdder
    {
        public static void AddTerms(List<PaymentTerm> terms)
        {
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "QB_Terms_Integration");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // Append terms to the request
                foreach (var term in terms)
                {
                    IStandardTermsAdd standardTermsAddRq = requestMsgSet.AppendStandardTermsAddRq();
                    standardTermsAddRq.Name.SetValue(term.Name);
                    standardTermsAddRq.IsActive.SetValue(true);

                }

                Log.Information("Sending {Count} terms to QuickBooks...", terms.Count);

                // Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                // Process response
                WalkStandardTermsAddRs(responseMsgSet, terms);

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;
            }
            catch (Exception e)
            {
                Log.Error("Error adding terms to QuickBooks: {Message}", e.Message);
                if (sessionBegun)
                {
                    sessionManager.EndSession();
                }
                if (connectionOpen)
                {
                    sessionManager.CloseConnection();
                }
            }
        }

        public static void WalkStandardTermsAddRs(IMsgSetResponse responseMsgSet, List<PaymentTerm> terms)
        {
            if (responseMsgSet == null) return;
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return;

            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);

                if (response.StatusCode == 0 && response.Detail != null)
                {
                    ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                    if (responseType == ENResponseType.rtStandardTermsAddRs)
                    {
                        IStandardTermsRet StandardTermsRet = (IStandardTermsRet)response.Detail;
                        WalkStandardTermsRet(StandardTermsRet, terms);
                    }
                }
                else
                {
                    Log.Warning("QuickBooks returned error: {StatusCode} - {StatusMessage}",
                                response.StatusCode, response.StatusMessage);
                }
            }
        }

        public static void WalkStandardTermsRet(IStandardTermsRet StandardTermsRet, List<PaymentTerm> terms)
        {
            if (StandardTermsRet == null) return;

            string listID = StandardTermsRet.ListID.GetValue();
            string name = StandardTermsRet.Name.GetValue();
            DateTime timeCreated = StandardTermsRet.TimeCreated.GetValue();

            Log.Information("Successfully added term: {Name} (ListID: {ListID}, Created: {TimeCreated})",
                            name, listID, timeCreated);

            var term = terms.FirstOrDefault(t => t.Name == name);
            if (term != null)
            {
                term.Status = PaymentTermStatus.Added;
            }
        }
    }
}

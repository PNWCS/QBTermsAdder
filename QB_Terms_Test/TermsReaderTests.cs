using System.Diagnostics;
using QBFC16Lib; // Ensure this COM reference is added to your project

namespace QB_Terms_Test
{
    public class TermsReaderTests
    {
        [Fact]
        public void QueryAllTerms_OneTerm()
        {
            string newTermListID = string.Empty;
            string randomName = "Term_" + Guid.NewGuid().ToString().Substring(0, 8);
            int randomDays = new Random().Next(1, 30);

            using (var qbSession = new QuickBooksSession("Integration Test - Standard Terms"))
            {
                try
                {
                    // 1) Insert a single Standard Term
                    newTermListID = InsertSingleStandardTerm(qbSession, randomName, randomDays);

                    // 2) Query all terms using existing method
                    var allTerms = QB_Terms_Lib.TermsReader.QueryAllTerms();
                    Assert.NotNull(allTerms);

                    // 3) Verify the new term exists in QuickBooks
                    var insertedTerm = allTerms.FirstOrDefault(t => t.Name.Equals(randomName, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(insertedTerm);
                    Assert.Equal(newTermListID, insertedTerm.QB_ID);
                }
                finally
                {
                    // 4) Cleanup: Delete the inserted term (even if test fails)
                    if (!string.IsNullOrEmpty(newTermListID))
                    {
                        DeleteStandardTerm(qbSession, newTermListID);
                    }
                }
            }
        }

        [Fact]
        public void QueryAllTerms_100Terms_ShouldNotExceed2Sec()
        {
            const int termCount = 100;
            var insertedTermListIDs = new List<string>();
            var insertedNames = new HashSet<string>();
            const int MaxQueryTimeMilliseconds = 2000; // 2 seconds

            using (var qbSession = new QuickBooksSession("Integration Test - Bulk Insert Standard Terms"))
            {
                try
                {
                    // 1) Insert 100 Standard Terms
                    for (int i = 0; i < termCount; i++)
                    {
                        string randomName = "Term_" + Guid.NewGuid().ToString().Substring(0, 8);
                        int randomDays = new Random().Next(1, 30);

                        string listID = InsertSingleStandardTerm(qbSession, randomName, randomDays);
                        insertedTermListIDs.Add(listID);
                        insertedNames.Add(randomName);
                    }

                    // 2) Measure Query Time
                    var stopwatch = Stopwatch.StartNew();
                    var allTerms = QB_Terms_Lib.TermsReader.QueryAllTerms();
                    stopwatch.Stop();


                    // 3) Assert Query Completes Within 2 Seconds
                    long elapsedMs = stopwatch.ElapsedMilliseconds; // Get actual time in milliseconds

                    Assert.True(elapsedMs <= MaxQueryTimeMilliseconds,
                        $"Query took {elapsedMs}ms, exceeding 2-second limit.");

                    // 4) Ensure All 100 Inserted Terms Were Found
                    var queriedTermNames = new HashSet<string>(allTerms.Select(t => t.Name));
                    foreach (var name in insertedNames)
                    {
                        Assert.Contains(name, queriedTermNames);
                    }
                }
                finally
                {
                    // 5) Cleanup: Delete all inserted terms
                    foreach (var listID in insertedTermListIDs)
                    {
                        DeleteStandardTerm(qbSession, listID);
                    }
                }
            }
        }
        /// <summary>
        /// Inserts a Standard Term and returns its ListID.
        /// </summary>
        private string InsertSingleStandardTerm(QuickBooksSession qbSession, string name, int dueDays)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IStandardTermsAdd stdTermsAddRq = requestMsgSet.AppendStandardTermsAddRq();
            stdTermsAddRq.Name.SetValue(name);
            stdTermsAddRq.IsActive.SetValue(true);
            stdTermsAddRq.StdDueDays.SetValue(dueDays);
            stdTermsAddRq.StdDiscountDays.SetValue(dueDays);
            stdTermsAddRq.DiscountPct.SetValue(5.0); // Example discount %

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            return ExtractListIDFromResponse(responseMsgSet);
        }


        /// <summary>
        /// Deletes a Standard Term using ListDelRq.
        /// </summary>
        private void DeleteStandardTerm(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtStandardTerms);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            WalkListDelResponse(responseMsgSet, listID);
        }

        /// <summary>
        /// Extracts ListID from response.
        /// </summary>
        private string ExtractListIDFromResponse(IMsgSetResponse responseMsgSet)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                throw new Exception("No response from StandardTermsAddRq.");

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0)
                throw new Exception($"StandardTermsAdd failed: {response.StatusMessage}");

            IStandardTermsRet? stdTermRet = response.Detail as IStandardTermsRet;
            if (stdTermRet == null)
                throw new Exception("No IStandardTermsRet returned after adding Standard Term.");

            string name = stdTermRet.ListID?.GetValue() ?? "";
            Console.WriteLine($"Inserted Standard Term with ListID: {name}");
            return name;
        }

        /// <summary>
        /// Processes the response for a ListDel request.
        /// </summary>
        private void WalkListDelResponse(IMsgSetResponse responseMsgSet, string listID)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode >= 0 && response.Detail != null)
            {
                Console.WriteLine($"Successfully deleted Standard Term (ListID: {listID}) using ListDelRq.");
            }
            else
            {
                Console.WriteLine($"Error Deleting Standard Term: {response.StatusMessage}");
            }
        }
    }

    /// <summary>
    /// Encapsulates QuickBooks session handling.
    /// </summary>
    public class QuickBooksSession : IDisposable
    {
        private QBSessionManager _sessionManager;
        private bool _sessionBegun;
        private bool _connectionOpen;

        public QuickBooksSession(string appName)
        {
            _sessionManager = new QBSessionManager();
            _sessionManager.OpenConnection("", appName);
            _connectionOpen = true;
            _sessionManager.BeginSession("", ENOpenMode.omDontCare);
            _sessionBegun = true;
        }

        public IMsgSetRequest CreateRequestSet()
        {
            IMsgSetRequest requestMsgSet = _sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;
            return requestMsgSet;
        }

        public IMsgSetResponse SendRequest(IMsgSetRequest requestMsgSet)
        {
            return _sessionManager.DoRequests(requestMsgSet);
        }

        public void Dispose()
        {
            if (_sessionBegun)
            {
                _sessionManager.EndSession();
            }
            if (_connectionOpen)
            {
                _sessionManager.CloseConnection();
            }
        }
    }
}

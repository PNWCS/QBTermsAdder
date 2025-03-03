using QBFC16Lib;
using System.Diagnostics;
using Xunit;
using QB_Terms_Lib; // Your library namespace

namespace QB_Terms_Test
{
    public class TermsAdderTests
    {
        [Fact]
        public void AddMultipleTerms_TimeLimit()
        {
            const int termCount = 10;
            const int startingCompanyID = 100;
            const int timeLimit = 5000; // 2 seconds
            List<PaymentTerm> termsToAdd = new List<PaymentTerm>();

            // Build a list of 100 random PaymentTerm objects
            for (int i = 0; i < termCount; i++)
            {
                string randomName = "TestTerm_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = startingCompanyID + i;
                termsToAdd.Add(new PaymentTerm(randomName, companyID));
            }

            // We'll store the time outside so we can still do a cleanup in 'finally'
            Stopwatch stopwatch = null;

            try
            {
                // 1) Measure time
                stopwatch = Stopwatch.StartNew();
                TermsAdder.AddTerms(termsToAdd);
                stopwatch.Stop();

                long elapsedMs = stopwatch.ElapsedMilliseconds;

                // 2) Assert that it took less than time limit
                Assert.True(elapsedMs < timeLimit,
                    $"Adding {termCount} terms took {elapsedMs} ms, which exceeds {timeLimit/1000} seconds.");
            }
            finally
            {
                // 3) Cleanup: Only delete terms that successfully added (Status=Added) and have a QB_ID
                var addedTerms = termsToAdd
                    .Where(t => t.Status == PaymentTermStatus.Added && !string.IsNullOrEmpty(t.QB_ID))
                    .ToList();

                using (var qbSession = new QuickBooksSession("Integration Test - Delete Standard Terms"))
                {
                    foreach (var term in addedTerms)
                    {
                        DeleteStandardTerm(qbSession, term.QB_ID);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a Standard Term using ListDelRq.
        /// Reuses the same approach from your existing tests.
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
        /// Processes the response for a ListDel request.
        /// </summary>
        private void WalkListDelResponse(IMsgSetResponse responseMsgSet, string listID)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode == 0 && response.Detail != null)
            {
                // Success
                Debug.WriteLine($"Successfully deleted Standard Term (ListID: {listID}).");
            }
            else
            {
                // Log or throw an exception as needed
                Debug.WriteLine($"Error Deleting Standard Term: {response.StatusMessage}");
            }
        }
    }
}

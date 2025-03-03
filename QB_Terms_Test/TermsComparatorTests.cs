using QB_Terms_Lib; // Adjust to your actual namespace for TermsComparator, PaymentTerm, etc.
using QBFC16Lib; // Needed for QuickBooks interaction

namespace QB_Terms_Test
{
    public class TermsComparatorTests
    {
        [Fact]
        public void CompareTerms_InMemoryScenario()
        {
            // 1️ Create 5 new PaymentTerms in memory
            const int startId = 100;
            var initialTerms = new List<PaymentTerm>();

            for (int i = 0; i < 5; i++)
            {
                string termName = "TestTerm_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyId = startId + i;
                initialTerms.Add(new PaymentTerm(termName, companyId));
            }

            List<PaymentTerm> firstCompareResult = null;
            List<PaymentTerm> secondCompareResult = null;

            try
            {
                // 2️ First Compare: Expect all terms to be 'Added' in QuickBooks
                firstCompareResult = TermsComparator.CompareTerms(initialTerms);

                // Ensure all 5 terms were correctly added to QuickBooks.
                foreach (var term in firstCompareResult.Where(t => initialTerms.Any(x => x.Company_ID == t.Company_ID)))
                {
                    Assert.Equal(PaymentTermStatus.Added, term.Status);
                }

                // 3️ Modify Terms:
                //    a) Remove one term (simulate 'Missing' scenario)
                //    b) Change the name of one term (simulate 'Different' scenario)
                var updatedTerms = new List<PaymentTerm>(initialTerms);
                var termToRemove = updatedTerms[0]; // Simulate "Missing"
                var termToRename = updatedTerms[1]; // Simulate "Different"
                updatedTerms.Remove(termToRemove);
                termToRename.Name += "_Modified";

                // 4️ Second Compare: Expect 'Missing' & 'Different' statuses
                secondCompareResult = TermsComparator.CompareTerms(updatedTerms);
                var secondCompareDict = secondCompareResult.ToDictionary(t => t.Company_ID);

                // Check the removed term is marked as 'Missing'
                Assert.True(secondCompareDict.ContainsKey(termToRemove.Company_ID), "Missing term was not found in results.");
                Assert.Equal(PaymentTermStatus.Missing, secondCompareDict[termToRemove.Company_ID].Status);

                // Check the renamed term is marked as 'Different'
                Assert.True(secondCompareDict.ContainsKey(termToRename.Company_ID), "Renamed term was not found in results.");
                Assert.Equal(PaymentTermStatus.Different, secondCompareDict[termToRename.Company_ID].Status);

                // Ensure the remaining 3 terms remain 'Unchanged'
                var unaffectedIds = updatedTerms
                    .Select(t => t.Company_ID)
                    .Except(new[] { termToRename.Company_ID })
                    .ToArray();

                foreach (var id in unaffectedIds)
                {
                    var term = secondCompareDict[id];
                    Assert.Equal(PaymentTermStatus.Unchanged, term.Status);
                }
            }
            finally
            {
                // 5️ Cleanup: Delete the 5 test-created terms from QuickBooks
                var allAddedTerms = firstCompareResult?.Where(t => t.Status == PaymentTermStatus.Added && !string.IsNullOrEmpty(t.QB_ID)).ToList();

                if (allAddedTerms != null && allAddedTerms.Count > 0)
                {
                    using (var qbSession = new QuickBooksSession("Integration Test - Cleanup Standard Terms"))
                    {
                        foreach (var term in allAddedTerms)
                        {
                            DeleteStandardTerm(qbSession, term.QB_ID);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a Standard Term from QuickBooks using ListDelRq.
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
                Console.WriteLine($"Successfully deleted Standard Term (ListID: {listID}).");
            }
            else
            {
                Console.WriteLine($"Error Deleting Standard Term: {response.StatusMessage}");
            }
        }
    }
}

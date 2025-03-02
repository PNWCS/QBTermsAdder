using Serilog;

namespace QB_Terms_Lib
{
    public class TermsComparator
    {
        // Static constructor runs ONCE when the class is first used
        static TermsComparator()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("TermsComparator Initialized.");
        }

        public static List<PaymentTerm> CompareTerms(List<PaymentTerm> companyTerms)
        {
            // Read QB terms
            List<PaymentTerm> qbTerms = TermsReader.QueryAllTerms();

            // Convert QuickBooks and Company terms into dictionaries for quick lookup
            var qbTermDict = new Dictionary<int, PaymentTerm>();

            foreach (var term in qbTerms)
            {
                qbTermDict[term.Company_ID] = term; // Add new term

            }

            var companyTermDict = new Dictionary<int, PaymentTerm>();

            foreach (var term in companyTerms)
            {
                companyTermDict[term.Company_ID] = term; // Last duplicate overwrites previous one
            }

            List<PaymentTerm> newTermsToAdd = new List<PaymentTerm>();

            // Iterate through company terms to compare with QB terms
            foreach (var companyTerm in companyTerms)
            {
                if (qbTermDict.TryGetValue(companyTerm.Company_ID, out var qbTerm))
                {
                    // Term exists in both, compare names
                    if (qbTerm.Name == companyTerm.Name)
                    {
                        companyTerm.Status = PaymentTermStatus.Unchanged;
                    }
                    else
                    {
                        companyTerm.Status = PaymentTermStatus.Different;
                    }
                }
                else
                {
                    // Term does not exist in QB, queue for addition
                    newTermsToAdd.Add(companyTerm);
                }
            }

            // Check for terms that exist in QB but not in the company file
            foreach (var qbTerm in qbTerms)
            {
                if (!companyTermDict.ContainsKey(qbTerm.Company_ID))
                {
                    qbTerm.Status = PaymentTermStatus.Missing;
                }
            }

            // Call TermsAdder to add new terms to QuickBooks
            if (newTermsToAdd.Count > 0)
            {
                TermsAdder.AddTerms(newTermsToAdd);

                // Ensure Added terms are updated in companyTermDict
                foreach (var addedTerm in newTermsToAdd)
                {
                    if (companyTermDict.TryGetValue(addedTerm.Company_ID, out var companyTerm))
                    {
                        companyTerm.Status = addedTerm.Status;
                    }
                }
            }

            // Merge companyTermDict with qbTermDict (removing duplicates)
            Dictionary<int, PaymentTerm> mergedTermsDict = new Dictionary<int, PaymentTerm>();

            foreach (var term in qbTermDict.Values)
                mergedTermsDict[term.Company_ID] = term; // Add all QB terms

            foreach (var term in companyTermDict.Values)
                mergedTermsDict[term.Company_ID] = term; // Overwrite with company terms

            // Convert merged dictionary back to a list
            Log.Information("TermsComparator Completed");
            return mergedTermsDict.Values.ToList();
        }
    }
}
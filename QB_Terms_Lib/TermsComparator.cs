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

            // Group qbTerms by Company_ID to avoid duplicates and use the first term for each key.
            var qbTermDict = qbTerms
                .GroupBy(t => t.Company_ID)
                .ToDictionary(g => g.Key, g => g.First());

            // Assume companyTerms are unique by Company_ID.
            var companyTermDict = companyTerms.ToDictionary(t => t.Company_ID);

            // Use a dictionary to merge results without duplicates.
            var resultTerms = new Dictionary<int, PaymentTerm>();

            // Compare each company term with the corresponding QB term.
            foreach (var companyTerm in companyTerms)
            {
                if (qbTermDict.TryGetValue(companyTerm.Company_ID, out var qbTerm))
                {
                    // If term exists in QB, compare names.
                    companyTerm.Status = qbTerm.Name == companyTerm.Name
                        ? PaymentTermStatus.Unchanged
                        : PaymentTermStatus.Different;
                    resultTerms[companyTerm.Company_ID] = companyTerm;
                }
                else
                {
                    // Term is not in QB, mark it as Added.
                    companyTerm.Status = PaymentTermStatus.Added;
                    resultTerms[companyTerm.Company_ID] = companyTerm;
                }
            }

            // For QB terms that don't appear in the company list, mark them as Missing.
            foreach (var qbTerm in qbTerms.GroupBy(t => t.Company_ID).Select(g => g.First()))
            {
                if (!companyTermDict.ContainsKey(qbTerm.Company_ID))
                {
                    qbTerm.Status = PaymentTermStatus.Missing;
                    resultTerms[qbTerm.Company_ID] = qbTerm;
                }
            }

            // Add new terms to QuickBooks.
            var newTermsToAdd = resultTerms.Values
                .Where(t => t.Status == PaymentTermStatus.Added)
                .ToList();
            if (newTermsToAdd.Count > 0)
            {
                TermsAdder.AddTerms(newTermsToAdd);
            }

            Log.Information("TermsComparator Completed");
            return resultTerms.Values.ToList();
        }


    }
}
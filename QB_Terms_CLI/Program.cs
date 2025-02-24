using System.Data;
using ClosedXML.Excel;
using QB_Terms_Lib;

namespace qb_payment
{
    public class Sample
    {
        public static void Main(string[] args)
        {
            string filePath = "..\\..\\..\\..\\..\\Sample_Company_Data.xlsx";

            List<PaymentTerm> companyTerms = new List<PaymentTerm>();

            // Ensure file exists
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("payment_terms");

                // Get the range of used rows

                var range = worksheet.RangeUsed();
                if (range == null)
                {
                    Console.WriteLine("Warning: The worksheet is empty or contains no used range.");
                }
                else
                {
                    var rows = range.RowsUsed();
                    foreach (var row in rows.Skip(1)) // Skip header row
                    {
                        string name = row.Cell(1).GetString().Trim();  // Column "Name"
                        int companyID = row.Cell(2).GetValue<int>();   // Column "ID"

                        companyTerms.Add(new PaymentTerm(name, companyID));
                    }
                }
            }

            List<PaymentTerm> terms = TermsComparator.CompareTerms(companyTerms);
            foreach(var term in terms)
            {
                Console.WriteLine($"Term {term.Name} has the {term.Status} Status");
            }

            Console.WriteLine("Data Sync Completed"); 
        }

    }
}
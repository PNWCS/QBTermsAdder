namespace QB_Terms_Lib
{
    public class PaymentTerm
    {
        public string QB_ID { get; set; }
        public string Name { get; set; }
        public int Company_ID { get; set; }
        public PaymentTermStatus Status { get; set; }

        public PaymentTerm(string name, int companyID)
        {
            QB_ID = string.Empty;
            Name = name;
            Company_ID = companyID;
            Status = PaymentTermStatus.Unknown;
        }

        public PaymentTerm(string qbID, string name, int companyID)
        {
            QB_ID = qbID;
            Name = name;
            Company_ID = companyID;
            Status = PaymentTermStatus.Unknown;
        }
    }

    public enum PaymentTermStatus
    {
        Unknown, // When first read from the company excel or QB
        Unchanged, // Exists in both but no changes
        Different, // Exists in both but name is different
        Added,     // Newly added to QB
        FailedToAdd, // If adding to QB failed
        Missing    // Exists in QB but not in the company file
    }
}
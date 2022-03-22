namespace Awesome_CMDB_DataAccess_Models
{
    public class Account
    {
        public DatacenterType DatacenterType { get; set; }
        public string AccountName { get; set; }
        public string AccountId { get; set; }
    }

    public enum DatacenterType
    {
        Unknown,
        AWS
    }
}

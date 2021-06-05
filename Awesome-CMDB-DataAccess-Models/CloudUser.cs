using System;

namespace Awesome_CMDB_DataAccess_Models
{
    public class CloudUser
    {
        public string Id { get; set; }
        public string User { get; set; }
        public string Email { get; set; }
        public bool? Active { get; set; } = null;
        public DateTime? CreateDate { get; set; } = null;
        public DateTime? UpdateDate { get; set; } = null;
        public DateTime? PasswordLastUsed { get; set; } = null;
    }
}
using System.Collections.Generic;

namespace Awesome_CMDB_DataAccess_Models
{
    public class ServerGroup
    {
        public string GroupName { get; set; }
        public string GroupId { get; set; }
        public string Region { get; set; }
        public List<ServerDetails> Servers { get; set; } = new List<ServerDetails>();
    }
}
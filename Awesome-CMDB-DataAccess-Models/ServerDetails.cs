using System;
using System.Collections.Generic;

namespace Awesome_CMDB_DataAccess_Models
{
    public class ServerDetails
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Flavour { get; set; }
        public double Ram { get; set; }
        public int Cpu { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
        public DateTime? Updated { get; set; } = null;
        public DateTime? Created { get; set; } = null;
        public DateTime? Terminated { get; set; } = null;
        public string CreatorEmail { get; set; }
        public string Status { get; set; }
        public List<IpV4Network> Ipv4Networks { get; set; } = new List<IpV4Network>();
        public List<VolumeDetail> Volumes { get; set; } = new List<VolumeDetail>();
        public string AvailabilityZone { get; set; }
        public DateTime? Deleted { get; set; } = null;
        public string DatacenterType { get; set; }
        public string AccountId { get; set; }
    }
}
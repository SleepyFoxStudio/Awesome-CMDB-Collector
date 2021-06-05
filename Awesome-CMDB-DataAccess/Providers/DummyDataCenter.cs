using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Awesome_CMDB_DataAccess_Models;

namespace Awesome_CMDB_DataAccess.Providers
{
    public class DummyDataCenter : IDatacenter
    {

        public async Task<List<ServerGroup>> GetServerGroupsAsync()
        {
            return new List<ServerGroup>
            {
                new ServerGroup
                {
                    GroupName = "EU (Ireland)",
                    GroupId = "eu-west-1",
                    Region = "eu-west-1",
                    Servers = new List<ServerDetails>
                    {
                        new ServerDetails
                        {
                            DatacenterType = "AWS",
                            AvailabilityZone = "eu-west-1b",
                            Tags = new Dictionary<string, string>
                            {
                                {"Name", "MyTestServer"},
                                {"Owner", "Bob Smith"}
                            },
                            Cpu = 4,
                            Created = DateTime.Today,
                            CreatorEmail = "foo@bar.com",
                            Deleted = null,
                            Flavour = "a1.4xlarge",
                            Id = "i-123456",
                            Ipv4Networks = new List<IpV4Network>(),
                            Name = "MyTestServer",
                            Ram = 20,
                            Status = "running",
                            Terminated = null,
                            Updated = null,
                            Volumes = new List<VolumeDetail>()
                        }
                    }
                }
            };
        }
    }
}

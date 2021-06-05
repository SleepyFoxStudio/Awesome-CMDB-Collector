using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Pricing;
using Amazon.Pricing.Model;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Awesome_CMDB_DataAccess_Models;
using Newtonsoft.Json.Linq;
using Filter = Amazon.Pricing.Model.Filter;
using VolumeDetail = Awesome_CMDB_DataAccess_Models.VolumeDetail;

namespace Awesome_CMDB_DataAccess.Providers
{
    public class AwsDatacenter : IDatacenter
    {
        private readonly BasicAWSCredentials _awsCreds;
        private readonly string _accessKeyId;
        private readonly string _secretKey;

        public AwsDatacenter(string accessKeyId, string secretKey)
        {
            _awsCreds = new BasicAWSCredentials(accessKeyId, secretKey);
            _accessKeyId = accessKeyId;
            _secretKey = secretKey;
        }

        public async Task<List<ServerGroup>> GetServerGroupsAsync()
        {
            var serverGroups = new List<ServerGroup>();
            var client = new AmazonEC2Client(_awsCreds, RegionEndpoint.EUWest1);

            var regionRequest = new DescribeRegionsRequest();
            var regionResponse = await client.DescribeRegionsAsync(regionRequest, CancellationToken.None);


            var stsClient = new AmazonSecurityTokenServiceClient(_awsCreds, RegionEndpoint.EUWest1);
            var getCallerIdentityResponse = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());


            var iamClient = new AmazonIdentityManagementServiceClient(_awsCreds, RegionEndpoint.EUWest1);
            var accountAliases = await iamClient.ListAccountAliasesAsync(new ListAccountAliasesRequest());


            var accountName = accountAliases.AccountAliases.SingleOrDefault();
            if (string.IsNullOrEmpty(accountName))
            {
                accountName = getCallerIdentityResponse.Account;
            }




            foreach (var region in regionResponse.Regions)
            {
                var servers = new List<ServerDetails>();
                string nextToken = null;
                while (true)
                {
                    var allVolumes = new List<Volume>();
                    var request = new DescribeInstancesRequest
                    {
                        NextToken = nextToken
                    };
                    var instanceClient = new AmazonEC2Client(_awsCreds, RegionEndpoint.GetBySystemName(region.RegionName));
                    var response = await instanceClient.DescribeInstancesAsync(request, CancellationToken.None).ConfigureAwait(false);
                    if (response.Reservations.Count > 0)
                    {
                        allVolumes = await GetAllVolumesAsync(region).ConfigureAwait(false);
                    }

                    foreach (var item in response.Reservations)
                    {
                        foreach (var server in item.Instances)
                        {
                            var metadata = new Dictionary<string, string>();
                            if (item.Instances[0].Tags.Count > 0)
                            {
                                foreach (var tag in item.Instances[0].Tags)
                                {
                                    metadata.Add(tag.Key, tag.Value);
                                }
                            }

                            servers.Add(new ServerDetails
                            {
                                Name = server.Tags.SingleOrDefault(k => k.Key.Equals("name", StringComparison.InvariantCultureIgnoreCase))?.Value,
                                Id = server.InstanceId,
                                AccountId = getCallerIdentityResponse.Account,
                                Tags = metadata,
                                Updated = null,
                                Created = server.LaunchTime,
                                Flavour = server.InstanceType.Value,
                                Cpu = server.CpuOptions.CoreCount * server.CpuOptions.ThreadsPerCore,
                                Volumes = GetVolumes(server.BlockDeviceMappings, allVolumes),
                                Status = server.State.Name,
                                Ipv4Networks = GetNetworks(server.NetworkInterfaces),
                                AvailabilityZone = server.Placement.AvailabilityZone,
                                DatacenterType = "AWS"
                            });
                        }


                    }
                    if (response.NextToken == null)
                    {
                        break;
                    }
                    nextToken = response.NextToken;
                }



                Console.WriteLine($"{region.RegionName} contains {servers.Count} servers");

                if (servers.Count > 0)
                {
                    serverGroups.Add(new ServerGroup() { GroupName = $"{accountName} {region.RegionName}", Region = region.RegionName, Servers = servers });
                }
            }


            var allInstanceTypes = serverGroups.SelectMany(a => a.Servers).Select(s => s.Flavour).Distinct();

            var priceListClient = new AmazonPricingClient(_awsCreds, RegionEndpoint.USEast1);
            var getInstanceTypeTasks = allInstanceTypes.Select(t => priceListClient.GetProductsAsync(new GetProductsRequest
            {
                ServiceCode = "AmazonEC2",
                Filters = new List<Filter>
                {
                    new Filter
                    {
                        Type = FilterType.TERM_MATCH,
                        Field = "instanceType",
                        Value = t
                    }
                },
                // We only want the first result, as there are many many pricing options for a given instanceType, 
                // and we only want memory and vCPUs, which are the same for all options.
                MaxResults = 1
            }));

            var instanceTypeResponses = await Task.WhenAll(getInstanceTypeTasks).ConfigureAwait(false);

            var instanceTypeLookup = instanceTypeResponses
                .Select(r => JObject.Parse(r.PriceList[0])["product"]["attributes"])
                .Select(j => (memory: j["memory"].Value<string>(), vcpu: j["vcpu"].Value<string>(), instanceType: j["instanceType"].Value<string>()))
                .ToDictionary(t => t.instanceType);


            foreach (var server in serverGroups.SelectMany(a => a.Servers))
            {

                if (instanceTypeLookup.TryGetValue(server.Flavour, out var t))
                {
                    server.Cpu = int.Parse(t.vcpu);
                    server.Ram = ByteSize.Parse(t.memory).GigaBytes;
                }
            }


            return serverGroups;
        }

        private async Task<List<CloudDatabase>> GetDatabasesAsync()
        {
            var rdsClient = new AmazonRDSClient(_accessKeyId, _secretKey, RegionEndpoint.EUWest1);
            var rdsRequest = new DescribeDBInstancesRequest
            {
                MaxRecords = 100
            };
            var rdsInstances = await rdsClient.DescribeDBInstancesAsync().ConfigureAwait(false);
            var databases = new List<CloudDatabase>();
            foreach (var instance in rdsInstances.DBInstances)
            {
                databases.Add(new CloudDatabase
                {
                    Id = instance.DbiResourceId,
                    Engine = instance.Engine,
                    Name = instance.DBInstanceIdentifier,
                    Version = instance.EngineVersion,
                    AvailabilityZone = instance.AvailabilityZone
                });
            }

            return databases;
        }

        private async Task<List<CloudUser>> GetUsers()
        {
            var users = new List<CloudUser>();

            try
            {
                var iamClient = new AmazonIdentityManagementServiceClient(_accessKeyId, _secretKey, RegionEndpoint.EUWest1);
                var listUsersRequest = new ListUsersRequest
                {

                };



                var iamUsers = await iamClient.ListUsersAsync().ConfigureAwait(false);


                foreach (var user in iamUsers.Users)
                {
                    DateTime? passwordLastUsed = null;
                    if (user.PasswordLastUsed != DateTime.MinValue)
                    {
                        passwordLastUsed = user.PasswordLastUsed;
                    }
                    users.Add(new CloudUser
                    {
                        Id = user.UserId,
                        User = user.UserName,
                        CreateDate = user.CreateDate,
                        PasswordLastUsed = passwordLastUsed
                    });
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("We can`t get users as we don`t have access");
                Console.WriteLine(e);
            }
            return users;
        }


        private async Task<List<Volume>> GetAllVolumesAsync(Region region)
        {
            var client = new AmazonEC2Client(_awsCreds, RegionEndpoint.GetBySystemName(region.RegionName));

            List<Volume> volumes = new List<Volume>();
            string nextToken = null;

            while (true)
            {
                var request = new DescribeVolumesRequest
                {
                    NextToken = nextToken
                };


                var response = await client.DescribeVolumesAsync(request).ConfigureAwait(false);

                volumes.AddRange(response.Volumes);
                if (response.NextToken == null)
                {
                    break;
                }
                nextToken = response.NextToken;
            }
            return volumes;
        }

        private async Task<List<Image>> GetAllImagesAsync(Region region, List<string> imageIds)
        {
            var client = new AmazonEC2Client(_awsCreds, RegionEndpoint.GetBySystemName(region.RegionName));

            List<Image> images = new List<Image>();


            var request = new DescribeImagesRequest
            {
                ImageIds = imageIds
            };

            var response = await client.DescribeImagesAsync(request).ConfigureAwait(false);

            images.AddRange(response.Images);

            return images;
        }

        private List<IpV4Network> GetNetworks(List<InstanceNetworkInterface> serverAddresses)
        {
            var networks = new List<IpV4Network>();
            foreach (var address in serverAddresses)
            {
                networks.Add(
                    new IpV4Network
                    {
                        Name = address.PrivateDnsName,
                        IpAddress = string.Join(",", address.PrivateIpAddresses.Select(k => k.PrivateIpAddress))
                    });
            }

            return networks;
        }


        private List<VolumeDetail> GetVolumes(List<InstanceBlockDeviceMapping> serverVolumes, List<Volume> allVolumes)
        {
            var volumes = new List<VolumeDetail>();
            foreach (var volume in serverVolumes)
            {
                var volDetails = allVolumes.Single(v => v.VolumeId == volume.Ebs.VolumeId);
                var tags = new Dictionary<string, string>();
                foreach (var tag in volDetails.Tags)
                {
                    tags.Add(tag.Key, tag.Value);
                }
                volumes.Add(new VolumeDetail
                {
                    Id = volume.Ebs.VolumeId,
                    Label = volume.DeviceName,
                    Size = volDetails.Size,
                    Type = volDetails.VolumeType.ToString(),
                    Created = volDetails.CreateTime,
                    Iops = volDetails.Iops,
                    Tags = tags
                });
            }

            return volumes;
        }



    }
}

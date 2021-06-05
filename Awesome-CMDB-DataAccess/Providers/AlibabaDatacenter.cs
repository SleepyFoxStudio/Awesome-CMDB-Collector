using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Auth;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Core.Regions;
using Aliyun.Acs.Ecs.Model.V20140526;
using Aliyun.Acs.Ram.Model.V20150501;
using Awesome_CMDB_DataAccess_Models;
using Polly;
using Polly.Retry;

namespace Awesome_CMDB_DataAccess.Providers
{
    public class AlibabaDatacenter : IDatacenter
    {
        public readonly string _accessKeyId;
        private readonly string _secretKey;
        private readonly RetryPolicy _defaultRetryPolicy;

        private readonly Lazy<Task<List<NamedAcsClient>>> _lazyClientsForAllRegions;


        public AlibabaDatacenter(string accessKeyId, string secretKey)
        {
            _accessKeyId = accessKeyId;
            _secretKey = secretKey;
            _lazyClientsForAllRegions = new Lazy<Task<List<NamedAcsClient>>>(GetClientsForEveryRegionAsync);
            _defaultRetryPolicy = Policy
                .Handle<Exception>().Or<WebException>()
                .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(5));

        }

        private async Task<List<NamedAcsClient>> GetClientsForEveryRegionAsync()
        {
            var regionResponse = await Task.Run(GetRegionsAsync).ConfigureAwait(false);
            var clients = new List<NamedAcsClient>();
            foreach (var region in regionResponse.Regions)
            {
                clients.Add(new NamedAcsClient
                {
                    Region = region.RegionId,
                    AcsClient = new DefaultAcsClient(FixedAlicloudDefaultProfile.GetProfile(region.RegionId, _accessKeyId, _secretKey)),
                });
            }
            return clients;
        }


        private async Task<DescribeRegionsResponse> GetRegionsAsync()
        {
            IClientProfile profile = FixedAlicloudDefaultProfile.GetProfile("eu-west-1", _accessKeyId, _secretKey);
            DefaultAcsClient client = new DefaultAcsClient(profile);

            var request = new DescribeRegionsRequest();
            try
            {
                return await Task.Run(() => client.GetAcsResponse(request)).ConfigureAwait(false);
            }
            catch (ServerException e)
            {
                Console.WriteLine(e);
            }
            catch (ClientException e)
            {
                Console.WriteLine(e);
            }
            throw new Exception("Failed getting Regions list");
        }




        private async Task<string> GetAlibabaAccountNumberAsync()
        {
            IClientProfile profile = FixedAlicloudDefaultProfile.GetProfile("eu-west-1", _accessKeyId, _secretKey);
            var client = new DefaultAcsClient(profile);

            var request = new Aliyun.Acs.Sts.Model.V20150401.GetCallerIdentityRequest();
            try
            {

                var response = _defaultRetryPolicy.Execute(() => client.GetAcsResponse(request));
                return response.AccountId;
            }
            catch (ServerException e)
            {
                await Console.Out.WriteLineAsync(e.Message).ConfigureAwait(false);
            }
            catch (ClientException e)
            {
                await Console.Out.WriteLineAsync(e.Message).ConfigureAwait(false);
            }
            throw new Exception("Could not get account");
        }

        public async Task<List<ServerGroup>> GetServerGroupsAsync()
        {
            var serverGroups = new List<ServerGroup>();
            var accountId = await GetAlibabaAccountNumberAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"****  ACCOUNTID LOOKUP  {_accessKeyId.GetLastChars(3)} {accountId}").ConfigureAwait(false);


       

            var clientForEachRegion = await _lazyClientsForAllRegions.Value.ConfigureAwait(false);


            foreach (var regionClient in clientForEachRegion)
            {
                //var instances = client.AcsClient.
                var request = new DescribeInstancesRequest()
                {
                    TimeoutInMilliseconds = 30000
                };
                DescribeInstancesResponse response = _defaultRetryPolicy.Execute(() => regionClient.AcsClient.GetAcsResponse(request));
                if (response?.Instances.Count == 0)
                {
                    continue;
                }

                List<ServerDetails> serverList = new List<ServerDetails>();
                foreach (var server in response.Instances)
                {
                    serverList.Add(new ServerDetails
                    {
                        Name = server.InstanceName,
                        Id = server.InstanceId,
                        Tags = GetTags(server.Tags),
                        Updated = null,
                        Created = DateTime.ParseExact(server.CreationTime, "yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture, DateTimeStyles.None),
                        Flavour = server.InstanceType,
                        Cpu = (int)server.Cpu,
                        Ram = (double)server.Memory / 1024,
                        Status = server.Status,
                        Ipv4Networks = GetNetworks(server.NetworkInterfaces),
                        AvailabilityZone = server.ZoneId,
                        DatacenterType = "Alibaba",
                        AccountId = accountId
                    });
                    await Console.Out.WriteLineAsync($"{_accessKeyId.Substring(_accessKeyId.Length - 3)} {accountId} {server.InstanceId} - {server.InstanceName}").ConfigureAwait(false);
                }

                serverGroups.Add(new ServerGroup
                {
                    GroupId = regionClient.Region,
                    Region = regionClient.Region,
                    GroupName = $"{accountId} {regionClient.Region}",
                    Servers = serverList
                });
            }

            return serverGroups;
        }

        private async Task<List<CloudDatabase>> GetDatabasesAsync()
        {
            await Console.Out.WriteLineAsync("RDS lookup not supported for AliCloud").ConfigureAwait(false);
            return new List<CloudDatabase>();
        }

        private async Task<List<CloudUser>> GetUsersAsync()
        {
            var users = new List<CloudUser>();
            try
            {
                var client = new DefaultAcsClient(FixedAlicloudDefaultProfile.GetProfile("eu-west-1", _accessKeyId, _secretKey));
                var request = new ListUsersRequest
                {
                    MaxItems = 100
                };

                var response = _defaultRetryPolicy.Execute(() => client.GetAcsResponse(request));
                foreach (var user in response.Users)
                {
                    DateTime? createDate = null;
                    DateTime? updateDate = null;
                    if (!string.IsNullOrEmpty(user.CreateDate))
                    {
                        createDate = DateTime.Parse(user.CreateDate);
                    }
                    if (!string.IsNullOrEmpty(user.UpdateDate))
                    {
                        updateDate = DateTime.Parse(user.UpdateDate);
                    }
                    users.Add(new CloudUser
                    {
                        Id = user.UserId,
                        User = user.UserName,
                        Email = user.Email,
                        CreateDate = createDate,
                        UpdateDate = updateDate
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

        private List<IpV4Network> GetNetworks(List<DescribeInstancesResponse.DescribeInstances_Instance.DescribeInstances_NetworkInterface> serverNetworkInterfaces)
        {
            var nics = new List<IpV4Network>();
            foreach (var interfaceItem in serverNetworkInterfaces)
            {
                nics.Add(new IpV4Network
                {
                    IpAddress = interfaceItem.PrimaryIpAddress,
                    Name = interfaceItem.NetworkInterfaceId
                });
            }

            return nics;
        }

        private Dictionary<string, string> GetTags(List<DescribeInstancesResponse.DescribeInstances_Instance.DescribeInstances_Tag> aliTags)
        {
            var tags = new Dictionary<string, string>();

            foreach (var aliTag in aliTags)
            {
                tags.Add(aliTag.TagKey, aliTag.TagValue);
            }

            return tags;
        }
    }

    class NamedAcsClient
    {
        public DefaultAcsClient AcsClient { get; set; }
        public string Region { get; set; }
    }

    public class ThreadsafeClientProfile : IClientProfile
    {
        private readonly string _regionId;
        private readonly Credential _credential;

        public ThreadsafeClientProfile(string regionId, Credential credential)
        {
            _regionId = regionId;
            _credential = credential;
        }

        [Obsolete]
        public ISigner GetSigner() => null;

        public string GetRegionId() => _regionId;

        public FormatType GetFormat() => DefaultProfile.GetProfile(_regionId).acceptFormat;

        public Credential GetCredential() => _credential;

        public List<Endpoint> GetEndpoints(string product, string regionId, string serviceCode, string endpointType)
        {
            return DefaultProfile.GetProfile(_regionId).GetEndpoints(product, regionId, serviceCode, endpointType);
        }

        public void SetLocationConfig(string regionId, string product, string endpoint)
        {
            DefaultProfile.GetProfile(_regionId).SetLocationConfig(regionId, product, endpoint);
        }

        public void SetCredentialsProvider(AlibabaCloudCredentialsProvider credentialsProvider)
        { }

        public void AddEndpoint(string endpointName, string regionId, string product, string domain, bool isNeverExpire = false)
        {
            DefaultProfile.GetProfile(_regionId).AddEndpoint(endpointName, regionId, product, domain, isNeverExpire);
        }

        public string DefaultClientName { get; set; }
    }

    /// <summary>
    /// Holy forking shirtballs Alicloud's DefaultProfile is forked! Do not use it directly, ever, in multithreaded code. I think someone sneezed static all over it.
    /// </summary>
    public static class FixedAlicloudDefaultProfile
    {
        public static IClientProfile GetProfile(string regionId, string accessKeyId, string secret)
        {
            return new ThreadsafeClientProfile(regionId, new Credential(accessKeyId, secret));
        }
    }

    public static class StringExtensions
    {
        public static string GetLastChars(this string s, int length)
        {
            return s?.Substring(s.Length - length);
        }
    }
}

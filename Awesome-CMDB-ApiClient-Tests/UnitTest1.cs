using System;
using System.Net.Http;
using System.Threading.Tasks;
using Awesome_CMDB_DataAccess;
using Xunit;

using FluentAssertions;
using static FluentAssertions.FluentActions;


namespace Awesome_CMDB_ApiClient_Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task TestUnauthorizedResponseReturns401()
        {
            var client = new AwesomeClient("https://localhost:6001/", new HttpClient());

            await Awaiting(() => client.AccountsAllAsync())
                .Should()
                .ThrowAsync<ApiException>()
                .Where(e => e.StatusCode == 401);
        }
    }
}
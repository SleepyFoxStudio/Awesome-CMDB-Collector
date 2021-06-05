using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Awesome_CMDB_DataAccess_Models;

namespace Awesome_CMDB_DataAccess
{
    public interface IDatacenter
    {
        Task<List<ServerGroup>> GetServerGroupsAsync();
    }
}

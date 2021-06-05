using System;
using System.Collections.Generic;

namespace Awesome_CMDB_DataAccess_Models
{
    public class VolumeDetail
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public int Size { get; set; }
        public string Type { get; set; }
        public DateTime Created { get; set; }
        public int Iops { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }
}
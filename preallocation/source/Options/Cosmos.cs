using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Preallocation.Options
{
    public record Cosmos
    {
        public string? CosmosUri { get; set; }
        public string? CosmosKey { get; set; }
        public string? DatabaseName { get; set; }
        public string? WithPreallocation { get; set; }
        public string? WithoutPreallocation { get; set; }
    }
}

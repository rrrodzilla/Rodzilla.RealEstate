using System;
using System.Collections.Generic;
using System.Text;

namespace Rodzilla.RealEstate.Models
{
    public class PostMetaValue
    {
        public string PostId { get; set; }
        public MlsListing Listing { get; set; }
        public string MetaKey { get; set; }
        public string MetaValue { get; set; }
    }
}

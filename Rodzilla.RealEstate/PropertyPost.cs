using Rodzilla.RealEstate.Models;
using WordPressPCL.Models;

namespace Rodzilla.RealEstate
{
    internal class PropertyPost
    {
        private readonly MlsListing _listing;

        public PropertyPost(MlsListing listing)
        {
            _listing = listing;
            var post = new Post();            
        }

        public int? id { get; set; }

        public string content => new Content(_listing.Content).Rendered;
        public string title => new Title(_listing.Title).Rendered;
        public string slug => _listing.MlsId.ToString();
        public int[] status => new int[]{6};

        public int[] city => new int[]{ ParseEnumValue<Cities>(_listing.City) };

        public int[] type => new int[]{ ParseEnumValue<PropertyTypes>(_listing.PropertyType)};

        public double fave_property_price => _listing.SalePrice;
        public double property_price => _listing.SalePrice;
        public double price => _listing.SalePrice;

        public int[] state => new int[]{ 41 };

        private int ParseEnumValue<T>(string value)
        {
            return (int)System.Enum.Parse(typeof(T), value.Replace(" ", "").Replace("'", "").Replace("-","").Replace("/",""));
        }

    }
}

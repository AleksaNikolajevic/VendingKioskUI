using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VendingKioskUI
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(myJsonResponse);
    public class Category
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Product
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int price { get; set; }
        public string imageUrl { get; set; }
    }

    public class Article
    {
        public int id { get; set; }
        public string tagCode { get; set; }
        public DateTime addedAt { get; set; }
        public Product product { get; set; }
        public Subcategory subcategory { get; set; }
        public Category category { get; set; }
    }

    public class Subcategory
    {
        public int id { get; set; }
        public string name { get; set; }
    }


}

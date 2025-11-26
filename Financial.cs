using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VendingKioskUI
{
    public class Financial
    {
        public string transaction { get; set; }
        public Id id { get; set; }
        public Amounts amounts { get; set; }
        public Options options { get; set; }
    }
}

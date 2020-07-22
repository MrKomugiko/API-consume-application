using System;
using System.Collections.Generic;
using System.Text;

namespace ApiConsumer.Models
{
     public class SearchingHistory
    {
        public string WyszukiwanaFraza { get; set; } // query
        public int OdwiedzonaStrona { get; set; } // pageId
        public DateTime DataWyszukiwania { get; set; } // data
    }
}

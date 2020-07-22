using System.Collections.Generic;

namespace ApiConsumer
{
    public class Query
    {
        public SearchInfo searchinfo { get; set; }
        public List<Search> search { get; set; }
    }

   public class SearchInfo
    {
        public int totalhits { get; set; }
    }
}

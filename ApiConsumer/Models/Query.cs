using System.Collections.Generic;

namespace ApiConsumer
{
    class Query
    {
        public SearchInfo searchinfo { get; set; }
        public List<Search> search { get; set; }
    }

    class SearchInfo
    {
        public int totalhits { get; set; }
    }
}

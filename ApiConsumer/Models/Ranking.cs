using System.Collections.Generic;

namespace ApiConsumer
{
    public class Ranking
    {
        public int Id { get; set; }
        public int Position { get; set; }
        public string Title { get; set; }
        public int Visited { get; set; }
        public List<string> SearchedByQueryList { get; set; }
    }
}
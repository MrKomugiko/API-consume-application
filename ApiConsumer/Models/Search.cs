using System;

namespace ApiConsumer
{
    class Search
    {
        public int ns { get; set; }
        public string title { get; set; }
        public int pageid { get; set; }
        public int size { get; set; }
        public int wordcount { get; set; }
        public string snipped { get; set; }
        public DateTime timestamp { get; set; }
    }
}

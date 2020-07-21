namespace ApiConsumer
{
    class Respond
    {
        public Continue _continue { get; set; }
        public Query query { get; set; }
    }

    class Continue
    {
        public int sroffset { get; set; }
    }
}

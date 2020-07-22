namespace ApiConsumer
{
    public class Respond
    {
        public Continue _continue { get; set; }
        public Query query { get; set; }
    }

    public class Continue
    {
        public int sroffset { get; set; }
    }
}

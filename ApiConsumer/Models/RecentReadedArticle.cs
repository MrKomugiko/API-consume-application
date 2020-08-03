namespace ApiConsumer
{
    public class RecentReadedArticle
    {
        public string Title { get; set; }
        public string Article { get; set; }

        public RecentReadedArticle(string title, string article) {
            Title = title;
            Article = article;
        }
    }
}
namespace MiniTwitter.Models.Net.Twitter
{
    public class List
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ScreenName { get; set; }

        public static readonly List[] Empty = new List[0];
    }
}

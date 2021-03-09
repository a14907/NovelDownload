namespace NovelSpider
{
    public class NovelSpiderOption
    {
        public string[] Domain { get; set; }
        /// <summary>
        /// 毫秒
        /// </summary>
        public int RequestDelay { get; set; } = 1000;
        public string Charset { get; set; }
        public string MenuLocationXpath { get; set; }
        public string TitleXpath { get; set; }
        public string MenuXpath { get; set; }
        public string NextMenuPageXpath { get; set; }
        public string ContentTitleXpath { get; set; }
        public string ContentXpath { get; set; }
        public string NextContentPageXpath { get; set; }
        public string AllContentPageXpath { get; set; }
    }
}

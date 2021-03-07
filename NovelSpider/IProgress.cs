namespace NovelSpider
{
    public interface IProgress
    {
        void Report(int val, int max);
    }
}

using System;
using System.IO;

namespace NovelSpider
{
    public static class UrlExtension
    {
        public static string GetFullUrl(this string target, string baseurl)
        {
            if (string.IsNullOrEmpty(target))
            {
                return target;
            }
            if (target.StartsWith("http"))
            {
                return target;
            }

            if (Uri.TryCreate(new Uri(baseurl), target, out var newuri))
            {
                return newuri.ToString();
            }
            throw new Exception("获取地址失败");
        }
    }
}

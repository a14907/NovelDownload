using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using HtmlAgilityPack;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Net;

namespace NovelSpider
{
    public class DefaultNovelSpider
    {
        private readonly IProgress _process;
        private readonly NovelSpiderOption _opt;
        private readonly Encoding _defaultCharset;
        private readonly HttpClient _client;

        public DefaultNovelSpider(NovelSpiderOption opt, IProgress progress)
        {
            _process = progress;
            _opt = opt;
            _defaultCharset = Encoding.GetEncoding(_opt.Charset);
            _client = new HttpClient(
            //new HttpClientHandler()
            //{
            //    Proxy = new WebProxy("http://127.0.0.1:50501")
            //}
            );
            _client.DefaultRequestHeaders.AcceptEncoding.Clear();
            _client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        }

        public bool CanProcess(string url)
        {
            var uri = new Uri(url);
            return _opt.Domain.Any(m => m.Equals(uri.Host, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<(bool result, string msg, int retryCount)> Process(string url, string targetDirectory)
        {
            int retryCount = 0;
            var targetUrl = new Uri(url);
            if (!Directory.Exists(targetDirectory))
            {
                return (false, $"目标目录不存在", retryCount);
            }
            if (!CanProcess())
            {
                return (false, $"无法处理url:{url}", retryCount);
            }

            var targetHtml = await LoadHtmlDocument(url);

            var title = GetTitle();
            var targetFile = Path.Combine(targetDirectory, title + ".txt");

            using (var fs = new FileStream(targetFile, FileMode.Create))
            using (var sw = new StreamWriter(fs, Encoding.UTF8))
            {
                var menus = await GetMenuUrlsAsync();
                var max = menus.Count();
                int index = 0;
                foreach (var menuItem in menus)
                {
                    (var ctitle, var content) = await GetChapterContent(menuItem);
                    sw.WriteLine(ctitle);
                    sw.WriteLine(content);
                    _process.Report(++index, max);
                }
            }
            return (true, "ok", retryCount);


            bool CanProcess()
            {
                return _opt.Domain.Any(m => m.Equals(targetUrl.Host, StringComparison.OrdinalIgnoreCase));
            }

            string GetTitle()
            {
                return targetHtml.DocumentNode.SelectSingleNode(_opt.TitleXpath).InnerText;
            }

            string GetMenuLocation()
            {
                if (string.IsNullOrEmpty(_opt.MenuLocationXpath))
                {
                    return url;
                }
                return targetHtml.DocumentNode.SelectSingleNode(_opt.TitleXpath).GetAttributeValue("href", "").GetFullUrl(url);
            }

            async Task<IEnumerable<string>> GetMenuUrlsAsync()
            {
                var loc = GetMenuLocation();
                var menuhtml = targetHtml;
                if (loc != url)
                {
                    menuhtml = await LoadHtmlDocument(loc);
                }

                if (string.IsNullOrEmpty(_opt.NextMenuPageXpath))
                {
                    return menuhtml.DocumentNode.SelectNodes(_opt.MenuXpath).Select(m => m.GetAttributeValue("href", "").GetFullUrl(loc));
                }
                else
                {
                    var res = menuhtml.DocumentNode.SelectNodes(_opt.MenuXpath).Select(m => m.GetAttributeValue("href", "").GetFullUrl(loc)).ToList();
                    do
                    {
                        var oldloc = loc;
                        loc = menuhtml.DocumentNode.SelectSingleNode(_opt.NextMenuPageXpath).GetAttributeValue("href", "").GetFullUrl(loc);
                        if (string.IsNullOrEmpty(loc) || loc == oldloc)
                        {
                            break;
                        }
                        menuhtml = await LoadHtmlDocument(loc);
                        var nextres = menuhtml.DocumentNode.SelectNodes(_opt.MenuXpath).Select(m => m.GetAttributeValue("href", "").GetFullUrl(loc)).ToList();
                        res.AddRange(nextres);
                    } while (true);
                    return res;
                }
            }

            async Task<(string title, string content)> GetChapterContent(string contentUrl)
            {
                var chtml = await LoadHtmlDocument(contentUrl);
                var contentTitle = chtml.DocumentNode.SelectSingleNode(_opt.ContentTitleXpath).InnerText;
                if (string.IsNullOrEmpty(_opt.NextContentPageXpath) && string.IsNullOrEmpty(_opt.AllContentPageXpath))
                {
                    var content = chtml.DocumentNode.SelectSingleNode(_opt.ContentXpath).InnerText;
                    content = System.Net.WebUtility.HtmlDecode(content).Replace("\u00A0\u00A0\u00A0\u00A0", "\r\n");
                    return (contentTitle, content);
                }
                else if (!string.IsNullOrEmpty(_opt.NextContentPageXpath) && string.IsNullOrEmpty(_opt.AllContentPageXpath))
                {
                    var content = chtml.DocumentNode.SelectSingleNode(_opt.ContentXpath).InnerText;
                    do
                    {
                        var oldcontentUrl = contentUrl;
                        contentUrl = chtml.DocumentNode.SelectSingleNode(_opt.NextContentPageXpath).GetAttributeValue("href", "").GetFullUrl(contentUrl);
                        if (string.IsNullOrEmpty(contentUrl) || content == oldcontentUrl)
                        {
                            break;
                        }
                        chtml = await LoadHtmlDocument(contentUrl);

                        content = content + "\r\n" + chtml.DocumentNode.SelectSingleNode(_opt.ContentXpath).InnerText;
                    } while (true);
                    return (contentTitle, content);
                }
                else if (string.IsNullOrEmpty(_opt.NextContentPageXpath) && !string.IsNullOrEmpty(_opt.AllContentPageXpath))
                {
                    var content = chtml.DocumentNode.SelectSingleNode(_opt.ContentXpath).InnerText;

                    var allOtherContentUrls = chtml.DocumentNode.SelectNodes(_opt.AllContentPageXpath).Select(m => m.GetAttributeValue("href", "").GetFullUrl(contentUrl)).Where(m => m != contentUrl);
                    foreach (var item in allOtherContentUrls)
                    {
                        chtml = await LoadHtmlDocument(item);
                        content = content + chtml.DocumentNode.SelectSingleNode(_opt.ContentXpath).InnerText;
                    }

                    return (contentTitle, content);
                }
                throw new Exception("不支持");
            }

            async Task<string> GetPageHtmlAsync(string turl)
            {
                if (_opt.RequestDelay > 0)
                {
                    await Task.Delay(_opt.RequestDelay);
                }
                
                var response = await _client.GetAsync(turl);
                Encoding newcharset = null;
                if (!string.IsNullOrEmpty(response.Content.Headers.ContentType.CharSet))
                {
                    newcharset = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
                }

                while (response.StatusCode != HttpStatusCode.OK)
                {
                    retryCount++;
                    response = await _client.GetAsync(turl);
                }

                var buf = await response.Content.ReadAsByteArrayAsync();
                var encoding = response.Content.Headers.ContentEncoding;
                if (encoding.Any(m => m.Equals("gzip", StringComparison.OrdinalIgnoreCase)))
                {
                    using (MemoryStream ms = new MemoryStream(buf))
                    {
                        using (GZipStream decompressedStream = new GZipStream(ms, CompressionMode.Decompress))
                        {
                            using (StreamReader sr = new StreamReader(decompressedStream, newcharset ?? _defaultCharset))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
                else if (encoding == null || encoding.Count == 0)
                {
                    return (newcharset ?? _defaultCharset).GetString(buf);
                }
                throw new Exception("不支持的压缩编码");
            }

            async Task<HtmlDocument> LoadHtmlDocument(string turl)
            {
                var doc = new HtmlDocument();
                var html = await GetPageHtmlAsync(turl);
                doc.LoadHtml(html);
                return doc;
            }
        }
    }
}

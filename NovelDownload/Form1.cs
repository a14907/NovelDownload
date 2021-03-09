using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using NovelSpider;
using System.IO;
using System.Net.Http;
using System.Diagnostics;

namespace NovelDownload
{
    public partial class Form1 : Form, IProgress
    {
        private List<DefaultNovelSpider> _options;

        public Form1()
        {
            InitializeComponent();
            using (var doc = JsonDocument.Parse(File.ReadAllText("appsetting.json")))
            {
                foreach (var item in doc.RootElement.EnumerateObject())
                {
                    if (item.Name == "settings")
                    {
                        _options = JsonSerializer.Deserialize<NovelSpiderOption[]>(item.Value.ToString()).Select(m => new DefaultNovelSpider(m, this)).ToList();
                    }
                }
            }
        }

        public void Report(int val, int max)
        {
            pbDownload.Maximum = max;
            pbDownload.Value = val;
        }

        private async void btnDownload_ClickAsync(object sender, EventArgs e)
        {
            try
            {
                btnDownload.Enabled = false;
                var url = txtUrl.Text.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.Show("链接不能为空");
                    return;
                }

                var instance = _options.FirstOrDefault(m => m.CanProcess(url));
                if (instance == null)
                {
                    MessageBox.Show("该站点暂不支持");
                    return;
                }
                var res = await instance.Process(url, "./小说");
                Process.Start("explorer.exe", Path.Combine(Directory.GetCurrentDirectory(), "小说"));
                MessageBox.Show(res.msg + " retryCount:" + res.retryCount);
            }
            finally
            {
                btnDownload.Enabled = true;
            }
        }
    }
}

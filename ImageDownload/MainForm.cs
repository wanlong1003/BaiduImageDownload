using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Threading;

namespace ImageDownload
{
    public partial class MainForm : Form
    {
        int pagesize = 60;   //页大小,百度规定最大为60
        private Thread thread;
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            txtPath.Text = Application.StartupPath;
            cbImageType.SelectedIndex = 0;
            numCount.Maximum = int.MaxValue;
            numCount.Minimum = pagesize;
            numCount.Value = pagesize;
            txtLog.WordWrap = false;
        }

        private void btnSearchPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.SelectedPath = txtPath.Text;
            dialog.Description = "请选择下载路径";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtPath.Text = dialog.SelectedPath;
            }
        }

        private void btnDownLoad_Click(object sender, EventArgs e)
        {
            string path = txtPath.Text.Trim();
            int count = Convert.ToInt32(numCount.Value);
            int type = cbImageType.SelectedIndex;
            string keyword = txtKeyword.Text.Trim();

            lblMsg.Text = string.Empty;
            if (path == string.Empty)
            {
                lblMsg.Text = "温馨提示：请选择下载路径！";
                return;
            }
            if (keyword == string.Empty)
            {
                lblMsg.Text = "温馨提示：请输入关键字！";
                return;
            }

            if (thread != null && thread.IsAlive)
            {
                thread.Abort();
                WriteLog("您终止了图片下载！");
                SetStatus(false);
            }
            else
            {
                thread = new Thread(() =>
                {
                    DelegateSetStatus setStatus = new DelegateSetStatus(SetStatus);
                    this.Invoke(setStatus, true);
                    SearchImages(keyword, path, count, type);
                    this.Invoke(setStatus, false);
                });
                thread.IsBackground = true;
                thread.Start();
            }
        }

        #region 回调

        delegate void DelegateSetStatus(bool isRun);
        /// <summary>
        /// 设置状态
        /// </summary>
        /// <param name="isRun">是否在下载</param>
        private void SetStatus(bool isRun)
        {
            btnSearchPath.Enabled = !isRun;
            numCount.Enabled = !isRun;
            cbImageType.Enabled = !isRun;
            btnDownLoad.Text = isRun ? "停  止" : "开始下载";
            txtKeyword.Enabled = !isRun;
            lblMsg.Text = isRun ? "下载中..." : "下载结束";
        }

        delegate void DelegateWriteLog(string msg);
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="msg"></param>
        private void WriteLog(string msg)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new DelegateWriteLog(WriteLog), msg);
            }
            else
            {
                txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "]" + msg + "\r\n");
                txtLog.ScrollToCaret();
            }
        }

        #endregion

        /// <summary>
        /// 图片下载
        /// </summary>
        /// <param name="keyword">关键字</param>
        /// <param name="path">保存路径</param>
        /// <param name="count">总数</param>
        /// <param name="type">类型 0大图  1中图  2小图</param>
        private void SearchImages(string keyword, string path, int count, int type)
        {
            //将输入的关键字转义，负责服务器可能会不识别
            keyword = Uri.EscapeDataString(keyword);

            int totalnum = ((count - 1) / pagesize) + 1;   //总页数

            //创建客户端
            WebClient client = new WebClient();
            //欺骗服务器的防盗链
            client.Headers["Referer"] = "http://image.baidu.com/";
            //欺骗服务器对浏览器的判断
            client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko";

            WriteLog("开始下载...");
            WriteLog("提示：本程序采用分批下载的方式，每批" + pagesize + "个！");
            //循环下载五页
            for (int i = 0; i < totalnum; i++)
            {
                int pagenum = i * pagesize;
                WriteLog("开始读取第【" + (i + 1) + "/" + totalnum + "】页数据");
                try
                {
                    //搜索并返回json
                    Stream stream = client.OpenRead("http://image.baidu.com/search/avatarjson?tn=resultjsonavatarnew&word=" + keyword + "&ie=utf-8&pn=" + pagenum + "&rn=" + pagesize + "");
                    StreamReader reader = new StreamReader(stream);
                    //分析json
                    JsonData data = JsonConvert.DeserializeObject<JsonData>(reader.ReadToEnd());
                    if (data != null && data.imgs != null)
                    {
                        WriteLog("获取到【" + data.imgs.Count + "】张图片");

                        int inx = 1;
                        foreach (ImageData img in data.imgs)
                        {
                            try
                            {
                                string url;
                                switch (type)
                                {
                                    case 0: url = img.objURL; break;
                                    case 1: url = img.middleURL; break;
                                    case 2: url = img.thumbURL; break;
                                    default: url = img.objURL; break;
                                }
                                WriteLog("【" + inx + "/" + data.imgs.Count + "】开始下载:" + url);
                                client.DownloadFile(url, Path.Combine(path, Path.GetFileName(url)));
                                WriteLog("【" + inx + "/" + data.imgs.Count + "】下载成功:" + url);
                            }
                            catch (Exception ex)
                            {
                                WriteLog("【" + inx + "/" + data.imgs.Count + "】下载出错:" + ex.Message);
                            }

                            inx++;
                        }
                    }
                    else
                    {
                        WriteLog("没有获取到图片");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("读取发生错误:" + ex.Message);
                }
            }
            WriteLog("下载结束！");
        }
    }
}

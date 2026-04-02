using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WindowsFormsApp1
{
    public partial class BrowserForm : Form
    {
        SimpleHTTPServer server;
        PaypalService paypalService;
        string url;
        public bool isPaymentComplete = false;
        public BrowserForm(SimpleHTTPServer httpServer,PaypalService paypal, string approvalURL)
        {
            InitializeComponent();
            server = httpServer;
            paypalService = paypal;
            url = approvalURL;

            if (server != null)
            {
                server.PayerIdReceived += Server_PayerIdReceived;
            }
            
        }

        private bool Server_PayerIdReceived(string payerId)
        {            
            if (paypalService.ExecutePayment(payerId))
            {
                isPaymentComplete = true;                
            }
            else
            {
                isPaymentComplete = false;
            }

            return isPaymentComplete;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            webView21.Dispose();
            this.Close();
        }

        private void BrowserForm_Load(object sender, EventArgs e)
        {
            InitBrowser();
        }

        private async Task Initialized()
        {
            await webView21.EnsureCoreWebView2Async();
        }

        public async void InitBrowser()
        {
            await Initialized();
            webView21.CoreWebView2.Navigate(url);
        }
    }
}

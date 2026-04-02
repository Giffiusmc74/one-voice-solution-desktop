using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.src
{
    public class SimpleHTTPServer
    {
        public delegate bool PayerIdReceivedHandler(string payerId);
        public event PayerIdReceivedHandler PayerIdReceived;

        private HttpListener _listener = new HttpListener();
        private Thread _listenerThread;
        PaypalService paypal = null;

        public SimpleHTTPServer(string[] prefixes, PaypalService paypal)
        {
            _listener.Prefixes.Clear();
            foreach (string prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }

            this.paypal = paypal;
        }

        public void Start()
        {
            _listener.Start();
            _listenerThread = new Thread(HandleRequests);
            _listenerThread.Start();
        }

        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException ex)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);                    
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string urlPath = request.Url.ToString().ToLower();
            bool isReturnUrl = urlPath.Contains("/return");
            bool isCancelUrl = urlPath.Contains("/cancel");

            // Extract Payment ID and Payer ID from the query string
            string paymentId = request.QueryString["paymentId"];
            string payerId = request.QueryString["PayerID"];

            string responseString = "";

            if (isReturnUrl && !string.IsNullOrEmpty(payerId))
            {
                if (!string.IsNullOrEmpty(payerId) && paypal != null && paypal.createdPayment != null)
                {
                    bool? paymentResult = PayerIdReceived?.Invoke(payerId); // calling execute payment

                    if (paymentResult.HasValue && paymentResult.Value)
                    {
                        responseString = "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <title>Payment Success - OneApp</title>\r\n    <style>\r\n        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background-color: #e0f7fa; }\r\n        h1 { color: #388e3c; }\r\n        p { color: #666; }\r\n        .info { margin-top: 20px; color: #333; }\r\n        .bold { font-weight: bold; }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1 class=\"bold\">OneApp - Payment Successful</h1>\r\n    <p>Thank you for purchasing a 30-day license for OneApp.</p>\r\n    <div class=\"info\">\r\n        <p>Your Payment ID: [paymentId]</p>\r\n        <p>Your Payer ID: [payerId]</p>\r\n        <p>Please press the back button in your application to proceed.</p>\r\n    </div>\r\n</body>\r\n</html>";
                        responseString = responseString.Replace("[paymentId]", paymentId).Replace("[payerId]", payerId);
                    }
                    else
                    {
                        responseString = "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <title>Payment Failed - OneApp</title>\r\n    <style>\r\n        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background-color: #ffcdd2; }\r\n        h1 { color: #d32f2f; }\r\n        p { color: #666; }\r\n        .bold { font-weight: bold; }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1 class=\"bold\">OneApp - Payment Failed</h1>\r\n    <p>There was a problem processing your payment for the OneApp 30-day license.</p> \r\n <p>Your Payment ID: [paymentId]</p>\r\n    <div class=\"info\">\r\n        <p>Please press the back button in your application and try again.</p>\r\n    </div>\r\n</body>\r\n</html>";
                        responseString = responseString.Replace("[paymentId]", paymentId);
                    }
                }
            }
            else if (isCancelUrl)
            {
                // Handle cancel URL logic
                responseString = "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <title>Payment Canceled - OneApp</title>\r\n    <style>\r\n        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background-color: #ffe0b2; }\r\n        h1 { color: #f57c00; }\r\n        p { color: #666; }\r\n        .bold { font-weight: bold; }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1 class=\"bold\">OneApp - Payment Canceled</h1>\r\n    <p>You have canceled the payment process for a 30-day license for OneApp.</p>\r\n    <div class=\"info\">\r\n        <p>If this was a mistake or you wish to try again, please press the back button in your application and proceed with the payment.</p>\r\n    </div>\r\n</body>\r\n</html>";
            }
            else
            {
                // Handle unknown request
                responseString = "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <title>Unknown Request - OneApp</title>\r\n    <style>\r\n        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background-color: #fff3e0; }\r\n        h1 { color: #f57c00; }\r\n        p { color: #666; }\r\n        .bold { font-weight: bold; }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1 class=\"bold\">OneApp - Unknown Request</h1>\r\n    <p>We're sorry, but we couldn't process your request for the OneApp license.</p>\r\n    <div class=\"info\">\r\n        <p>Please press the back button in your application and try again.</p>\r\n    </div>\r\n</body>\r\n</html>";
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            responseOutput.Write(buffer, 0, buffer.Length);
            responseOutput.Close();
        }

        public void Stop()
        {
            if (_listener != null)
            {                
                _listener.Stop();
                _listener.Close();                
            }

            if (_listenerThread.IsAlive)
                _listenerThread.Join();
        }
    }
}

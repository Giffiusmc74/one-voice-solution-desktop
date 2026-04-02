/**
 * PaypalService.cs — STUB (PayPal archived, replaced by Stripe)
 *
 * BrowserForm.cs and SimpleHTTPServer.cs reference this class from the old
 * PayPal in-app payment flow. That flow is no longer used — payments now go
 * through the web portal (Stripe). This stub keeps the project compiling
 * without changing any other files.
 */
using System;

namespace WindowsFormsApp1.src
{
    /// <summary>
    /// Legacy PayPal payment stub. Not called in v5 — payments handled via portal.
    /// </summary>
    public class PaypalService
    {
        /// <summary>Holds the last created PayPal payment object (legacy, unused).</summary>
        public object createdPayment { get; set; } = null;

        /// <summary>
        /// Execute a PayPal payment by payer ID. Always returns false in stub.
        /// </summary>
        public bool ExecutePayment(string payerId)
        {
            // PayPal flow is archived. Payments are handled via the web portal.
            return false;
        }
    }
}

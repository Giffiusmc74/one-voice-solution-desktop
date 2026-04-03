using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using NLog;
using PayPal.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using TimeZoneConverter;
using WindowsFormsApp1.Properties;
using WindowsFormsApp1.src;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowsFormsApp1
{
    public partial class LicenseForm : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        TimeApiConsumer timeApiConsumer = new TimeApiConsumer();

        MainForm mainForm;
        
        private System.Threading.Timer licenseCheckTimer;

        public bool isLicenseVerified = false;

        DateTime APITime = DateTime.MinValue;
        
        string licenseKey;
               
        public LicenseForm()
        {
            InitializeComponent();
        }

        private void LicenseForm_Load(object sender, EventArgs e)
        {
            try
            {
                logger.Info("License form started. Intializing Objects.");
                //DeleteRegistryValuesTesting();
                // Initialize the timer to call time API                                
                licenseCheckTimer = new System.Threading.Timer(timerCallback, null, 3600000, 3600000); // every hour (no immediate fire - startup validation handled below)

                txtLicenseKey.Enabled = false;

                btnValidate.Enabled = false;


                richTextBoxLicense.Text = "About This Site\r\nThis Website (hereinafter “the Site” or “this Site”) is part of One United Global Group LLC dba (One United Global).  One United Global is an online service provider, specializing in providing insurance quotes and connecting insurance shoppers with professionals and our marketing partners who can assist them.  We also specialize in our call center software:  ONE.\r\n\r\nOne United Global does not endorse any particular insurance plan, provider, or agent. Any information provided about any particular insurance plan, provider, or agent shall not be construed as an endorsement by One United Global.   One United Global is not operated, affiliated, nor endorsed by any government agency.\r\n\r\nPersonal and Noncommercial Use Limitation\r\nIn order to use the Services, you must be at least 18 years of age. In consideration of your use of the Services, you agree to provide true, accurate and current information about yourself as prompted by the required registration forms. This Site is for your personal, noncommercial use. You may not modify, copy, distribute, transmit, display, perform, reproduce, publish, license, create derivative works from, transfer, or sell any information, software, products or service found on or obtained from the Site; provided that you may download, reproduce, and retransmit One United Global Information solely for non-commercial purposes within your organization. With the exception of the foregoing limited authorization, no license or right in any copyright of One United Global or any other party is granted or conferred to you.\r\n\r\nThe Site is provided on an “as is” basis without warranties of any kind, either express or implied, including but not limited to warranties of title or implied warranties of merchantability, fitness for a particular purpose, or non-infringement, other than those warranties which are imposed by and incapable of exclusion, restriction or modification under the laws applicable to this agreement. YOUR USE OF THIS SITE IS AT YOUR OWN RISK. IN NO EVENT SHALL One United Global, ITS AGENTS, REPRESENTATIVES OR LICENSORS be liable for any LOSS OR INJURY OR ANY damages, either direct, indirect, punitive, special, incidental, consequential or otherwise, resulting from, or in any way connected to, the use of this Site OR ANY One United Global INFORMATION, in each case regardless of whether such damages are based on contract, tort, strict liability, or those other theories of liability. Some jurisdictions do not allow the exclusion of implied warranties or consequential or incidental damages, so portions of the above-referenced exclusions may not directly apply to you. YOU HEREBY WAIVE ANY AND ALL CLAIMS AGAINST One United Global, ITS AGENTS, REPRESENTATIVES AND LICENSORS ARISING OUT OF YOUR USE OF THE SITE OR ANY OTHER One United Global INFORMATION.  Or Software:  ONE.\r\n\r\nCopyright and Proprietary Rights Information\r\nThe Site may contain technical inaccuracies or typographical errors or omissions. One United Global reserves the right to make changes, corrections and/or improvements to the Site, and to the products and programs described in such information, at any time without notice.\r\n\r\nThis Site contains and references trademarks, patents, trade secrets, technologies, products, processes or other proprietary rights of One United Global and ONE.  No license or right to or in any such trademarks, patents, trade secrets, technologies, products, processes and other proprietary rights of One United Global and/or other parties is granted to or conferred upon you.\r\n\r\nAvailable Services\r\nThe purpose of the Site is to provide consumers with ability to receive our software program: ONE.   ONE guarantees no profits or any results by its usage.\r\n\r\nWe reserve all the rights to our Site contents, and may at our discretion change the contents of the Site, or restrict access to certain sections of the Site, or to discontinue any aspect of the Site, including, but not limited to content, features, hours of availability, without notice or penalty.\r\n\r\nYour Use of The Service\r\nYou must be at least 18 years of age to use the Service. You warrant and agree that, while using the Service, you will not do any of the following:\r\n\r\nImpersonate any other person or entity\r\nSubmit any information pertaining to a third party without that party’s understanding and express consent.\r\nSubmit false or misleading information.\r\nExploit this Service for any commercial reason\r\nengage in any activity prohibited under the CAN-SPAM Act\r\nEngage in any criminal or otherwise unlawful behavior\r\nCommit any action which could give rise to civil liability\r\nRestrict others from making use of this Service\r\nUpload or attempt to upload any computer virus, malicious computer code, file program, or any other digital property\r\nCommit any action that interferes with, blocks, or otherwise harms the Service\r\nLinks to the Web Site\r\nYou may not establish a hypertext “link” to the Web Site and/or distribute, modify or re-use the text or graphics of the Site unless you obtain prior express written permission from One United Global.\r\n\r\nLinks to Third Party Sites\r\nThis Site may contain links to sites that are controlled by third parties. These linked sites are not under the control of One United Global and One United Global is not responsible for the contents of any linked site or any link contained in a linked site. One United Global is providing those links to you only as a convenience, and the inclusion of any link does not imply endorsement by One United Global of any linked site or constitute any warranty by One United Global of any linked site or any items contained on such site.\r\n\r\nGoverning Law\r\nAny disputes arising out of or related to the Site shall be governed by and construed and enforced in accordance with, the laws of the State of Ohio applicable to contracts entered into and to be performed entirely within the State of Ohio.     Any claim must first attempted to be settled through arbitration agreed upon by both parties with an arbiter within Crawford County, Ohio.    All claims must be filed through Crawford County Courts as well.\r\n\r\nNo Unlawful or Prohibited Use. As a condition of your use of the Site, you warrant to One United Global that you will not use the Site for a purpose that is unlawful or prohibited by these terms, conditions, and notices.\r\n\r\nDisclaimer\r\nThe Service is provided on an “as is” and “as available” basis. To the fullest extent permitted by law, we make no representations of warranties of any kind, express or implied, regarding the use of the Service. We disclaim all warranties regarding the accuracy or reliability of the information provided, including the implied warranties of merchantability and fitness for a particular purpose, and non-infringement. WE DO NOT WARRANT THAT THE SERVICE WILL BE SECURE, UNINTERRUPTED, OR ERROR-FREE, WE MAKE WARRANTY THAT THE SERVICE WILL MEET YOUR REQUIREMENTS. NO ADVICE, RESULTS, OR INFORMATION, WHETHER ORAL OR WRITTEN, OBTAINED BY YOU FROM US OR THROUGH THE SERVICE WILL CREATE ANY WARRANTY NOT EXPRESSLY MADE HEREIN.\r\n\r\nWE MAKE NO WARRANTIES REGARDING THE AFFILIATION OF ANY PROVIDER WHO MAY CONTACT YOU. WE EXPRESSLY DISCLAIM ALL RESPONSIBILITY FOR THE ACTIONS OF ANY PROVIDER WHO MAY CONTACT YOU THROUGH THE SERVICE. IF YOU ARE DISSATISIFIED WITH THE SERVICE, YOUR SOLE REMEDY IS TO DISCONTINUE ITS USE.\r\nWE MAKE NO REPRESENTATION, EITHER EXPRESS OR IMPLIED, OF ANY SPONSORSHIP BY ANY COMPANY MENTIONED ON THIS SITE, OR OF ANY OTHER RELATIONSHIP WITH ANY SUCH COMPANY. WE MAKE NO GUARANTEE, EXPRESSED OR IMPLIED, THAT A USER WILL BE ABLE TO OBTAIN A QUOTE FROM ANY PARTICULAR COMPANY OR PROVIDER MENTIONED ON THIS SITE.\r\n\r\nLimitation of Liability\r\nWE SHALL NOT BE LIABLE FOR ANY DAMAGES WHATSOEVER, AND IN PARTICULAR, WE SHALL NOT BE LIABLE FOR ANY SPECIAL, INDIRECT, CONSEQUENTIAL, OR INCIDENTAL DAMAGES, OR ANY DAMAGES FOR LOST PROFITS, LOSS OF REVENUE, OR LOSS OF USE, ARISING OUT OF OR RELATED TO THE SERVICE OR THE INFORMATION CONTAINED IN IT, WHETHER SUCH DAMAGES ARISE IN CONTRACT, NEGLIGENCE, TORT, UNDER STATUTE, IN EQUITY, AT LAW, OR OTHERWISE, EVEN IF WE HAVE BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.\r\n\r\nIndemnification\r\nYou agree to indemnify and hold harmless the Service (and its parents, directors, officers, employees, subsidiaries, agents, and affiliates) from any and all claims, liabilities, costs, and expenses, including reasonable attorneys’ fees and costs, due to or arising in any way from the use or misuse by you of the Service, your violation of these Terms and Conditions, your violation of any law, or infringement by you of any right of any person or entity.\r\n\r\nProprietary Rights\r\nWe retain all rights, title, and interest in and to the Service, including all content, data, materials, specific implementations of XHTML, CSS, and other code, the look and feel, the design, and all other aspects of the trade dress of this Site, and retain all intellectual and property rights therein.\r\n\r\nThird party logotypes, brandmarks, and service marks are the property of their respective owners.\r\n\r\nUse of the Service by you does not grant to you ownership and any commercial use or exploitation of the Service by you is expressly prohibited. You may not exploit, copy, redistribute, or reproduce any aspect of the Service or software. Doing so may be a violation of applicable Federal and state laws and may subject you to liability.  We will prosecute to the fullest extent possible anyone that attempts to copy or redistribute any version of our software.  You agree that by purchasing our software that the intellectual knowledge and the design is uniquely made and it is the intellectual knowledge and ease of use that makes this software work and therein a substantial value.   \r\n\r\nOther\r\nIf any provision of these Terms and Conditions is found to be invalid or unenforceable, the other provisions shall remain in full force and effect. Further, you agree that for any provision that may be found to be invalid and unenforceable, you shall ask the court to endeavor to give effect to the intent of the provision. These Terms and Conditions constitute the full understanding between you and us. You agree that any claim arising from or related to the Service must be filed within twelve (12) months of your use of the Service, regardless of any statute or law to the contrary.\r\n\r\nContacting Us\r\nIf you have any questions about One United Global, this Site, our service, or this Privacy Policy, you may contact us at:\r\n\r\nOne United Global / ONE\r\nadmin@1unitedglobal.com\r\n\r\nChanges and Updates\r\nWe reserve the right to make changes and updates to these Terms and Conditions at any time, at our sole discretion. An updated copy of these Terms and Conditions will be kept clearly posted and accessible on this Site at all times.\r\n\r\nBy submitting a contact form or purchasing our software ONE, you are authorizing One United Global, and her affiliates to contact you per the rules and regulations associated with TCPA and DNC compliance\r\n";
                richTextBoxLicense.ReadOnly = true;

                Thread.Sleep(3000); // add delay for timer thread to validate license at startup

                // ── Pre-keyed installer support ───────────────────────────────
                // When a call center manager generates agent installers from the
                // portal, App.config is stamped with PreloadedLicenseKey.  On the
                // agent's first launch we silently save it to the registry so the
                // agent never sees the license entry screen.
                string preloaded = System.Configuration.ConfigurationManager
                    .AppSettings["PreloadedLicenseKey"];
                if (!string.IsNullOrWhiteSpace(preloaded))
                {
                    var existingKey = RegistryUtils.GetRegistryValue(@"SOFTWARE\OneApp3", "License");
                    if (existingKey == null)
                    {
                        logger.Info("[License] Pre-keyed installer: saving key to registry silently.");
                        RegistryUtils.SetRegistryValue(
                            @"Software\OneApp3", "License",
                            DataEncryption.EncryptString_Aes(preloaded.Trim()),
                            RegistryValueKind.String);
                    }
                }

                var value = RegistryUtils.GetRegistryValue(@"SOFTWARE\OneApp3", "License");

                if (value == null)
                {
                    // first time case — no saved key and no pre-keyed installer
                    lblStatus.Text = "Status: Need License to activate.";
                    UpdateUIForLoading(false); // Show standard UI
                }
                else
                {
                    licenseKey = DataEncryption.DecryptString_Aes(value.ToString());

                    // Show loading state
                    UpdateUIForLoading(true);
                    lblStatus.Text = "Status: Verifying saved license...";

                    // Validate silently — key is already saved, no entry screen needed
                    CheckLicenseStatus();
                    // Note: CheckLicenseStatus is async and will handle the UI/Form transition
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: " + ex.Message);
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
                UpdateUIForLoading(false); // Ensure UI is valid if error
            }
        }

        private void UpdateUIForLoading(bool isLoading)
        {
            // If checking (loading), hide everything except status
            // If not checking (false), show agreement/intro
            
            bool showContent = !isLoading;

            lblIntro.Visible = showContent;
            richTextBoxLicense.Visible = showContent;
            btnAccept.Visible = showContent;
            
            // Input fields are hidden by default until Accept is clicked anyway, 
            // but we ensure they are hidden during loading
            if (isLoading)
            {
                label2.Visible = false;
                txtLicenseKey.Visible = false;
                btnValidate.Visible = false;
            }
            // If not loading, we don't necessarily show them, as they depend on "Accept" click.
            // So we leave them alone or let btnAccept_Click handle them.
        }

        private void DeleteRegistryValuesTesting()
        {
            RegistryUtils.DeleteRegistryValue(@"Software\OneApp", "License");
            //RegistryUtils.DeleteRegistryValue(@"Software\OneApp", "StartDate");
            //RegistryUtils.DeleteRegistryValue(@"Software\OneApp", "EndDate");
            //RegistryUtils.DeleteRegistryValue(@"Software\OneApp", "Trial");
            //RegistryUtils.DeleteRegistryValue(@"Software\OneApp", "NewUser");
        }        

        private void timerCallback(object state)
        {            
            CheckLicenseStatus();
        }

        private async void CheckLicenseStatus()
        {
            try
            {
                var value = RegistryUtils.GetRegistryValue(@"SOFTWARE\OneApp3", "License");
                if (value != null)
                {
                    string lic = DataEncryption.DecryptString_Aes(value.ToString());
                    string deviceId = GetMacAddress();
                    string status = await timeApiConsumer.GetLicenseStatusAsync(lic, deviceId);

                    if (status == "LicenseValidated")
                    {
                        isLicenseVerified = true;
                        // Save license key to AppSettings so MainFormV5 heartbeat can use it
                        AppSettings.Instance.LicenseKey = lic;
                        AppSettings.Instance.Save();
                        
                        // Launch MainFormV5
                        this.Invoke(new Action(() =>
                        {
                if (System.Threading.Interlocked.CompareExchange(ref _isLaunching, 1, 0) != 0) return;
                            this.Hide();
                            var mainFormV5 = new MainFormV5();
                            mainFormV5.ShowDialog();
                            this.Close();
                        }));
                    }
                    else
                    {
                        // If validation returned an error/exception string but we have a saved key,
                        // launch the app anyway (offline-tolerant, same as HeartbeatService)
                        bool isTransientError = status.StartsWith("Error:") || status.StartsWith("Exception:");
                        if (isTransientError && value != null)
                        {
                            logger.Info($"[License] Transient validation error ({status}) but saved key exists — launching app.");
                            AppSettings.Instance.LicenseKey = lic;
                            AppSettings.Instance.Save();
                            this.Invoke(new Action(() =>
                            {
                if (System.Threading.Interlocked.CompareExchange(ref _isLaunching, 1, 0) != 0) return;
                                this.Hide();
                                var mainFormV5 = new MainFormV5();
                                mainFormV5.ShowDialog();
                                this.Close();
                            }));
                        }
                        else if (MainForm.IsFormOpen && mainForm != null && !mainForm.IsDisposed)
                        {
                            // Use Invoke to close the form on the UI thread
                            mainForm.Invoke(new Action(() =>
                            {
                                isLicenseVerified = false;
                                mainForm.Close();
                                this.Show();
                                UpdateUIForLoading(false); // Show UI
                                lblStatus.Text = "Status: Need License to activate.";
                                txtLicenseKey.Enabled = true;
                                btnValidate.Enabled = true;
                                
                                // Show Inputs since we failed validation
                                label2.Visible = true;
                                txtLicenseKey.Visible = true;
                                btnValidate.Visible = true;

                                // Show specific error if needed
                                if (status != "LicenseValidated")
                                {
                                     MessageBox.Show($"License validation failed: {status}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }));
                        }
                        else
                        {
                             // If main form wasn't open, just enable UI on this form
                             this.Invoke(new Action(() =>
                             {
                                 UpdateUIForLoading(false); // Show UI
                                 lblStatus.Text = "Status: Validation Failed (" + status + ")";
                                 txtLicenseKey.Enabled = true;
                                 btnValidate.Enabled = true;
                                 
                                 // Show inputs
                                 label2.Visible = true;
                                 txtLicenseKey.Visible = true;
                                 btnValidate.Visible = true;
                             }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
                // Network error with a saved key — launch the app anyway
                // (same offline-tolerant approach as HeartbeatService)
                var savedKey = RegistryUtils.GetRegistryValue(@"SOFTWARE\OneApp3", "License");
                if (savedKey != null)
                {
                    string lic = DataEncryption.DecryptString_Aes(savedKey.ToString());
                    AppSettings.Instance.LicenseKey = lic;
                    AppSettings.Instance.Save();
                    logger.Info("[License] Network error but saved key exists — launching app offline.");
                    this.Invoke(new Action(() =>
                    {
                if (System.Threading.Interlocked.CompareExchange(ref _isLaunching, 1, 0) != 0) return;
                        this.Hide();
                        var mainFormV5 = new MainFormV5();
                        mainFormV5.ShowDialog();
                        this.Close();
                    }));
                }
                else
                {
                    this.Invoke(new Action(() => UpdateUIForLoading(false)));
                }
            }
        }


        private bool CheckMachineID()
        {
            bool result = false;
            
            if (!string.IsNullOrEmpty(licenseKey))
            {
                string currentMachineMAC = DataEncryption.EncryptString_Aes(GetMacAddress());

                if (currentMachineMAC == DataEncryption.EncryptString_Aes(licenseKey))
                    result = true;
            }

            return result;  
        }
        
        public string GetMacAddress()
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Only consider Ethernet network interfaces, which are 'up' and not loopback
                if ((nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                    nic.OperationalStatus == OperationalStatus.Up &&
                    !nic.Description.ToLowerInvariant().Contains("virtual") &&
                    !nic.Description.ToLowerInvariant().Contains("pseudo"))
                {
                    return nic.GetPhysicalAddress().ToString();
                }
            }

            return ""; // MAC Address not found
        }

        private void LicenseForm_FormClosing(object sender, FormClosingEventArgs e)
        {            
            if (licenseCheckTimer != null)
                licenseCheckTimer.Dispose();

            if (MainForm.IsFormOpen && mainForm != null && !mainForm.IsDisposed)
            {
                mainForm.Close();
            }
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            txtLicenseKey.Enabled = true;
            btnValidate.Enabled = true;
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtLicenseKey.Text))
                {
                    // Master owner key - bypasses all license validation
                    if (txtLicenseKey.Text.Trim() == "ONE-OWNER-2026")
                    {
                        isLicenseVerified = true;
                if (System.Threading.Interlocked.CompareExchange(ref _isLaunching, 1, 0) != 0) return;
                        this.Hide();
                        var ownerForm = new MainFormV5("Owner");
                        ownerForm.ShowDialog();
                        return;
                    }

                    isLicenseVerified = false;
                    licenseKey = txtLicenseKey.Text;
                    logger.Info("Attempting to save license key to registry...");
                    RegistryUtils.SetRegistryValue(@"Software\OneApp3", "License", DataEncryption.EncryptString_Aes(licenseKey), RegistryValueKind.String);
                    logger.Info("License key saved to registry successfully.");

                    CheckLicenseStatusUI();


                }
                else
                {
                    MessageBox.Show("Plesae provide valid license Key and then validate.");
                }
            }

            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private async void CheckLicenseStatusUI()
        {            
            string deviceId = GetMacAddress();
            string status = await timeApiConsumer.GetLicenseStatusAsync(licenseKey, deviceId);

            if (status == "LicenseValidated")
            {
                isLicenseVerified = true;
                // Save license key to AppSettings so MainFormV5 heartbeat can use it
                AppSettings.Instance.LicenseKey = licenseKey;
                AppSettings.Instance.Save();
                if (System.Threading.Interlocked.CompareExchange(ref _isLaunching, 1, 0) != 0) return;
                this.Hide();
                var mainFormV5 = new MainFormV5();
                mainFormV5.ShowDialog();
                this.Close();
            }
            else
            {
                if (MainForm.IsFormOpen && mainForm != null && !mainForm.IsDisposed)
                {
                    // Use Invoke to close the form on the UI thread
                    mainForm.Invoke(new Action(() =>
                    {
                        isLicenseVerified = false;
                        mainForm.Close();
                        this.Show();

                        lblStatus.Text = "Status: Need License to activate.";
                       // button1.Enabled = true;
                    }));
                }
                else
                {
                    // Show exact error for debugging
                    MessageBox.Show($"Validation Failed. Server Response: {status}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnAccept_MouseEnter(object sender, EventArgs e)
        {
            btnAccept.Height += 3;
            btnAccept.Width += 3;
        }

        private void btnAccept_MouseLeave(object sender, EventArgs e)
        {
            btnAccept.Height -= 3;
            btnAccept.Width -= 3;
        }

        private void btnValidate_MouseEnter(object sender, EventArgs e)
        {
            btnValidate.Height += 3;
            btnValidate.Width += 3;
        }

        private void btnValidate_MouseLeave(object sender, EventArgs e)
        {
            btnValidate.Height -= 3;
            btnValidate.Width -= 3;
        }
    }
}

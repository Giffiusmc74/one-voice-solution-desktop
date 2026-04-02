using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public class TimeApiConsumer
    {
        private readonly HttpClient _httpClient;
        
        public TimeApiConsumer()
        {
            _httpClient = new HttpClient();            
        }

        public async Task<string> GetCurrentTimeAsync(string timeZone)
        {
            try
            {                
                string url = $"https://timeapi.io/api/Time/current/zone?timeZone={Uri.EscapeDataString(timeZone)}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var timeData = JsonConvert.DeserializeObject<TimeApiResponse>(json);
                    return timeData.DateTime; // Return the dateTime field from the API response
                }
                else
                {
                    // Handle non-success status code
                    return $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                return $"Exception: {ex.Message}";
            }
        }

        public async Task<string> GetLicenseStatusAsync(string licenseKey, string deviceId = null)
        {
            try
            {
                // ValidateLicenseURL should be the full path e.g. https://domain.com/api/license/validate
                string baseUrl = ConfigurationManager.AppSettings["ValidateLicenseURL"];
                
                // Remove trailing slash if present to avoid double slash issues if we were appending path, 
                // but here we assume baseUrl IS the endpoint.
                
                string url = $"{baseUrl}?licenseId={Uri.EscapeDataString(licenseKey)}";
                
                if (!string.IsNullOrEmpty(deviceId))
                {
                    url += $"&deviceId={Uri.EscapeDataString(deviceId)}";
                }
                
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return "LicenseValidated";
                }
                else
                {
                    // Handle non-success status code
                    return $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                return $"Exception: {ex.Message}";
            }
        }

        private class TimeApiResponse
        {
            // Define properties based on the JSON structure of the API response
            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
            public int Hour { get; set; }
            public int Minute { get; set; }
            public int Seconds { get; set; }
            public int MilliSeconds { get; set; }
            public string DateTime { get; set; }
            public string Date { get; set; }
            public string Time { get; set; }
            public string TimeZone { get; set; }
            public string DayOfWeek { get; set; }
            public bool DstActive { get; set; }
        }
    }
}

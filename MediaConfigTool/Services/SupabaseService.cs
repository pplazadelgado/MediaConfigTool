using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaConfigTool;
using MediaConfigTool.Models;

namespace MediaConfigTool.Services
{
    public class SupabaseService
    {
        private readonly HttpClient _httpClient;

        private const string BaseUrl = "https://neuopqphtwylcgiyczox.supabase.co";
        private const string ApiKey = "sb_publishable_KG_nyOf3csC-ViPolVrc1w_Jyk2VlI2";

        public SupabaseService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apiKey", ApiKey);
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                    }

        public async Task<List<Tenant>> GetTenantsAsync()
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/tenant?select=tenant_id,tenant_name";

                var result = await _httpClient.GetFromJsonAsync<List<Tenant>>(url);
                return result ?? new List<Tenant>();
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] {ex.Message}", ex);
                return new List<Tenant>();
            }

        }
    }
}

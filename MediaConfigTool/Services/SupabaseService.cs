using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using MediaConfigTool;
using MediaConfigTool.Models;
using System.Security.Cryptography.X509Certificates;

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

        public async Task<string> InsertMediaAssetAsync(MediaFile file, string tenantId)
        {
            try
            {
                var mimeType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                var rawMetadata = JsonSerializer.Serialize(new
                {
                    relative_path = file.RelativePath,
                    orientation = file.Orientation
                });

                var payload = new
                {
                    tenantId = tenantId,
                    media_type = "image",
                    canonical_mime_type = mimeType,
                    capture_timestamp = file.CaptureTimestamp?.ToString("o"),
                    raw_metadata = rawMetadata,
                    source_system = "local_import"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/media_asset";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Prefer", "return=representation");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);

                //Supabase returns an array even for single inserts
                var id = doc.RootElement[0].GetProperty("media_asset_id").GetString();
                return id;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] InsertMediaAssetAsync failed - {ex.Message}"); ;
            }
        }

        public async Task<bool> InsertMediaFileInstanceAsync(string mediaAssetId, MediaFile file)
        {
            try
            {
                var payload = new
                {
                    media_asset_id = mediaAssetId,
                    role = "source_master",
                    file_path = file.FullPath,
                    file_name = file.FileName
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/media_file_instance";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaFile InstanceAsync failed - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MediaAssetExistsAsync(string tenantId, string relativePath)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/media_asset" +
                  $"?tenant_id=eq.{tenantId}" +
                  $"&select=media_asset_id" +
                  $"&limit=1";

                // Supabase REST filtering on raw_metadata JSON requires
                // checking relative_path separately for the POC
                // For now we use (tenant_id + relative_path raw_metadata ->> filter)
                var fullUrl = $"{BaseUrl}/rest/v1/media_asset" +
                      $"?tenant_id=eq.{tenantId}" +
                      $"&raw_metadata->>relative_path=eq.{Uri.EscapeDataString(relativePath)}" +
                      $"&select=media_asset_id" +
                      $"&limit=1";

                var result = await _httpClient.GetFromJsonAsync<List<JsonElement>>(fullUrl);
                return result != null && result.Count > 0;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaAssetExistsAsync failed - {ex.Message}");
            }
        }
}

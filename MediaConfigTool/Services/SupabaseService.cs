using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using MediaConfigTool;
using MediaConfigTool.Models;
using System.Security.Cryptography.X509Certificates;
using System.IO;

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] {ex.Message}", ex);
                return new List<Tenant>();
            }

        }

        public async Task<string?> InsertMediaAssetAsync(MediaFile file, string tenantId)
        {
            try
            {
                string mimeType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                var now = DateTime.UtcNow.ToString("o");
                var rawMetadata = JsonSerializer.Serialize(new
                {
                    relative_path = file.RelativePath,
                    orientation = file.Orientation
                });

                var payload = new
                {
                    tenant_id = tenantId,
                    media_type = "photo",
                    canonical_mime_type = mimeType,
                    capture_timestamp = file.CaptureTimestamp?.ToString("o"),
                    import_timestamp = now,
                    is_deleted = false,
                    raw_metadata = rawMetadata,
                    source_system = "local_import",
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/media_asset";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Prefer", "return=representation");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaAsset HTTP {(int)response.StatusCode}: {errorBody}");
                    response.EnsureSuccessStatusCode();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaAsset response: {responseBody}");
                using var doc = JsonDocument.Parse(responseBody);

                string? id = null;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    id = doc.RootElement[0].GetProperty("media_asset_id").GetString();
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    id = doc.RootElement.GetProperty("media_asset_id").GetString();

                return id;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaAssetAsync failed – {ex.Message}");
                return null;
            }
        }

        public async Task<bool> InsertMediaFileInstanceAsync(
     string mediaAssetId,
     string storageSourceId,
     string tenantId,
     MediaFile file)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    tenant_id = tenantId,
                    media_asset_id = mediaAssetId,
                    storage_source_id = storageSourceId,
                    file_uri = file.FullPath,
                    file_name = file.FileName,
                    file_extension = Path.GetExtension(file.FileName).ToLowerInvariant(),
                    instance_role = "source_master",
                    is_primary = true,
                    status = "available",
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/media_file_instance");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaFileInstance HTTP {(int)response.StatusCode}: {errorBody}");
                    response.EnsureSuccessStatusCode();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] InsertMediaFileInstanceAsync failed – {ex.Message}");
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
                // For now we use (tenant_id + relative_path via raw_metadata ->> filter)
                var fullUrl = $"{BaseUrl}/rest/v1/media_asset" +
                              $"?tenant_id=eq.{tenantId}" +
                              $"&raw_metadata->>relative_path=eq.{Uri.EscapeDataString(relativePath)}" +
                              $"&select=media_asset_id" +
                              $"&limit=1";

                var result = await _httpClient.GetFromJsonAsync<List<JsonElement>>(fullUrl);
                return result != null && result.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaAssetExistsAsync failed – {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetOrCreateStorageSourceAsync(string tenantId, string rootPath)
        {
            try
            {
                // Try to find existing storage source for this tenant and root path
                var getUrl = $"{BaseUrl}/rest/v1/storage_source" +
                             $"?tenant_id=eq.{tenantId}" +
                             $"&base_uri=eq.{Uri.EscapeDataString(rootPath)}" +
                             $"&select=storage_source_id" +
                             $"&limit=1";

                var getResponse = await _httpClient.GetAsync(getUrl);
                if (!getResponse.IsSuccessStatusCode)
                {
                    var errorBody = await getResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetStorageSource HTTP {(int)getResponse.StatusCode}: {errorBody}");
                    return null;
                }

                var getBody = await getResponse.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetStorageSource response: {getBody}");
                var existing = JsonSerializer.Deserialize<List<JsonElement>>(getBody);


                if (existing != null && existing.Count > 0)
                {
                    var id = existing[0].GetProperty("storage_source_id").GetString();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] Reusing storage_source: {id}");
                    return id;
                }

                // Not found — create one
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    tenant_id = tenantId,
                    source_type = "local_disk",
                    name = $"Local Disk – {rootPath}",
                    base_uri = rootPath,
                    is_removable = false,
                    is_available = true,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/storage_source");
                request.Headers.Add("Prefer", "return=representation");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetOrCreateStorageSource HTTP {(int)response.StatusCode}: {errorBody}");
                    response.EnsureSuccessStatusCode();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] storage_source response: {responseBody}");
                using var doc = JsonDocument.Parse(responseBody);

                string? storageId = null;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    storageId = doc.RootElement[0].GetProperty("storage_source_id").GetString();
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    storageId = doc.RootElement.GetProperty("storage_source_id").GetString();

                return storageId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetOrCreateStorageSourceAsync failed – {ex.Message}");
                return null;
            }
        }


        public async Task<HashSet<string>> GetImportedRelativePathsAsync(string tenantId, IEnumerable<string> relativePaths)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var url = $"{BaseUrl}/rest/v1/media_asset" +
                          $"?tenant_id=eq.{tenantId}" +
                          $"&select=raw_metadata";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetImportedRelativePaths HTTP {(int)response.StatusCode}: {errorBody}");
                    return result;
                }

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("raw_metadata", out var meta))
                    {
                        // raw_metadata puede venir como string JSON o como objeto
                        JsonElement metaObj;

                        if (meta.ValueKind == JsonValueKind.String)
                        {
                            var metaStr = meta.GetString();
                            if (string.IsNullOrWhiteSpace(metaStr)) continue;
                            using var innerDoc = JsonDocument.Parse(metaStr);
                            metaObj = innerDoc.RootElement.Clone();
                        }
                        else if (meta.ValueKind == JsonValueKind.Object)
                        {
                            metaObj = meta;
                        }
                        else continue;

                        if (metaObj.TryGetProperty("relative_path", out var relPath))
                        {
                            var path = relPath.GetString();
                            if (!string.IsNullOrWhiteSpace(path))
                                result.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetImportedRelativePathsAsync failed – {ex.Message}");
            }

            return result;
        }
    }
}

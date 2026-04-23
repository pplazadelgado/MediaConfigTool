using MediaConfigTool;
using MediaConfigTool.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.DirectoryServices;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;

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
                _ = ex;
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

                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
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


        public async Task<Dictionary<string, string>> GetImportedRelativePathsAsync(
    string tenantId, IEnumerable<string> relativePaths)
        {
            // Returns: relative_path -> media_asset_id
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var url = $"{BaseUrl}/rest/v1/media_asset" +
                          $"?tenant_id=eq.{tenantId}" +
                          $"&select=media_asset_id,raw_metadata";

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
                    // Read media_asset_id
                    if (!element.TryGetProperty("media_asset_id", out var assetIdEl)) continue;
                    var assetId = assetIdEl.GetString();
                    if (string.IsNullOrWhiteSpace(assetId)) continue;

                    // Read raw_metadata (can be string or object)
                    if (!element.TryGetProperty("raw_metadata", out var meta)) continue;

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
                            result[path] = assetId;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetImportedRelativePathsAsync failed – {ex.Message}");
            }

            return result;
        }

        public async Task<List<Location>> GetLocationAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/location?select=location_id,location_name,location_type,tenant_id&tenant_id=eq.{tenantId}&order=location_name.asc";

                var result = await _httpClient.GetFromJsonAsync<List<Location>>(url);
                return result ?? new List<Location>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetLocationAsync: {ex.Message}");
                return new List<Location>();
            }
        }

        public async Task<bool> MediaLocationExistsAsync(string mediaAssetId, string locationId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/media_location" +
                  $"?media_asset_id=eq.{mediaAssetId}" +
                  $"&location_id=eq.{locationId}" +
                  $"&select=media_asset_id" +
                  $"&limit=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    && doc.RootElement.GetArrayLength() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaLocationExistsAsync {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InsertMediaLocationAsync(string mediaAssetId, string locationId, string tenantId)
        {
            var now = DateTime.UtcNow.ToString("o");
            var payload = new
            {
                media_asset_id = mediaAssetId,
                location_id = locationId,
                relationship_type = "captured_at",
                source_type = "user_assigned",
                tenant_id = tenantId,
                created_at = now
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/media_location");
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"HTTP {(int)response.StatusCode}: {errorBody}");
            }
            return true;
        }

        public async Task<List<Person>> GetPersonAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/person" +
                      $"?select=person_id,display_name,relationship_type,tenant_id" +
                      $"&tenant_id=eq.{tenantId}" +
                      $"&order=display_name.asc";

                var result = await _httpClient.GetFromJsonAsync<List<Person>>(url);

                return result ?? new List<Person>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetPersonsAsync: {ex.Message}");
                return new List<Person>();
            }
        }

        public async Task<List<Event>> GetEventsAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/event" +
                  $"?select=event_id,event_name,description,start_timestamp,end_timestamp,tenant_id" +
                  $"&tenant_id=eq.{tenantId}" +
                  $"&order=event_name.asc";

                var result = await _httpClient.GetFromJsonAsync<List<Event>>(url);
                return result ?? new List<Event>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetEventsAsync: {ex.Message}");
                return new List<Event>();
            }
        }

        public async Task<List<Tag>> GetTagsAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/tag" +
                  $"?select=tag_id,tag_name,tag_category_id,description,color_hex,tenant_id" +
                  $"&tenant_id=eq.{tenantId}" +
                  $"&order=tag_name.asc";

                var result = await _httpClient.GetFromJsonAsync<List<Tag>>(url);
                return result ?? new List<Tag>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetTagsAsync: {ex.Message}");
                return new List<Tag>();
            }
        }

        public async Task<List<TagCategory>> GetTagCategoriesAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/tag_category" +
                  $"?select=tag_category_id,name,category_type,tenant_id" +
                  $"&tenant_id=eq.{tenantId}" +
                  $"&order=name.asc";

                var result = await _httpClient.GetFromJsonAsync<List<TagCategory>>(url);
                return result ?? new List<TagCategory>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetTagCategoriesAsync: {ex.Message}");
                return new List<TagCategory>();
            }
        }

        public async Task<bool> CreateTagAsync(string name, string tagCategoryId, string? description, string? colorHex, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    tag_name = name,
                    tag_category_id = tagCategoryId,
                    description,
                    color_hex = colorHex,
                    tenant_id = tenantId,
                    is_system_defined = false,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/tag");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateTagAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateTagAsync(string tagId, string name, string tagCategoryId, string? description, string? colorHex)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    tag_name = name,
                    tag_category_id = tagCategoryId,
                    description,
                    color_hex = colorHex,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/tag?tag_id=eq.{tagId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] UpdateTagAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteTagAsync(string tagId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/tag?tag_id=eq.{tagId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] DeleteTagAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AssingPersonAsync(string mediaAssetId, string personId, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    media_asset_id = mediaAssetId,
                    person_id = personId,
                    source_type = "user_assigned",
                    tenant_id = tenantId,
                    created_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/media_person");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] AssignPersonAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AssingEventAsync(string mediaAssetId, string eventId, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    media_asset_id = mediaAssetId,
                    event_id = eventId,
                    relationship_type = "belongs_to",
                    tenant_id = tenantId,
                    created_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/media_event");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStreamAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] AssignEventAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AssingTagAsync(string mediaAssetId, string tagId, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    media_asset_id = mediaAssetId,
                    tag_id = tagId,
                    source_type = "user_assigned",
                    tenant_id = tenantId,
                    created_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/media_tag");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] AssignTagAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreatePersonAsync(string name, string? relationshipType, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    display_name = name,
                    relationship_type = relationshipType,
                    tenant_id = tenantId,
                    is_system_generated = false,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/person");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreatePersonAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdatePersonAsync(string personId, string name, string? relationshipType)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    display_name = name,
                    relationship_type = relationshipType,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/person?person_id=eq.{personId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] UpdatePersonAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeletePersonAsync(string personId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/person?person_id=eq.{personId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] DeletePersonAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateEventAsync(string name, string? description, DateTimeOffset? startDate, DateTimeOffset? endDate, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    event_name = name,
                    description,
                    start_timestamp = startDate?.ToString("o"),
                    end_timestamp = endDate?.ToString("o"),
                    tenant_id = tenantId,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/event");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateEventAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateEventAsync(string eventId, string name, string? description, DateTimeOffset? startDate, DateTimeOffset? endDate)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    event_name = name,
                    description,
                    start_timestamp = startDate?.ToString("o"),
                    end_timestamp = endDate?.ToString("o"),
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/event?event_id=eq.{eventId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] UpdateEventAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteEventAsync(string eventId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/event?event_id=eq.{eventId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] DeleteEventAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateLocationAsync(string name, string locationType, string tenantId, string? parentLocationId = null)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    location_name = name,
                    location_type = locationType,
                    parent_location_id = parentLocationId,
                    tenant_id = tenantId,
                    is_system_defined = false,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/location");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateLocationAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateLocationAsync(string locationId, string name, string locationType)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    location_name = name,
                    location_type = locationType,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/location?location_id=eq.{locationId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] UpdateLocationAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteLocationAsync(string locationId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/location?location_id=eq.{locationId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Prefer", "retur=minimal");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] DeleteLocationAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateVisualAssetAsync(
             string assetUri, string mimeType, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    asset_type = "map_snapshot",
                    asset_uri = assetUri,
                    mime_type = mimeType,
                    is_system_defined = false,
                    tenant_id = tenantId,
                    created_at = now,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/visual_asset");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateVisualAssetAsync HTTP {(int)response.StatusCode}: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateVisualAssetAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<VisualAsset>> GetMapVisualAssetsAsync(string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/visual_asset" +
                  $"?select=visual_asset_id,asset_uri,asset_type,mime_type,tenant_id" +
                  $"&tenant_id=eq.{tenantId}" +
                  $"&asset_type=eq.map_snapshot" +
                  $"&order=asset_uri.asc";

                var result = await _httpClient.GetFromJsonAsync<List<VisualAsset>>(url);
                return result ?? new List<VisualAsset>();
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetMapVisualAssetsAsync: {ex.Message}");
                return new List<VisualAsset>();
            }
        }

       
        

        public async Task<bool> AssignMapToLocationAsync(
            string locationId, string visualAssetId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    map_visual_asset_id = visualAssetId,
                    updated_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/rest/v1/location?location_id=eq.{locationId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] AssignMapToLocationAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> VisualAssetExistsAsync(string assetUri, string tenantId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/visual_asset" +
                          $"?asset_uri=eq.{Uri.EscapeDataString(assetUri)}" +
                          $"&tenant_id=eq.{tenantId}" +
                          $"&select=visual_asset_id" +
                          $"&limit=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    && doc.RootElement.GetArrayLength() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] VisualAssetExistsAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MediaPersonEsixtsAsync(string mediaAssetId, string personId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/media_person" +
                  $"?media_asset_id=eq.{mediaAssetId}" +
                  $"&person_id=eq.{personId}" +
                  $"&select=media_person_id" +
                  $"&limit=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    && doc.RootElement.GetArrayLength() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaPersonExistsAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MediaEventExistsAsync(string mediaAssetId, string eventId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/media_event" +
                      $"?media_asset_id=eq.{mediaAssetId}" +
                      $"&event_id=eq.{eventId}" +
                      $"&select=media_event_id" +
                      $"&limit=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    && doc.RootElement.GetArrayLength() > 0;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaEventExistsAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MediaTagExistsAsync(string mediaAssetId, string tagId)
        {
            try
            {
                var url = $"{BaseUrl}/rest/v1/media_tag" +
                  $"?media_asset_id=eq.{mediaAssetId}" +
                  $"&tag_id=eq.{tagId}" +
                  $"&select=media_tag_id" +
                  $"&limit=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    && doc.RootElement.GetArrayLength() > 0;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] MediaTagExistsAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateTagCategoryAsync(string name, string categoryType, string tenantId)
        {
            try
            {
                var now = DateTime.UtcNow.ToString("o");
                var payload = new
                {
                    name,
                    category_type = categoryType,
                    tenant_id = tenantId,
                    is_system_defined = false,
                    created_at = now
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/tag_category");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                }
                return true;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] CreateTagCategoryAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<MediaRenderData?> GetMediaRenderDataAsync(string mediaAssetId, string tenantId, string fullPath, DateTime? captureTimestamp)
        {
            try
            {
                var data = new MediaRenderData
                {
                    MediaAssetId = mediaAssetId,
                    FullPath = fullPath,
                    CaptureTimestamp = captureTimestamp
                };

                // Location + map
                var locationUrl = $"{BaseUrl}/rest/v1/media_location" +
                                  $"?media_asset_id=eq.{mediaAssetId}" +
                                  $"&tenant_id=eq.{tenantId}" +
                                  $"&select=location_id" +
                                  $"&limit=1";

                var locationResponse = await _httpClient.GetAsync(locationUrl);
                if (locationResponse.IsSuccessStatusCode)
                {
                    var locationBody = await locationResponse.Content.ReadAsStringAsync();
                    using var locationDoc = JsonDocument.Parse(locationBody);
                    if (locationDoc.RootElement.GetArrayLength() > 0)
                    {
                        var locationId = locationDoc.RootElement[0]
                            .GetProperty("location_id").GetString();

                        if (!string.IsNullOrEmpty(locationId))
                        {
                            var locUrl = $"{BaseUrl}/rest/v1/location" +
                                         $"?location_id=eq.{locationId}" +
                                         $"&select=location_name,map_visual_asset_id" +
                                         $"&limit=1";

                            var locResponse = await _httpClient.GetAsync(locUrl);
                            if (locResponse.IsSuccessStatusCode)
                            {
                                var locBody = await locResponse.Content.ReadAsStringAsync();
                                using var locDoc = JsonDocument.Parse(locBody);
                                if (locDoc.RootElement.GetArrayLength() > 0)
                                {
                                    var locEl = locDoc.RootElement[0];
                                    data.LocationName = locEl.TryGetProperty("location_name", out var ln)
                                        ? ln.GetString() : null;

                                    if (locEl.TryGetProperty("map_visual_asset_id", out var mapId)
                                        && mapId.ValueKind != JsonValueKind.Null)
                                    {
                                        var mapUrl = $"{BaseUrl}/rest/v1/visual_asset" +
                                                     $"?visual_asset_id=eq.{mapId.GetString()}" +
                                                     $"&select=asset_uri" +
                                                     $"&limit=1";

                                        var mapResponse = await _httpClient.GetAsync(mapUrl);
                                        if (mapResponse.IsSuccessStatusCode)
                                        {
                                            var mapBody = await mapResponse.Content.ReadAsStringAsync();
                                            using var mapDoc = JsonDocument.Parse(mapBody);
                                            if (mapDoc.RootElement.GetArrayLength() > 0)
                                                data.MapAssetUri = mapDoc.RootElement[0]
                                                    .GetProperty("asset_uri").GetString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Persons
                var personsUrl = $"{BaseUrl}/rest/v1/media_person" +
                                 $"?media_asset_id=eq.{mediaAssetId}" +
                                 $"&tenant_id=eq.{tenantId}" +
                                 $"&select=person_id";

                var personsResponse = await _httpClient.GetAsync(personsUrl);
                if (personsResponse.IsSuccessStatusCode)
                {
                    var personsBody = await personsResponse.Content.ReadAsStringAsync();
                    using var personsDoc = JsonDocument.Parse(personsBody);
                    foreach (var p in personsDoc.RootElement.EnumerateArray())
                    {
                        var personId = p.GetProperty("person_id").GetString();
                        if (string.IsNullOrEmpty(personId)) continue;

                        var personUrl = $"{BaseUrl}/rest/v1/person" +
                                        $"?person_id=eq.{personId}" +
                                        $"&select=display_name&limit=1";

                        var personResponse = await _httpClient.GetAsync(personUrl);
                        if (!personResponse.IsSuccessStatusCode) continue;

                        var personBody = await personResponse.Content.ReadAsStringAsync();
                        using var personDoc = JsonDocument.Parse(personBody);
                        if (personDoc.RootElement.GetArrayLength() > 0)
                        {
                            var name = personDoc.RootElement[0]
                                .GetProperty("display_name").GetString();
                            if (!string.IsNullOrEmpty(name))
                                data.PersonNames.Add(name);
                        }
                    }
                }

                // Events
                var eventsUrl = $"{BaseUrl}/rest/v1/media_event" +
                                $"?media_asset_id=eq.{mediaAssetId}" +
                                $"&tenant_id=eq.{tenantId}" +
                                $"&select=event_id";

                var eventsResponse = await _httpClient.GetAsync(eventsUrl);
                if (eventsResponse.IsSuccessStatusCode)
                {
                    var eventsBody = await eventsResponse.Content.ReadAsStringAsync();
                    using var eventsDoc = JsonDocument.Parse(eventsBody);
                    foreach (var e in eventsDoc.RootElement.EnumerateArray())
                    {
                        var eventId = e.GetProperty("event_id").GetString();
                        if (string.IsNullOrEmpty(eventId)) continue;

                        var eventUrl = $"{BaseUrl}/rest/v1/event" +
                                       $"?event_id=eq.{eventId}" +
                                       $"&select=event_name&limit=1";

                        var eventResponse = await _httpClient.GetAsync(eventUrl);
                        if (!eventResponse.IsSuccessStatusCode) continue;

                        var eventBody = await eventResponse.Content.ReadAsStringAsync();
                        using var eventDoc = JsonDocument.Parse(eventBody);
                        if (eventDoc.RootElement.GetArrayLength() > 0)
                        {
                            var name = eventDoc.RootElement[0]
                                .GetProperty("event_name").GetString();
                            if (!string.IsNullOrEmpty(name))
                                data.EventNames.Add(name);
                        }
                    }
                }

                // Tags
                var tagsUrl = $"{BaseUrl}/rest/v1/media_tag" +
                              $"?media_asset_id=eq.{mediaAssetId}" +
                              $"&tenant_id=eq.{tenantId}" +
                              $"&select=tag_id";

                var tagsResponse = await _httpClient.GetAsync(tagsUrl);
                if (tagsResponse.IsSuccessStatusCode)
                {
                    var tagsBody = await tagsResponse.Content.ReadAsStringAsync();
                    using var tagsDoc = JsonDocument.Parse(tagsBody);
                    foreach (var t in tagsDoc.RootElement.EnumerateArray())
                    {
                        var tagId = t.GetProperty("tag_id").GetString();
                        if (string.IsNullOrEmpty(tagId)) continue;

                        var tagUrl = $"{BaseUrl}/rest/v1/tag" +
                                     $"?tag_id=eq.{tagId}" +
                                     $"&select=tag_name&limit=1";

                        var tagResponse = await _httpClient.GetAsync(tagUrl);
                        if (!tagResponse.IsSuccessStatusCode) continue;

                        var tagBody = await tagResponse.Content.ReadAsStringAsync();
                        using var tagDoc = JsonDocument.Parse(tagBody);
                        if (tagDoc.RootElement.GetArrayLength() > 0)
                        {
                            var name = tagDoc.RootElement[0]
                                .GetProperty("tag_name").GetString();
                            if (!string.IsNullOrEmpty(name))
                                data.TagNames.Add(name);
                        }
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetMediaRenderDataAsync: {ex.Message}");
                return null;
            }
        }
    }
}

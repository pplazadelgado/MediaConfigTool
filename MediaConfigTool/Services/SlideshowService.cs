using MediaConfigTool.Models;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaConfigTool.Services
{
    public class SlideshowService
    {
        private readonly SupabaseService _supabaseService;

        public SlideshowService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<List<(string assetId, DateTime? captureTs)>> GetFilteredAssetIdsAsync(
    string tenantId,
    List<int> selectedYears,
    List<string> selectedEventIds,
    List<string> selectedPersonIds,
    List<string> selectedTagIds)
        {
            try
            {
                var url = $"{_supabaseService.BaseUrl}/rest/v1/media_asset" +
                          $"?tenant_id=eq.{tenantId}" +
                          $"&is_deleted=eq.false" +
                          $"&select=media_asset_id,capture_timestamp";

                var response = await _supabaseService.HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<(string assetId, DateTime? captureTs)>();

                var body = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);

                var allIds = new List<(string id, int? year, DateTime? captureTs)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = el.GetProperty("media_asset_id").GetString();
                    if (string.IsNullOrEmpty(id)) continue;

                    int? year = null;
                    DateTime? captureTs = null;

                    if (el.TryGetProperty("capture_timestamp", out var ts) &&
                        ts.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        var str = ts.GetString();
                        if (DateTimeOffset.TryParse(str, out var dt))
                        {
                            year = dt.Year;
                            captureTs = dt.DateTime;
                        }
                    }

                    allIds.Add((id, year, captureTs));
                }

                // Filter by year
                if (selectedYears.Count > 0)
                    allIds = allIds.Where(x => x.year.HasValue &&
                        selectedYears.Contains(x.year.Value)).ToList();

                var filtered = allIds.Select(x => (x.id, x.captureTs)).ToList();

                // Filter by event
                if (selectedEventIds.Count > 0)
                {
                    var byEvent = new HashSet<string>();
                    foreach (var eventId in selectedEventIds)
                    {
                        var evUrl = $"{_supabaseService.BaseUrl}/rest/v1/media_event" +
                                    $"?event_id=eq.{eventId}" +
                                    $"&tenant_id=eq.{tenantId}" +
                                    $"&select=media_asset_id";

                        var evResponse = await _supabaseService.HttpClient.GetAsync(evUrl);
                        if (!evResponse.IsSuccessStatusCode) continue;

                        var evBody = await evResponse.Content.ReadAsStringAsync();
                        using var evDoc = System.Text.Json.JsonDocument.Parse(evBody);
                        foreach (var el in evDoc.RootElement.EnumerateArray())
                        {
                            var id = el.GetProperty("media_asset_id").GetString();
                            if (!string.IsNullOrEmpty(id)) byEvent.Add(id);
                        }
                    }
                    filtered = filtered.Where(x => byEvent.Contains(x.id)).ToList();
                }

                // Filter by person
                if (selectedPersonIds.Count > 0)
                {
                    var byPerson = new HashSet<string>();
                    foreach (var personId in selectedPersonIds)
                    {
                        var pUrl = $"{_supabaseService.BaseUrl}/rest/v1/media_person" +
                                   $"?person_id=eq.{personId}" +
                                   $"&tenant_id=eq.{tenantId}" +
                                   $"&select=media_asset_id";

                        var pResponse = await _supabaseService.HttpClient.GetAsync(pUrl);
                        if (!pResponse.IsSuccessStatusCode) continue;

                        var pBody = await pResponse.Content.ReadAsStringAsync();
                        using var pDoc = System.Text.Json.JsonDocument.Parse(pBody);
                        foreach (var el in pDoc.RootElement.EnumerateArray())
                        {
                            var id = el.GetProperty("media_asset_id").GetString();
                            if (!string.IsNullOrEmpty(id)) byPerson.Add(id);
                        }
                    }
                    filtered = filtered.Where(x => byPerson.Contains(x.id)).ToList();
                }

                // Filter by tag
                if (selectedTagIds.Count > 0)
                {
                    var byTag = new HashSet<string>();
                    foreach (var tagId in selectedTagIds)
                    {
                        var tUrl = $"{_supabaseService.BaseUrl}/rest/v1/media_tag" +
                                   $"?tag_id=eq.{tagId}" +
                                   $"&tenant_id=eq.{tenantId}" +
                                   $"&select=media_asset_id";

                        var tResponse = await _supabaseService.HttpClient.GetAsync(tUrl);
                        if (!tResponse.IsSuccessStatusCode) continue;

                        var tBody = await tResponse.Content.ReadAsStringAsync();
                        using var tDoc = System.Text.Json.JsonDocument.Parse(tBody);
                        foreach (var el in tDoc.RootElement.EnumerateArray())
                        {
                            var id = el.GetProperty("media_asset_id").GetString();
                            if (!string.IsNullOrEmpty(id)) byTag.Add(id);
                        }
                    }
                    filtered = filtered.Where(x => byTag.Contains(x.id)).ToList();
                }

                return filtered;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SlideshowService] GetFilteredAssetIdsAsync: {ex.Message}");
                return new List<(string assetId, DateTime? captureTs)>();
            }
        }

        public async Task<string?> GetFileUriForAssetAsync(string mediaAssetId, string tenantId)
        {
            try
            {
                var url = $"{_supabaseService.BaseUrl}/rest/v1/media_file_instance" +
                          $"?media_asset_id=eq.{mediaAssetId}" +
                          $"&tenant_id=eq.{tenantId}" +
                          $"&instance_role=eq.source_master" +
                          $"&select=file_uri" +
                          $"&limit=1";

                var response = await _supabaseService.HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);

                if (doc.RootElement.GetArrayLength() == 0) return null;
                return doc.RootElement[0].GetProperty("file_uri").GetString();
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SlideshowService] GetFileUriForAssetAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<RenderTargetBitmap> RenderOffscreenAsync(
     MediaRenderData data, bool isPortrait)
        {
            RenderTargetBitmap bitmap = null!;

            // Must run on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (isPortrait)
                {
                    var window = new Views.PreviewPortraitWindow(data);
                    window.Show();
                    window.Hide();

                    var canvas = (System.Windows.Controls.Grid)window
                        .FindName("RenderCanvas");

                    canvas.Measure(new System.Windows.Size(1200, 1920));
                    canvas.Arrange(new System.Windows.Rect(0, 0, 1200, 1920));
                    canvas.UpdateLayout();

                    bitmap = new RenderTargetBitmap(
                        1200, 1920, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(canvas);
                    window.Close();
                }
                else
                {
                    var window = new Views.PreviewWindow(data);
                    window.Show();
                    window.Hide();

                    var canvas = (System.Windows.Controls.Grid)window
                        .FindName("RenderCanvas");

                    canvas.Measure(new System.Windows.Size(1920, 1200));
                    canvas.Arrange(new System.Windows.Rect(0, 0, 1920, 1200));
                    canvas.UpdateLayout();

                    bitmap = new RenderTargetBitmap(
                        1920, 1200, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(canvas);
                    window.Close();
                }
            });

            return bitmap;
        }

    }
}


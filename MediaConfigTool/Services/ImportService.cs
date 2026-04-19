using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaConfigTool.Models;
using System.Linq;

namespace MediaConfigTool.Services
{
    public class ImportService
    {
        private readonly SupabaseService _supabaseService;

        public ImportService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// Imports a list of media files into the database for the given tenant.
        /// Reports progress after each file via the progress callback.
        /// Supports cancellation via CancellationToken.
        /// </summary>
        public async Task<ImportResult> ImportAsync(
     IEnumerable<MediaFile> files,
     string tenantId,
     string rootPath,
     IProgress<string>? progress = null,
     CancellationToken cancellationToken = default)
        {
            var result = new ImportResult();
            var fileList = files.ToList();

            progress?.Report("Preparing storage source...");
            var storageSourceId = await _supabaseService.GetOrCreateStorageSourceAsync(tenantId, rootPath);

            if (storageSourceId == null)
            {
                result.Failed += fileList.Count;
                result.Errors.Add("Could not get or create local storage source. Import aborted.");
                progress?.Report("Import failed: storage source unavailable.");
                return result;
            }

            foreach (var file in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Usar IsImported que ya está marcado en la galería
                    if (file.IsImported)
                    {
                        result.Skipped++;
                        progress?.Report($"Skipped (already exists): {file.FileName}");
                        continue;
                    }

                    // Insertar media_asset
                    progress?.Report($"Importing {file.FileName}...");
                    var mediaAssetId = await _supabaseService.InsertMediaAssetAsync(file, tenantId);

                    if (mediaAssetId == null)
                    {
                        result.Failed++;
                        result.Errors.Add($"Failed to insert asset: {file.RelativePath}");
                        progress?.Report($"Failed: {file.FileName}");
                        continue;
                    }

                    // Insertar media_file_instance
                    var instanceOk = await _supabaseService.InsertMediaFileInstanceAsync(
                        mediaAssetId, storageSourceId, tenantId, file);

                    if (!instanceOk)
                    {
                        result.Failed++;
                        result.Errors.Add($"Asset inserted but file instance failed: {file.RelativePath}");
                        progress?.Report($"Partial failure: {file.FileName}");
                        continue;
                    }

                    file.IsImported = true;
                    file.MediaAssetId = mediaAssetId;
                    result.Imported++;
                    progress?.Report($"Imported: {file.FileName}");
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Import cancelled by user.");
                    break;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Unexpected error for {file.RelativePath}: {ex.Message}");
                    progress?.Report($"Error: {file.FileName}");
                    System.Diagnostics.Debug.WriteLine($"[ImportService] {ex.Message}");
                }
            }

            progress?.Report(result.Summary);
            return result;
        }
    }
}
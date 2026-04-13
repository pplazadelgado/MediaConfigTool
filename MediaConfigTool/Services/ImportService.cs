using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MediaConfigTool.Models;

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
            IProgress<string> progress = null,
            CancellationToken  cancellationToken = default)
        {
            var result = new ImportResult();

            foreach(var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    progress?.Report($"Checking {file.FileName}....");

                    //Step 1 - duplicate check 
                    var exists = await _supabaseService.MediaAssetExistsAsync(tenantId, file.RelativePath);
                    if(exists)
                    {
                        result.Skipped++;
                        progress?.Report($"Skipped (already exists): {file.FileName}");
                        continue;
                    }

                    //Step 2 - insert media_asset
                    progress?.Report($"Importing {file.FileName}...");
                    var mediaAssitId = await _supabaseService.InsertMediaAssetAsync(file, tenantId);

                    if(mediaAssitId== null)
                    {
                        result.Failed++;
                        result.Errors.Add($"Failed to insert asset: {file.RelativePath}");
                        progress?.Report($"Failed: {file.FileName}");
                        continue;
                    }

                    //Step 3 - insert media file_instance
                    var instanceOk = await _supabaseService.InsertMediaFileInstanceAsync(mediaAssitId, file);

                    if (!instanceOk)
                    {
                        result.Failed++;
                        result.Errors.Add($"Asset inserted but file instance failed: {file.RelativePath}");
                        progress?.Report($"Partial failure: {file.FileName}");
                        continue;
                    }

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
        }
    }
}

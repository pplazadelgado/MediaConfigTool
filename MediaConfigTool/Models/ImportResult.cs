

namespace MediaConfigTool.Models
{
    public class ImportResult
    {
        public int Imported {  get; set; }
        public int Skipped {  get; set; }
        public int Failed {  get; set; }

        public List<string> Errors { get; set; } = new();

        public bool HasErrors => Errors.Count > 0;
        public int Total => Imported + Skipped + Failed;

        public string Summary => 
            $"{Imported} imported, {Skipped} skipped, {Failed} failed of {Total} total.";
    }
}

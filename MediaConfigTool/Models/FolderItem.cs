using System.Collections.ObjectModel;
using System.IO;

namespace MediaConfigTool.Models
{
    public class FolderItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;

        public ObservableCollection<FolderItem> SubFolders { get; } = new();

        // nodo fantasma: hace que el árbol muestre la flecha de expandir
        private static readonly FolderItem _placeholder = new() { Name = "..." };

        public FolderItem() { }

        public FolderItem(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(path) is { Length: > 0 } n ? n : path;

            // solo añade el placeholder, no carga subcarpetas todavía
            SubFolders.Add(_placeholder);
        }

        public void LoadSubFolders()
        {
            if (SubFolders.Count == 1 && SubFolders[0] == _placeholder)
            {
                SubFolders.Clear();
                try
                {
                    foreach (var dir in Directory.GetDirectories(FullPath))
                        SubFolders.Add(new FolderItem(dir));
                }
                catch { /* sin permisos, se ignora */ }
            }
        }
    }
}
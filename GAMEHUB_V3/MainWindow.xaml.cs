using IWshRuntimeLibrary;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using File = System.IO.File;

namespace GAMEHUB_V3
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameLink> Links { get; set; } = new();
        private ObservableCollection<GameLink> AllLinks { get; set; } = new(); // <- lista completa
        private Config config;
        private FileSystemWatcher watcher;

        //private string cacheFile = "cache.txt";
        //private string imageCacheFolder = "cache_images";

        private readonly string cacheFile = Path.Combine(AppPaths.BasePath, "cache.txt");
        private readonly string imageCacheFolder = Path.Combine(AppPaths.BasePath, "cache_images");

        public MainWindow()
        {
            InitializeComponent();
            GameList.ItemsSource = Links;
            config = Config.Load();

            // Crear carpetas si no existen
            AppPaths.EnsureDirectories();

            // Cargar la última carpeta usada
            this.Loaded += async (s, e) =>
            {
                if (File.Exists(cacheFile))
                {
                    string lastPath = File.ReadAllText(cacheFile);
                    if (Directory.Exists(lastPath))
                        await LoadInks(lastPath);
                }
            };
        }

        private async Task LoadInks(string folderPath)
        {
            Links.Clear();
            AllLinks.Clear();
            string[] lnkFiles = Directory.GetFiles(folderPath, "*.lnk");
            string[] urlFiles = Directory.GetFiles(folderPath, "*.url");
            string[] files = lnkFiles.Concat(urlFiles).ToArray();

            foreach (string file in files)
            {
                try
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    string targetPath = "";

                    // .lnk case
                    if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        var shell = new WshShell();
                        var link = (IWshShortcut)shell.CreateShortcut(file);
                        targetPath = link.TargetPath;
                    }
                    // .url case
                    else if (file.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = File.ReadAllLines(file);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                            {
                                targetPath = line.Substring(4).Trim();
                                break;
                            }
                        }
                    }
                    string imageFile = Path.GetFullPath(Path.Combine(AppPaths.CacheImages, name + ".jpg"));

                    if (!File.Exists(imageFile))
                        await DownloadGameImageAsync(name, imageFile);

                    var game = new GameLink
                    {
                        Name = name,
                        TargetPath = targetPath,
                        ImagePath = File.Exists(imageFile) ? imageFile : "default.jpg"
                    };

                    Links.Add(game);
                    AllLinks.Add(game);
                }
                catch
                {
                    // Ignorar errores individuales
                }
            }

            File.WriteAllText(cacheFile, folderPath);
            InitializeWatcher(folderPath);
        }

        //private BitmapSource GetIcon(string path)
        //{
        //    try
        //    {
        //        if (!File.Exists(path))
        //            return null;

        //        // Extraer icono del ejecutable o acceso directo
        //        Icon icon = Icon.ExtractAssociatedIcon(path);
        //        return Imaging.CreateBitmapSourceFromHIcon(
        //            icon.Handle,
        //            Int32Rect.Empty,
        //            BitmapSizeOptions.FromWidthAndHeight(64, 64));
        //    }
        //    catch
        //    {
        //        return null; // en caso de error, se puede usar default.png
        //    }
        //}
        private void InitializeWatcher(string folderPath)
        {
            watcher?.Dispose();

            watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*", 
            };

            watcher.Created += (s, e) =>
            {
                if (e.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.Invoke(() => LoadInks(folderPath));
                }
            };

            watcher.Deleted += (s, e) =>
            {
                if (e.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.Invoke(() => LoadInks(folderPath));
                }
            };

            watcher.Renamed += (s, e) =>
            {
                if (e.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.Invoke(() => LoadInks(folderPath));
                }
            };

            watcher.EnableRaisingEvents = true;
        }

        private void GameList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GameList.SelectedItem is GameLink game)
            {
                try
                {
                    string target = game.TargetPath?.Trim('"') ?? "";

                    if (string.IsNullOrWhiteSpace(target))
                    {
                        MessageBox.Show("Ruta del juego no válida.");
                        return;
                    }
                    // Verificar si es (.url)
                    if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("com.", StringComparison.OrdinalIgnoreCase) ||
                        target.Contains("://"))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = target,
                            UseShellExecute = true
                        });
                        return;
                    }
                    else
                    {
                        if (File.Exists(game.TargetPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = game.TargetPath,
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            MessageBox.Show("El archivo no existe.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al intentar abrir el juego: " + ex.Message);
                }
            }
        }

        // Filtro en tiempo real
        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim().ToLower();

            Links.Clear();

            var filtered = string.IsNullOrEmpty(searchText)
                ? AllLinks
                : new ObservableCollection<GameLink>(
                    AllLinks.Where(x => x.Name != null && x.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                  );

            foreach (var item in filtered)
                Links.Add(item);
        }

        private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
               await LoadInks(dialog.SelectedPath);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox_TextChanged(sender, null);
        }

        private void BtnUploadImage_Click(object sender, RoutedEventArgs e)
        {
            string path = UploadAndResizeImage();

            if (path != null)
            {
                // Asignar a juego seleccionado
                if (GameList.SelectedItem is GameLink selectedGame)
                {
                    selectedGame.ImagePath = path;
                    GameList.Items.Refresh();
                }
            }
        }

        private void BtnChangeImage_Click(object sender, RoutedEventArgs e)
        {
            if (GameList.SelectedItem is not GameLink selectedGame)
            {
                MessageBox.Show("Selecciona un juego primero.");
                return;
            }

            try
            {
                // Liberar la imagen del control para evitar bloqueos
                var container = GameList.ItemContainerGenerator.ContainerFromItem(selectedGame) as ListBoxItem;
                if (container != null)
                {
                    var imageControl = FindVisualChild<System.Windows.Controls.Image>(container);
                    if (imageControl != null)
                        imageControl.Source = null;
                }

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
                    Title = "Seleccionar nueva imagen"
                };

                if (dialog.ShowDialog() != true) return;

                string newImagePath = dialog.FileName;
                //string saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameImages");
                string saveDirectory = AppPaths.CacheImages;
                Directory.CreateDirectory(saveDirectory);

                string savePath = Path.Combine(saveDirectory, $"{selectedGame.Name}.jpg");

                // Redimensionar y guardar
                using (var original = System.Drawing.Image.FromFile(newImagePath))
                using (var resized = new System.Drawing.Bitmap(original, new System.Drawing.Size(1024, 1024)))
                {
                    resized.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                selectedGame.ImagePath = savePath;
                GameList.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cambiar la imagen: " + ex.Message);
            }
        }

        /// Método auxiliar para encontrar un hijo visual dentro del contenedor.
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    return tChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        private async Task DownloadGameImageAsync(string gameName, string savePath)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();

                string query = Uri.EscapeDataString(gameName + " game cover");
                string url = $"https://www.googleapis.com/customsearch/v1?q={query}&cx={config.SearchEngineId}&key={config.GoogleApiKey}&searchType=image&num=1";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var imageUrl = doc.RootElement
                    .GetProperty("items")[0]
                    .GetProperty("link")
                    .GetString();

                var imageData = await client.GetByteArrayAsync(imageUrl);
                // Redimensionar a 1024x1024
                using var inputStream = new MemoryStream(imageData);
                using var original = System.Drawing.Image.FromStream(inputStream);
                using var resized = new Bitmap(1024, 1024);

                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, 0, 0, 1024, 1024);
                }

                resized.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                //await File.WriteAllBytesAsync(savePath, imageData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error descargando imagen: " + ex.Message);
            }
        }

        private string UploadAndResizeImage()
        {
            try
            {
                //string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache_images");
                string cacheDir = AppPaths.CacheImages;
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                    Title = "Seleccionar imagen"
                };

                if (dialog.ShowDialog() != true)
                    return null;

                string selectedPath = dialog.FileName;
                string fileName = (GameList.SelectedItem is GameLink g ? g.Name : Path.GetFileNameWithoutExtension(selectedPath)) + ".jpg";
                string savePath = Path.Combine(cacheDir, fileName);

                //Redimensionar y guardar
                using (var original = System.Drawing.Image.FromFile(selectedPath))
                using (var resized = new System.Drawing.Bitmap(original, new System.Drawing.Size(1024, 1024)))
                {
                    resized.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                MessageBox.Show($"Imagen guardada correctamente en:\n{savePath}", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                return savePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al subir o procesar la imagen:\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }

    public class GameLink
    {
        public string Name { get; set; }
        public string TargetPath { get; set; }
        public string ImagePath { get; set; }
    }

    public class Config
    {
        public string GoogleApiKey { get; set; }
        public string SearchEngineId { get; set; }

        public static Config Load()
        {
            //string json = File.ReadAllText(path);
            //return JsonSerializer.Deserialize<Config>(json);
            string path = AppPaths.ConfigFile;

            if (!File.Exists(path))
            {
                // Crear un archivo por defecto
                var defaultConfig = new Config
                {
                    GoogleApiKey = "",
                    SearchEngineId = ""
                };
                string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(AppPaths.BasePath);
                File.WriteAllText(path, json);
            }

            string configJson = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(configJson);
        }
    }

    public static class AppPaths
    {
        public static string BasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GAMEHUB_V3");

        public static string CacheImages => Path.Combine(BasePath, "cache_images");
        public static string ConfigFile => Path.Combine(BasePath, "config.json");

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(BasePath))
                Directory.CreateDirectory(BasePath);
            if (!Directory.Exists(CacheImages))
                Directory.CreateDirectory(CacheImages);
        }
    }
}

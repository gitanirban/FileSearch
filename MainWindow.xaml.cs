using System;
using System.Collections.Generic;
using System.Diagnostics; // For Process to open files
using System.IO;
using System.Linq;
using System.Text.Json; // For JSON serialization
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // For MouseButtonEventArgs
using System.Windows.Media.Animation;

namespace FileSearchApp
{
    public partial class MainWindow : Window
    {
        // File to store the index
        private const string IndexFilePath = "fileIndex.json";
        // List to hold the indexed files
        private List<FileInfoDisplay> fileIndex = new List<FileInfoDisplay>();

        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to the Loaded event
            this.Loaded += MainWindow_Loaded;
        }

        // Loaded event handler
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if index file exists, if not, create an index
            if (!File.Exists(IndexFilePath))
            {
                // First-time startup, create the index
                await IndexFilesAsync();
            }
            else
            {
                // Load the index from the file on subsequent runs
                LoadIndexFromFile();
            }
        }

        // Class to store and display file information
        public class FileInfoDisplay
        {
            public required string Name { get; set; }
            public required string Path { get; set; }
        }

        // Asynchronously index files across all drives
        private async Task IndexFilesAsync()
        {
            // Show a message to indicate indexing is in progress
            MessageBox.Show("Indexing files. This may take some time. Please wait...", "Indexing", MessageBoxButton.OK, MessageBoxImage.Information);

            // Show the loading spinner
            LoadingSpinner.Visibility = Visibility.Visible;
            StartSpinnerAnimation();

            // Run the file indexing in a background task
            fileIndex = await Task.Run(() => IndexFiles());

            // Save the index to a file
            SaveIndexToFile();

            // Hide the loading spinner
            StopSpinnerAnimation();
            LoadingSpinner.Visibility = Visibility.Collapsed;

            // Notify the user that indexing is complete
            MessageBox.Show("File indexing completed successfully!", "Indexing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Method to index all files on available drives
        private List<FileInfoDisplay> IndexFiles()
        {
            var results = new List<FileInfoDisplay>();
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();

            foreach (var drive in drives)
            {
                try
                {
                    results.AddRange(IndexDirectory(drive.Name));
                }
                catch (UnauthorizedAccessException)
                {
                    // Handle directories we can't access
                    Console.WriteLine($"Access denied to drive {drive.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accessing drive {drive.Name}: {ex.Message}");
                }
            }

            return results;
        }

        // Recursively index each directory
        private List<FileInfoDisplay> IndexDirectory(string directory)
        {
            var results = new List<FileInfoDisplay>();

            try
            {
                // Get all files in the current directory
                var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    results.Add(new FileInfoDisplay
                    {
                        Name = Path.GetFileName(file),
                        Path = file
                    });
                }

                // Get all subdirectories
                var subDirectories = Directory.GetDirectories(directory);

                foreach (var subDir in subDirectories)
                {
                    try
                    {
                        results.AddRange(IndexDirectory(subDir));
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories without permission
                        Console.WriteLine($"Skipping directory due to access restriction: {subDir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Access denied to directory {directory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing directory {directory}: {ex.Message}");
            }

            return results;
        }

        // Save the indexed files to a JSON file
        private void SaveIndexToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(fileIndex);
                File.WriteAllText(IndexFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving index to file: {ex.Message}");
            }
        }

        // Load the index from the JSON file
        private void LoadIndexFromFile()
        {
            try
            {
                var json = File.ReadAllText(IndexFilePath);
                fileIndex = JsonSerializer.Deserialize<List<FileInfoDisplay>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading index from file: {ex.Message}");
            }
        }

        // Event handler for the TextBox TextChanged event
        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string searchTerm = SearchTextBox.Text;

            try
            {
                // Clear previous search results only if there are items
                if (ResultsListView.Items.Count > 0)
                {
                    ResultsListView.Items.Clear();
                }
            }
            catch (Exception ex)
            {
                // Log or display the error message
                Console.WriteLine($"Error clearing items: {ex.Message}");
            }

            if (string.IsNullOrEmpty(searchTerm))
            {
                return; // Exit if no search term is provided
            }

            try
            {
                // Perform the search on the pre-built index
                var results = fileIndex.Where(f => f.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                                   f.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

                // Display the results in the ListView
                foreach (var file in results)
                {
                    ResultsListView.Items.Add(file);
                }
            }
            catch (Exception ex)
            {
                // Log or display the error message
                Console.WriteLine($"Error during search: {ex.Message}");
            }
        }

        // Start the spinner animation
        private void StartSpinnerAnimation()
        {
            var storyboard = (Storyboard)LoadingSpinner.Resources["SpinStoryboard"];
            storyboard.Begin();
        }

        // Stop the spinner animation
        private void StopSpinnerAnimation()
        {
            var storyboard = (Storyboard)LoadingSpinner.Resources["SpinStoryboard"];
            storyboard.Stop();
        }

        // Event handler for the right-click context menu
        private void ResultsListView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ResultsListView.SelectedItem == null)
            {
                ResultsListView.UnselectAll();
            }
        }

        // Event handler for the Open File menu item
        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsListView.SelectedItem is FileInfoDisplay selectedFile)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(selectedFile.Path) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Event handler for the Open File Location menu item
        private void OpenFileLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsListView.SelectedItem is FileInfoDisplay selectedFile)
            {
                try
                {
                    string folderPath = Path.GetDirectoryName(selectedFile.Path);
                    Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Event handler for the Reset Index button click
        private async void ResetIndexButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm the action
            var result = MessageBox.Show("Are you sure you want to reset the index and start a new indexing process?", 
                                          "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                // Delete the existing index file if it exists
                if (File.Exists(IndexFilePath))
                {
                    File.Delete(IndexFilePath);
                }

                // Clear the previous file index
                fileIndex.Clear();

                // Start the indexing process
                await IndexFilesAsync();
            }
        }
    }
}

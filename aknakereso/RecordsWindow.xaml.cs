using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aknakereso
{
    public partial class RecordsWindow : Window
    {
        public RecordsWindow()
        {
            InitializeComponent();
            DifficultyCombo.SelectedIndex = 0; // alapértelmezett: Könnyű
        }

        private void DifficultyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyCombo.SelectedItem is ComboBoxItem item)
            {
                string difficulty = item.Content.ToString();
                LoadRecords(difficulty);
            }
        }

        private void LoadRecords(string difficulty)
        {
            using var db = new GameDbContext();

            var records = db.Results
                .Where(r => r.Difficulty == difficulty)
                .OrderBy(r => r.TimeSeconds)
                .Take(10)
                .ToList();

            RecordsGrid.ItemsSource = records;
        }
    }
}

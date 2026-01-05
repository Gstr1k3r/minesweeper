using System;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Aknakereso
{
    public partial class MainWindow : Window
    {
        private int Rows;
        private int Cols;
        private int MineCount;
        private string selectedDifficulty = string.Empty;

        private Cell[,] board;
        private DispatcherTimer timer;
        private int secondsElapsed;

        public MainWindow()
        {
            InitializeComponent();

            using (var db = new GameDbContext())
            {
                db.Database.EnsureCreated(); // ha nincs adatbázis vagy tábla, létrehozza
            }

        }

        // --- NEHÉZSÉG VÁLASZTÁS ---
        private void EasyButton_Click(object sender, RoutedEventArgs e)
        {
            Rows = 8; Cols = 8; MineCount = 10;
            selectedDifficulty = "Könnyű";
            StartGame();
        }

        private void MediumButton_Click(object sender, RoutedEventArgs e)
        {
            Rows = 16; Cols = 16; MineCount = 40;
            selectedDifficulty = "Közepes";
            StartGame();
        }

        private void HardButton_Click(object sender, RoutedEventArgs e)
        {
            Rows = 24; Cols = 24; MineCount = 99;
            selectedDifficulty = "Nehéz";
            StartGame();
        }

        private void StartGame()
        {
            DifficultyPanel.Visibility = Visibility.Collapsed;
            StatsPanel.Visibility = Visibility.Visible;
            InitGame();
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            GameGrid.Children.Clear();
            GameGrid.RowDefinitions.Clear();
            GameGrid.ColumnDefinitions.Clear();

            StatsPanel.Visibility = Visibility.Collapsed;
            DifficultyPanel.Visibility = Visibility.Visible;

            timer?.Stop();
            TimerText.Text = "Idő: 0";
        }

        // --- JÁTÉK INIT ---
        private void InitGame()
        {
            GameGrid.RowDefinitions.Clear();
            GameGrid.ColumnDefinitions.Clear();

            for (int r = 0; r < Rows; r++)
                GameGrid.RowDefinitions.Add(new RowDefinition());

            for (int c = 0; c < Cols; c++)
                GameGrid.ColumnDefinitions.Add(new ColumnDefinition());

            board = new Cell[Rows, Cols];

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    board[r, c] = new Cell();

            PlaceMines();
            CalculateAdjacentMines();
            CreateButtons();
            UpdateMineCounter();

            timer?.Stop();
            secondsElapsed = 0;
            TimerText.Text = "Idő: 0";

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) =>
            {
                secondsElapsed++;
                TimerText.Text = $"Idő: {secondsElapsed}";
            };
            timer.Start();
        }

        // --- CELL OSZTÁLY ---
        public class Cell
        {
            public bool IsMine;
            public bool IsRevealed;
            public bool IsFlagged;
            public int AdjacentMines;
        }

        // --- AKNA LOGIKA ---
        private void PlaceMines()
        {
            Random rnd = new Random();
            int placed = 0;

            while (placed < MineCount)
            {
                int r = rnd.Next(Rows);
                int c = rnd.Next(Cols);

                if (!board[r, c].IsMine)
                {
                    board[r, c].IsMine = true;
                    placed++;
                }
            }
        }

        private void CalculateAdjacentMines()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if (board[r, c].IsMine) continue;

                    int count = 0;

                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;

                            int nr = r + dr;
                            int nc = c + dc;

                            if (nr >= 0 && nr < Rows &&
                                nc >= 0 && nc < Cols &&
                                board[nr, nc].IsMine)
                            {
                                count++;
                            }
                        }

                    board[r, c].AdjacentMines = count;
                }
            }
        }

        // --- UI ---
        private void CreateButtons()
        {
            GameGrid.Children.Clear();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    Button btn = new Button
                    {
                        Tag = (r, c),
                        FontWeight = FontWeights.Bold,
                        FontSize = 16
                    };

                    btn.Click += Cell_Click;
                    btn.MouseRightButtonUp += Cell_RightClick;

                    Grid.SetRow(btn, r);
                    Grid.SetColumn(btn, c);
                    GameGrid.Children.Add(btn);
                }
            }
        }

        private void Cell_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            var (r, c) = ((int, int))btn.Tag;
            Cell cell = board[r, c];

            if (cell.IsFlagged || cell.IsRevealed)
                return;

            if (cell.IsMine)
            {
                PlayExplosionSound();
                RevealAll();
                MessageBox.Show("💣 Vesztettél!");
                timer.Stop();
                return;
            }

            RevealCell(r, c);
            PlayClickSound();

            if (CheckWin())
            {
                PlayWinSound();
                timer.Stop();

                // Save successful result to database
                SaveResult(selectedDifficulty, secondsElapsed);

                MessageBox.Show("🎉 Nyertél!");
                using (var db = new GameDbContext())
                {
                    db.Results.Add(new Result
                    {
                        Difficulty = selectedDifficulty, // pl. "Könnyű", "Közepes", "Nehéz"
                        TimeSeconds = secondsElapsed,
                        PlayedAt = DateTime.Now
                    });
                    db.SaveChanges(); // 🔹 ez kell, különben nem kerül mentésre
                    Console.WriteLine($"Mentés: {selectedDifficulty}, {secondsElapsed} sec");

                }

                RevealAll();
            }
        }

        private void Cell_RightClick(object sender, MouseButtonEventArgs e)
        {
            Button btn = (Button)sender;
            var (r, c) = ((int, int))btn.Tag;
            Cell cell = board[r, c];

            if (cell.IsRevealed) return;

            cell.IsFlagged = !cell.IsFlagged;
            btn.Content = cell.IsFlagged ? "🚩" : "";
            UpdateMineCounter();
        }

        // --- Felfedés ---
        private void RevealCell(int r, int c)
        {
            if (r < 0 || r >= Rows || c < 0 || c >= Cols)
                return;

            Cell cell = board[r, c];
            if (cell.IsRevealed || cell.IsFlagged)
                return;

            cell.IsRevealed = true;
            Button btn = GetButton(r, c);
            btn.IsEnabled = false;

            if (cell.AdjacentMines > 0)
            {
                btn.Content = cell.AdjacentMines;
                btn.Foreground = GetNumberColor(cell.AdjacentMines);
                return;
            }

            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (dr != 0 || dc != 0)
                        RevealCell(r + dr, c + dc);
        }

        private void RevealAll()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    Button btn = GetButton(r, c);
                    Cell cell = board[r, c];

                    if (cell.IsMine)
                        btn.Content = "💣";

                    btn.IsEnabled = false;
                }
        }

        private bool CheckWin()
        {
            return board.Cast<Cell>().All(c => c.IsMine || c.IsRevealed);
        }

        // --- SEGÉDEK ---
        private Button GetButton(int r, int c)
        {
            return GameGrid.Children
                .Cast<Button>()
                .First(b => Grid.GetRow(b) == r && Grid.GetColumn(b) == c);
        }

        private Brush GetNumberColor(int n) => n switch
        {
            1 => Brushes.Blue,
            2 => Brushes.Green,
            3 => Brushes.Red,
            4 => Brushes.DarkBlue,
            5 => Brushes.Maroon,
            6 => Brushes.Teal,
            _ => Brushes.Black
        };

        private void UpdateMineCounter()
        {
            int flags = board.Cast<Cell>().Count(c => c.IsFlagged);
            MineCounter.Text = $"Aknák: {MineCount - flags}";
        }

        // --- HANGOK ---
        private void PlayClickSound() => PlaySound("Sounds/click.wav");
        private void PlayExplosionSound() => PlaySound("Sounds/explosion.wav");
        private void PlayWinSound() => PlaySound("Sounds/win.wav");

        private void PlaySound(string file)
        {
            try { new SoundPlayer(file).Play(); }
            catch { }
        }

        // --- DB: mentés rekord ---
        private void SaveResult(string difficulty, int timeSeconds)
        {
            try
            {
                using var db = new GameDbContext();
                db.Results.Add(new Result
                {
                    Difficulty = difficulty,
                    TimeSeconds = timeSeconds,
                    PlayedAt = DateTime.Now
                });
                db.SaveChanges();
            }
            catch
            {
                // Swallow DB errors so UI isn't blocked; consider logging in real app.
            }
        }

        // --- REKORDOK ABLAK ---
        private void RecordsButton_Click(object sender, RoutedEventArgs e)
        {
            RecordsWindow w = new RecordsWindow();
            w.Owner = this;
            w.ShowDialog();
        }

    }
}

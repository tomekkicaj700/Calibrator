using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using WelderRS232;
using static Logger.LoggerService;

namespace Calibrator.Controls
{
    public partial class MeasurementHistoryNewTab : UserControl
    {
        private readonly ObservableCollection<MeasurementDataRecord> measurements;
        private readonly string dataFilePath = "measurement_data.csv";

        // Asynchroniczne buforowanie dla wydajności
        private readonly List<MeasurementDataRecord> measurementBuffer = new();
        private readonly object bufferLock = new object();
        private readonly BackgroundWorker bufferWorker;
        private bool isProcessingBuffer = false;

        // Indeks od którego liczyć statystyki po resecie
        private int statsStartIndex = 0;

        private bool _suppressLayoutUpdate = false;
        private double? _lastProportion = null;
        private MainWindow? _mainWindow;
        private bool _splitterRestored = false;

        public MeasurementHistoryNewTab()
        {
            InitializeComponent();
            measurements = new ObservableCollection<MeasurementDataRecord>();
            dataGridMeasurements.ItemsSource = measurements;

            this.Loaded += MeasurementHistoryNewTab_Loaded;

            // Dodaj obsługę SizeChanged dla grida
            var grid = (Grid)this.Content;
            grid.SizeChanged += Grid_SizeChanged;

            // BackgroundWorker do przetwarzania bufora w tle
            bufferWorker = new BackgroundWorker();
            bufferWorker.DoWork += BufferWorker_DoWork;
            bufferWorker.RunWorkerCompleted += BufferWorker_RunWorkerCompleted;
            bufferWorker.WorkerSupportsCancellation = true;

            // Automatyczne wczytanie danych z pliku przy starcie
            LoadDataFromFile();
        }

        private MainWindow? GetMainWindow()
        {
            if (_mainWindow != null) return _mainWindow;

            // Znajdź MainWindow w drzewie wizualnym
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is MainWindow mainWindow)
                {
                    _mainWindow = mainWindow;
                    return _mainWindow;
                }
                parent = parent is FrameworkElement element ? element.Parent : null;
            }
            return null;
        }

        private void BufferWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            // Przetwarzanie w tle - symulacja pracy
            System.Threading.Thread.Sleep(5);
        }

        private void BufferWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            // Aktualizacja UI po zakończeniu przetwarzania w tle
            List<MeasurementDataRecord> bufferCopy;
            lock (bufferLock)
            {
                if (measurementBuffer.Count == 0)
                {
                    isProcessingBuffer = false;
                    return;
                }

                bufferCopy = new List<MeasurementDataRecord>(measurementBuffer);
                measurementBuffer.Clear();
            }

            // Aktualizuj UI w wątku UI
            Dispatcher.BeginInvoke(() =>
            {
                foreach (var record in bufferCopy)
                {
                    measurements.Add(record);
                }
                isProcessingBuffer = false;
                CalculateStatistics();
                // Scrolluj DataGrid na dół
                if (dataGridMeasurements.Items.Count > 0)
                {
                    dataGridMeasurements.ScrollIntoView(dataGridMeasurements.Items[dataGridMeasurements.Items.Count - 1]);
                }
            });
        }

        private void ProcessBufferInBackground()
        {
            if (isProcessingBuffer || bufferWorker.IsBusy) return;

            isProcessingBuffer = true;
            bufferWorker.RunWorkerAsync();
        }

        public void AddMeasurement(WeldParameters parameters)
        {
            var record = new MeasurementDataRecord
            {
                Index = measurements.Count + measurementBuffer.Count + 1,
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                Voltage = parameters.NapiecieZgrzewania,
                Current = parameters.PradZgrzewania,
                VoltageADC = parameters.ADCNapZgrzew,
                CurrentADC = parameters.ADCPradZgrzew
            };

            // Dodaj do bufora
            lock (bufferLock)
            {
                measurementBuffer.Add(record);
            }

            // Aktualizuj komponent pomiarów zgrzewania
            UpdateWeldMeasurementsComponent(parameters);

            // Uruchom przetwarzanie bufora w tle
            ProcessBufferInBackground();
        }

        public void UpdateWeldMeasurementsComponent(WeldParameters parameters)
        {
            Dispatcher.BeginInvoke(() =>
            {
                weldMeasurementsComponent.UpdateWeldParameters(parameters);
            });
        }

        public void ClearData()
        {
            lock (bufferLock)
            {
                measurementBuffer.Clear();
            }
            measurements.Clear();
            ClearStatistics();

            // Wyczyść komponent pomiarów zgrzewania
            Dispatcher.BeginInvoke(() =>
            {
                weldMeasurementsComponent.UpdateWeldParameters(new WeldParameters());
            });
        }

        public void SaveDataToFile()
        {
            try
            {
                // Poczekaj na zakończenie przetwarzania bufora
                while (isProcessingBuffer || bufferWorker.IsBusy)
                {
                    System.Threading.Thread.Sleep(10);
                }

                // Dodaj wszystkie pomiary z bufora
                lock (bufferLock)
                {
                    foreach (var record in measurementBuffer)
                    {
                        measurements.Add(record);
                    }
                    measurementBuffer.Clear();
                }

                using (var writer = new StreamWriter(dataFilePath, false))
                {
                    // Nagłówek
                    writer.WriteLine("Lp.;Czas;Napięcie [V];Prąd [A];ADC Napięcia;ADC Prądu");

                    // Dane
                    foreach (var measurement in measurements)
                    {
                        writer.WriteLine($"{measurement.Index};{measurement.Timestamp};{measurement.Voltage:F2};{measurement.Current:F2};{measurement.VoltageADC:D5};{measurement.CurrentADC:D5}");
                    }
                }
                Log($"Zapisano {measurements.Count} pomiarów do pliku {dataFilePath}");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas zapisywania danych: {ex.Message}");
                MessageBox.Show($"Błąd podczas zapisywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadDataFromFile()
        {
            try
            {
                if (!File.Exists(dataFilePath))
                {
                    Log("Plik z danymi pomiarowymi nie istnieje");
                    return;
                }

                measurements.Clear();
                var lines = File.ReadAllLines(dataFilePath);

                // Pomiń nagłówek
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(';');
                    if (parts.Length >= 6)
                    {
                        var record = new MeasurementDataRecord
                        {
                            Index = int.Parse(parts[0]),
                            Timestamp = parts[1],
                            Voltage = double.Parse(parts[2]),
                            Current = double.Parse(parts[3]),
                            VoltageADC = int.Parse(parts[4]),
                            CurrentADC = int.Parse(parts[5])
                        };
                        measurements.Add(record);
                    }
                }
                Log($"Wczytano {measurements.Count} pomiarów z pliku {dataFilePath}");
                CalculateStatistics();
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas wczytywania danych: {ex.Message}");
                MessageBox.Show($"Błąd podczas wczytywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateStatistics()
        {
            // Statystyki tylko dla pomiarów po resecie
            var statsMeasurements = measurements.Skip(statsStartIndex).ToList();
            if (statsMeasurements.Count == 0)
            {
                measurementStatisticsComponent.ClearAll();
                return;
            }

            // Napięcie
            var voltageValues = statsMeasurements.Select(m => m.Voltage).ToList();
            double vMin = voltageValues.Min();
            double vMax = voltageValues.Max();
            measurementStatisticsComponent.SetVoltageStats(
                $"Min: {vMin:F2}",
                $"Max: {vMax:F2}",
                $"Śr: {voltageValues.Average():F2}",
                $"Δ: {(vMax - vMin):F2}");

            // Prąd
            var currentValues = statsMeasurements.Select(m => m.Current).ToList();
            double cMin = currentValues.Min();
            double cMax = currentValues.Max();
            measurementStatisticsComponent.SetCurrentStats(
                $"Min: {cMin:F2}",
                $"Max: {cMax:F2}",
                $"Śr: {currentValues.Average():F2}",
                $"Δ: {(cMax - cMin):F2}");

            // ADC Napięcia
            var voltageADCValues = statsMeasurements.Select(m => m.VoltageADC).ToList();
            int vADCMin = voltageADCValues.Min();
            int vADCMax = voltageADCValues.Max();
            measurementStatisticsComponent.SetVoltageADCStats(
                $"Min: {vADCMin:D5}",
                $"Max: {vADCMax:D5}",
                $"Śr: {voltageADCValues.Average():F0}",
                $"Δ: {(vADCMax - vADCMin):D5}");

            // ADC Prądu
            var currentADCValues = statsMeasurements.Select(m => m.CurrentADC).ToList();
            int cADCMin = currentADCValues.Min();
            int cADCMax = currentADCValues.Max();
            measurementStatisticsComponent.SetCurrentADCStats(
                $"Min: {cADCMin:D5}",
                $"Max: {cADCMax:D5}",
                $"Śr: {currentADCValues.Average():F0}",
                $"Δ: {(cADCMax - cADCMin):D5}");

            Log($"Obliczono statystyki dla {statsMeasurements.Count} pomiarów od ostatniego resetu");

            // Aktualizuj komponent pomiarów zgrzewania z najnowszymi danymi
            if (statsMeasurements.Count > 0)
            {
                var latestMeasurement = statsMeasurements.Last();
                var latestParameters = new WeldParameters
                {
                    NapiecieZgrzewania = latestMeasurement.Voltage,
                    PradZgrzewania = latestMeasurement.Current,
                    ADCNapZgrzew = latestMeasurement.VoltageADC,
                    ADCPradZgrzew = latestMeasurement.CurrentADC
                };
                UpdateWeldMeasurementsComponent(latestParameters);
            }
        }

        private void ClearStatistics()
        {
            measurementStatisticsComponent.ClearAll();
        }

        private void btnCalculateStats_Click(object sender, RoutedEventArgs e)
        {
            CalculateStatistics();
        }

        private void btnOpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dataFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Plik z danymi pomiarowymi nie istnieje.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas otwierania pliku: {ex.Message}");
                MessageBox.Show($"Błąd podczas otwierania pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClearData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz wyczyścić wszystkie dane pomiarowe?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearData();
                Log("Wyczyszczono dane pomiarowe");
            }
        }

        private void btnResetStats_Click(object sender, RoutedEventArgs e)
        {
            // Dodaj separator do measurements
            var separator = new MeasurementDataRecord
            {
                Index = 0,
                Timestamp = "--- RESET ---",
                Voltage = 0,
                Current = 0,
                VoltageADC = 0,
                CurrentADC = 0,
                IsSessionSeparator = true
            };
            measurements.Add(separator);
            statsStartIndex = measurements.Count;
            ClearStatistics();
            Log("Zresetowano statystyki pomiarowe");
        }

        private void MeasurementHistoryNewTab_Loaded(object sender, RoutedEventArgs e)
        {
            // Przywracanie splittera zostanie wykonane w Grid_SizeChanged
            Log("[SPLITTER RESTORE] Loaded - czekam na SizeChanged grida");
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Przywróć splitter tylko raz, gdy grid ma sensowny rozmiar
            if (e.NewSize.Width > 100 && !_splitterRestored)
            {
                RestoreSplitter();
                _splitterRestored = true;
            }
        }

        private void RestoreSplitter()
        {
            var settings = Calibrator.WindowSettings.Load();
            if (settings.TablePanelProportion.HasValue && settings.TablePanelProportion.Value > 0 && settings.TablePanelProportion.Value < 1.01)
            {
                var mainWindow = GetMainWindow();
                if (mainWindow != null)
                {
                    var grid = (Grid)this.Content;
                    double windowWidth = mainWindow.ActualWidth;
                    double gridWidth = grid.ActualWidth;
                    double proportion = settings.TablePanelProportion.Value;
                    double minRight = grid.ColumnDefinitions[2].MinWidth;
                    double left, right;
                    if (proportion >= 1.0)
                    {
                        left = gridWidth;
                        right = minRight;
                    }
                    else
                    {
                        left = gridWidth * proportion;
                        right = gridWidth - left - grid.ColumnDefinitions[1].ActualWidth;
                        if (right < minRight) right = minRight;
                        if (left < grid.ColumnDefinitions[0].MinWidth) left = grid.ColumnDefinitions[0].MinWidth;
                    }
                    _suppressLayoutUpdate = true;
                    grid.ColumnDefinitions[0].Width = new GridLength(left, GridUnitType.Pixel);
                    grid.ColumnDefinitions[2].Width = new GridLength(right, GridUnitType.Pixel);
                    _suppressLayoutUpdate = false;
                    Log($"[SPLITTER RESTORE] Okno: {windowWidth:F0}px, Grid: {gridWidth:F0}px, Proporcja do przywrócenia: {proportion:F4}");
                    Log($"[SPLITTER RESTORE] Ustawiam szerokość kolumny: {left:F0}px, prawa kolumna: {right:F0}px");
                }
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SaveSplitterProportion();
        }

        private void SaveSplitterProportion()
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            var grid = (Grid)this.Content;
            double gridWidth = grid.ActualWidth;
            double tableWidth = grid.ColumnDefinitions[0].ActualWidth;
            double rightWidth = grid.ColumnDefinitions[2].ActualWidth;
            double windowWidth = mainWindow.ActualWidth;

            if (gridWidth < 100) return;

            double proportion;
            if (rightWidth < 5)
                proportion = 1.0;
            else
                proportion = tableWidth / gridWidth;

            if (_lastProportion.HasValue && Math.Abs(_lastProportion.Value - proportion) < 0.001) return;
            _lastProportion = proportion;
            var settings = Calibrator.WindowSettings.Load();
            settings.TablePanelProportion = proportion;
            settings.Save();
            Log($"[SPLITTER SAVE] Okno: {windowWidth:F0}px, Grid: {gridWidth:F0}px, Zapisuję proporcję: {proportion:F4} (tabela: {tableWidth:F0}px, prawa: {rightWidth:F0}px)");
        }
    }

    public class MeasurementDataRecord
    {
        public int Index { get; set; }
        public string Timestamp { get; set; } = "";
        public double Voltage { get; set; }
        public double Current { get; set; }
        public int VoltageADC { get; set; }
        public int CurrentADC { get; set; }
        // Czy to separator?
        public bool IsSessionSeparator { get; set; } = false;
    }
}
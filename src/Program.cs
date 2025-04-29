using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace ProcessMonitor
{
    public partial class MainForm : Form
    {
        private ListView listView = new();
        private System.Windows.Forms.Timer updateTimer = new();
        private readonly Dictionary<int, (DateTime Time, TimeSpan Total)> previousProcessTimes = new();
        private int sortColumn = 2; // По умолчанию сортировка по памяти
        private bool sortAscending = false;

        public MainForm()
        {
            try
            {
                InitializeComponents();
                StartMonitoring();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации: {ex.Message}", "Ошибка", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void InitializeComponents()
        {
            try
            {
                this.Text = "Монитор процессов";
                this.Size = new System.Drawing.Size(800, 600);
                this.MinimumSize = new System.Drawing.Size(600, 400);

                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.MultiSelect = false;

                listView.Columns.Add("PID", 70);
                listView.Columns.Add("Имя процесса", 200);
                listView.Columns.Add("Память (МБ)", 100);
                listView.Columns.Add("CPU (%)", 100);

                listView.ColumnClick += ListView_ColumnClick;

                this.Controls.Add(listView);

                updateTimer.Interval = 1000; // Обновление каждую секунду
                updateTimer.Tick += UpdateTimer_Tick;
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при инициализации компонентов", ex);
            }
        }

        private void StartMonitoring()
        {
            try
            {
                UpdateProcessList(); // Первоначальное обновление
                updateTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске мониторинга: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                UpdateProcessList();
            }
            catch (Exception ex)
            {
                updateTimer.Stop();
                MessageBox.Show($"Ошибка при обновлении списка процессов: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateProcessList()
        {
            var processes = Process.GetProcesses();
            var currentTime = DateTime.Now;
            var updatedProcessTimes = new Dictionary<int, (DateTime Time, TimeSpan Total)>();
            var cpuValues = new Dictionary<int, float>();

            foreach (var process in processes)
            {
                try
                {
                    var currentTotalProcessorTime = process.TotalProcessorTime;
                    if (previousProcessTimes.TryGetValue(process.Id, out var previousInfo))
                    {
                        var timeDiff = currentTime - previousInfo.Time;
                        if (timeDiff.TotalMilliseconds > 0)
                        {
                            var cpuDiff = (currentTotalProcessorTime - previousInfo.Total).TotalMilliseconds;
                            var cpuUsage = (cpuDiff / (timeDiff.TotalMilliseconds * Environment.ProcessorCount)) * 100;
                            cpuValues[process.Id] = (float)Math.Min(100, Math.Max(0, cpuUsage));
                        }
                    }
                    updatedProcessTimes[process.Id] = (currentTime, currentTotalProcessorTime);
                }
                catch
                {
                    // Игнорируем ошибки для отдельных процессов
                }
            }

            previousProcessTimes.Clear();
            foreach (var kvp in updatedProcessTimes)
            {
                previousProcessTimes[kvp.Key] = kvp.Value;
            }

            listView.BeginUpdate();
            var selectedItems = listView.SelectedItems.Cast<ListViewItem>()
                .Select(item => item.SubItems[0].Text).ToList();
            listView.Items.Clear();

            var items = new List<ListViewItem>();
            foreach (var process in processes)
            {
                try
                {
                    var item = new ListViewItem(process.Id.ToString());
                    item.SubItems.Add(process.ProcessName);
                    
                    // Память в МБ
                    var memoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
                    item.SubItems.Add(memoryMB.ToString("F2"));
                    
                    // CPU
                    var cpuUsage = cpuValues.GetValueOrDefault(process.Id, 0);
                    item.SubItems.Add(cpuUsage.ToString("F2"));
                    
                    // Сохраняем значения для сортировки
                    item.Tag = new object[] { process.Id, memoryMB, cpuUsage };
                    
                    items.Add(item);
                }
                catch
                {
                    // Игнорируем ошибки для отдельных процессов
                }
            }

            // Применяем текущую сортировку
            SortItems(items, sortColumn, sortAscending);

            listView.Items.AddRange(items.ToArray());
            listView.EndUpdate();

            // Восстанавливаем выделение
            foreach (ListViewItem item in listView.Items)
            {
                if (selectedItems.Contains(item.SubItems[0].Text))
                {
                    item.Selected = true;
                }
            }
        }

        private void SortItems(List<ListViewItem> items, int column, bool ascending)
        {
            items.Sort((a, b) =>
            {
                try
                {
                    var values1 = (object[])a.Tag;
                    var values2 = (object[])b.Tag;
                    var compareResult = 0;

                    switch (column)
                    {
                        case 0: // PID
                            compareResult = ((int)values1[0]).CompareTo((int)values2[0]);
                            break;
                        case 1: // Имя процесса
                            compareResult = string.Compare(a.SubItems[1].Text, b.SubItems[1].Text, StringComparison.OrdinalIgnoreCase);
                            break;
                        case 2: // Память
                            compareResult = ((double)values1[1]).CompareTo((double)values2[1]);
                            break;
                        case 3: // CPU
                            compareResult = ((float)values1[2]).CompareTo((float)values2[2]);
                            break;
                    }

                    return ascending ? compareResult : -compareResult;
                }
                catch
                {
                    return 0;
                }
            });
        }

        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            try
            {
                if (e.Column == sortColumn)
                {
                    sortAscending = !sortAscending;
                }
                else
                {
                    sortColumn = e.Column;
                    sortAscending = false;
                }

                var items = listView.Items.Cast<ListViewItem>().ToList();
                SortItems(items, sortColumn, sortAscending);
                
                listView.BeginUpdate();
                listView.Items.Clear();
                listView.Items.AddRange(items.ToArray());
                listView.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сортировке: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [STAThread]
        static void Main()
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 

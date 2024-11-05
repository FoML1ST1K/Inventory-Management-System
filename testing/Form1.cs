using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace testing {
    public partial class Form1 : Form {
        private Directory _directory = new Directory();
        private DataProcessor _processor;

        public Form1() {
            InitializeComponent();
            _processor = new DataProcessor(_directory);

            // Проверка системных требований при запуске
            if (!CheckSystemRequirements()) {
                MessageBox.Show("Системные характеристики не соответствуют минимальным требованиям.",
                                "Ошибка совместимости", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            // Инициализация окна приложения
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(834, 489);
        }

        // Метод для проверки выполнения минимальных системных требований
        private bool CheckSystemRequirements() {
            return CheckProcessorSpeed(1500) && CheckRAM(1024) && CheckDiskSpace(50) && CheckVideoMemory(128);
        }

        // Проверка скорости процессора (в МГц)
        private bool CheckProcessorSpeed(int minSpeedMHz) {
            using (var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor")) {
                foreach (ManagementObject obj in searcher.Get()) {
                    if (Convert.ToInt32(obj["MaxClockSpeed"]) < minSpeedMHz) return false;
                }
            }
            return true;
        }

        // Проверка объема оперативной памяти (в МБ)
        private bool CheckRAM(int minRAMMB) {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem")) {
                foreach (ManagementObject obj in searcher.Get()) {
                    if (Convert.ToInt64(obj["TotalVisibleMemorySize"]) / 1024 < minRAMMB) return false;
                }
            }
            return true;
        }

        // Проверка доступного дискового пространства (в ГБ)
        private bool CheckDiskSpace(int minDiskGB) {
            foreach (var drive in System.IO.DriveInfo.GetDrives()) {
                if (drive.IsReady && drive.DriveType == System.IO.DriveType.Fixed && drive.TotalFreeSpace / (1024 * 1024 * 1024) >= minDiskGB) {
                    return true;
                }
            }
            return false;
        }

        // Проверка объема видеопамяти (в МБ)
        private bool CheckVideoMemory(int minVideoMB) {
            using (var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController")) {
                foreach (ManagementObject obj in searcher.Get()) {
                    if (Convert.ToInt64(obj["AdapterRAM"]) / (1024 * 1024) >= minVideoMB) return true;
                }
            }
            return false;
        }

        // Обновление таблиц на экране
        private void UpdateTables() {
            UpdateTable(receiveDataGridView, _processor.GetInputData());
            UpdateTable(shipDataGridView, _processor.GetOutputData());
        }

        // Обновление данных одной таблицы
        private void UpdateTable(DataGridView gridView, Dictionary<string, DataObject> data) {
            gridView.Rows.Clear();
            foreach (var obj in data.Values) {
                gridView.Rows.Add(obj.Name, obj.Quantity);
            }
        }

        // Обработка очистки всех данных
        private void clearButton_Click(object sender, EventArgs e) {
            _processor.ClearTables();
            UpdateTables();
        }

        // Проверка, является ли идентификатор строкой из 24-х HEX символов
        private bool IsHexId(string id) {
            return Regex.IsMatch(id, @"^[A-Fa-f0-9]{24}$");
        }

        // Обработка нажатия Enter в поле для приема данных
        private void textBoxReceive_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                foreach (var id in textBoxReceive.Text.Trim().Split(' ')) {
                    if (IsHexId(id)) _processor.AddToTable(id, true); // Добавление в таблицу приема
                    else MessageBox.Show($"Неверный формат идентификатора: {id}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdateTables();
                textBoxReceive.Clear();
            }
        }

        // Обработка нажатия Enter в поле для отгрузки данных
        private void textBoxShip_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                foreach (var id in textBoxShip.Text.Trim().Split(' ')) {
                    if (IsHexId(id)) _processor.AddToTable(id, false); // Добавление в таблицу отгрузки
                    else MessageBox.Show($"Неверный формат идентификатора: {id}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdateTables();
                textBoxShip.Clear();
            }
        }
    }

    // Класс для хранения данных об объекте (идентификатор, имя, количество)
    public class DataObject {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; } = 0;
    }

    // Класс для справочника объектов (директория с данными об объектах)
    public class Directory {
        private Dictionary<string, DataObject> _dataObjects = new Dictionary<string, DataObject>();

        // Добавление объекта в справочник, если он отсутствует
        public void AddObject(DataObject obj) {
            if (!_dataObjects.ContainsKey(obj.Id.ToUpper())) {
                obj.Id = obj.Id.ToUpper();
                _dataObjects[obj.Id] = obj;
            }
        }

        // Получение объекта из справочника по идентификатору
        public DataObject GetObject(string id) {
            _dataObjects.TryGetValue(id.ToUpper(), out var obj);
            return obj;
        }
    }

    // Класс для обработки данных (добавление, удаление и очистка объектов в таблицах)
    public class DataProcessor {
        private Directory _directory;
        private Dictionary<string, DataObject> _inputData = new Dictionary<string, DataObject>();
        private Dictionary<string, DataObject> _outputData = new Dictionary<string, DataObject>();

        public DataProcessor(Directory directory) {
            _directory = directory;
        }

        // Добавление данных в одну из таблиц (в зависимости от флага isInputTable)
        public void AddToTable(string id, bool isInputTable) {
            var obj = _directory.GetObject(id) ?? new DataObject { Id = id.ToUpper(), Name = id.ToUpper(), Quantity = 1 };
            _directory.AddObject(obj);

            var table = isInputTable ? _inputData : _outputData;
            var oppositeTable = isInputTable ? _outputData : _inputData;

            // Если объект есть в противоположной таблице, уменьшаем его количество или удаляем
            if (oppositeTable.TryGetValue(id, out var oppositeObj)) {
                if (--oppositeObj.Quantity <= 0) oppositeTable.Remove(id);
            }

            // Добавление в текущую таблицу или увеличение количества, если уже существует
            if (table.TryGetValue(id, out var existingObj)) existingObj.Quantity++;
            else table.Add(id, new DataObject { Id = id, Name = obj.Name, Quantity = 1 });
        }

        // Очистка всех данных в таблицах
        public void ClearTables() {
            _inputData.Clear();
            _outputData.Clear();
        }

        public Dictionary<string, DataObject> GetInputData() => _inputData;
        public Dictionary<string, DataObject> GetOutputData() => _outputData;
    }
}

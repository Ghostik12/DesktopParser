using HtmlAgilityPack;
using OfficeOpenXml;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using RabotaRuParser.Data;
using RabotaRuParser.Models;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LicenseContext = OfficeOpenXml.LicenseContext;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace RabotaRuParser
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient httpClient;
        private readonly AppDbContext dbContext;
        private CancellationTokenSource cts; // Для остановки парсинга
        private CancellationTokenSource autoCts;
        private bool isAutoParsing = false;
        private int currentPage = 1;
        private int pageSize = 100; // Значение по умолчанию (соответствует SelectedIndex="1")
        private int totalItems = 0;
        private int totalPages = 1;
        private bool isInitialized = false; // Флаг инициализации
        private readonly Dictionary<string, string> cityToSubdomainMap;
        private bool onlyWithPhones = false; // Переменная для хранения состояния флага

        private bool _isVpnConnected;
        private string _currentVpnCountry = "Авто";

        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RabotaRuParser.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            dbContext = new AppDbContext(_dbOptions);
            dbContext.Database.EnsureCreated();
            // Инициализация маппинга городов
            cityToSubdomainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Москва", "msk" },
                { "Санкт-Петербург", "spb" },
                { "Нижний Новгород", "nn" },
                { "Екатеринбург", "eburg" },
                { "Казань", "kazan" },
                { "Новосибирск", "nsk" },
                { "Самара", "samara" },
                { "Челябинск", "chel" },
                { "Абакан", "abakan" },
                { "Анадырь", "anadyr" },
                { "Архангельск", "arkhangelsk" },
                { "Астрахань", "astrakhan" },
                { "Барнаул", "barnaul" },
                { "Белгород", "belgorod" },
                { "Биробиджан", "birobidjan" },
                { "Брянск", "bryansk" },
                { "Великий Новгород", "novgorod" },
                { "Владивосток", "vladivostok" },
                { "Владикавказ", "vladikavkaz" },
                { "Владимир", "vladimir" },
                { "Волгоград", "volgograd" },
                { "Воронеж", "voronezh" },
                { "Грозный", "grozniy" },
                { "Иваново", "ivanovo" },
                { "Ижевск", "izhevsk" },
                { "Иркутск", "irkutsk" },
                { "Йошкар-Ола", "yola" },
                { "Калининград", "kaliningrad" },
                { "Калуга", "kaluga" },
                { "Кемерово", "kemerovo" },
                { "Киров", "kirov" },
                { "Королев", "korolev" },
                { "Кострома", "kostroma" },
                { "Краснодар", "krasnodar" },
                { "Красноярск", "krasnoyarsk" },
                { "Курган", "kurgan" },
                { "Курск", "kursk" },
                { "Кызыл", "kyzyl" },
                { "Липецк", "lipetsk" },
                { "Магадан", "magadan" },
                { "Магнитогорск", "magnitogorsk" },
                { "Махачкала", "mahachkala" },
                { "Омск", "omsk" },
                { "Пермь", "perm" },
                { "Ростов-на-Дону", "rostov" },
            };
            LoadVpnSettings();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing; // Добавляем обработчик закрытия окна
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            isInitialized = true;

            pageSize = 100;
            LoadVacancies();

            if (dateToPicker != null)
            {
                dateToPicker.SelectedDate = DateTime.Today;
            }

            if (CityComboBox.Items.Count > 0)
            {
                CityComboBox.SelectedIndex = 0; // Выбираем первый город (например, "Москва")
            }

            // Загружаем сохранённые настройки
            onlyWithPhones = Properties.Settings.Default.OnlyWithPhones;
            OnlyWithPhonesCheckBox.IsChecked = onlyWithPhones;

            isAutoParsing = Properties.Settings.Default.AutoParseEnabled;
            if (autoParseCheckBox != null) // Если у тебя есть AutoParseCheckBox
            {
                autoParseCheckBox.IsChecked = isAutoParsing;
            }

            // Загружаем сохранённые значения для TextBox
            VacancyTitleTextBox.Text = Properties.Settings.Default.VacancyTitle;
            waitTimeTextBox.Text = Properties.Settings.Default.WaitTime;
            parseTimeTextBox.Text = Properties.Settings.Default.ParseTime;

            // Автоподключение VPN если было сохранено
            if (Properties.Settings.Default.VpnEnabled)
            {
                string countryCode = "US"; // Значение по умолчанию

                if (Properties.Settings.Default.VpnRotate)
                {
                    // Ротация - выбираем случайную страну
                    string[] countries = { "US", "NL", "JP" };
                    countryCode = countries[new Random().Next(0, countries.Length)];
                }
                else if (Properties.Settings.Default.VpnCountryIndex >= 0 &&
                         Properties.Settings.Default.VpnCountryIndex < vpnCountryComboBox.Items.Count)
                {
                    // Конкретная страна из сохраненных настроек
                    var selectedItem = vpnCountryComboBox.Items[Properties.Settings.Default.VpnCountryIndex] as ComboBoxItem;
                    countryCode = selectedItem?.Tag?.ToString() ?? "US";
                }

                await ConnectToWindscribe(countryCode);
            }
        }

        // Новый метод для загрузки настроек
        private void LoadVpnSettings()
        {
            vpnRotateAllCheckBox.IsChecked = Properties.Settings.Default.VpnRotate;
            vpnSelectCountryCheckBox.IsChecked = Properties.Settings.Default.VpnEnabled &&
                                               !Properties.Settings.Default.VpnRotate;
            vpnCountryComboBox.SelectedIndex = Properties.Settings.Default.VpnCountryIndex;

            // Принудительно обновляем состояние
            VpnRotateCheckBox_Changed(null, null);
            VpnCountryCheckBox_Changed(null, null);
        }

        // Обработчик выбора количества записей на странице
        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized) // Пропускаем вызов во время инициализации
            {
                return;
            }

            if (pageSizeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                pageSize = int.Parse(selectedItem.Content.ToString());
                currentPage = 1; // Сбрасываем на первую страницу
                LoadVacancies();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Сохраняем настройки при закрытии приложения
            Properties.Settings.Default.OnlyWithPhones = onlyWithPhones;
            Properties.Settings.Default.AutoParseEnabled = isAutoParsing;

            // Сохраняем значения TextBox
            Properties.Settings.Default.VacancyTitle = VacancyTitleTextBox.Text;
            Properties.Settings.Default.WaitTime = waitTimeTextBox.Text;
            Properties.Settings.Default.ParseTime = parseTimeTextBox.Text;

            SaveVpnSettings();

            Properties.Settings.Default.Save();

            // Отключаем VPN при закрытии
            if (_isVpnConnected)
            {
                DisconnectVpn().Wait(); // Синхронное ожидание отключения
            }

            dbContext?.Dispose();
            httpClient?.Dispose();
        }

        private void OnlyWithPhonesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            onlyWithPhones = true;
            Properties.Settings.Default.OnlyWithPhones = true;
            Properties.Settings.Default.Save();
            Dispatcher.Invoke(() => logTextBox.Text += "Фильтр 'Только с телефонами' включён.");
        }

        private void OnlyWithPhonesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            onlyWithPhones = false;
            Properties.Settings.Default.OnlyWithPhones = false;
            Properties.Settings.Default.Save();
            Dispatcher.Invoke(() => logTextBox.Text += "Фильтр 'Только с телефонами' выключён.");
        }

        // Если есть AutoParseCheckBox
        private void AutoParseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            isAutoParsing = true;
            Properties.Settings.Default.AutoParseEnabled = true;
            Properties.Settings.Default.Save();
            Dispatcher.Invoke(() => logTextBox.Text += "Автоматический парсинг включён.");
            StartAutoParsing();
        }

        private void AutoParseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            isAutoParsing = false;
            Properties.Settings.Default.AutoParseEnabled = false;
            Properties.Settings.Default.Save();
            Dispatcher.Invoke(() => logTextBox.Text += "Автоматический парсинг выключён.");
            if (autoCts != null)
            {
                autoCts.Cancel();
                autoCts.Dispose();
                autoCts = null;
            }
        }

        private void VacancyTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            Properties.Settings.Default.VacancyTitle = VacancyTitleTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void WaitTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.WaitTime = waitTimeTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void ParseTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.ParseTime = parseTimeTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void LoadVacancies()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var vacancies = dbContext.Vacancies.ToList();

                    // Фильтрация по датам
                    DateTime? dateFrom = dateFromPicker.SelectedDate;
                    DateTime? dateTo = dateToPicker.SelectedDate;

                    if (dateFrom.HasValue && dateTo.HasValue)
                    {
                        vacancies = vacancies.Where(v => v.ParseDate.Date >= dateFrom.Value.Date && v.ParseDate.Date <= dateTo.Value.Date).ToList();
                    }
                    else if (dateFrom.HasValue)
                    {
                        vacancies = vacancies.Where(v => v.ParseDate.Date >= dateFrom.Value.Date).ToList();
                    }
                    else if (dateTo.HasValue)
                    {
                        vacancies = vacancies.Where(v => v.ParseDate.Date <= dateTo.Value.Date).ToList();
                    }

                    // Обновляем общее количество записей
                    totalItems = vacancies.Count;
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                    totalPages = totalPages == 0 ? 1 : totalPages; // Минимум 1 страница
                    if (currentPage > totalPages) currentPage = totalPages;

                    // Загружаем записи для текущей страницы
                    var viewModels = vacancies
                        .Skip((currentPage - 1) * pageSize)
                        .Take(pageSize)
                        .Select((v, index) => new VacancyViewModel(v, (currentPage - 1) * pageSize + index + 1))
                        .ToList();

                    var collectionView = CollectionViewSource.GetDefaultView(viewModels);
                    //collectionView.SortDescriptions.Clear();
                    //collectionView.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending));

                    dataGrid.ItemsSource = collectionView;
                    AdjustColumnWidths(dataGrid);

                    // Обновляем UI
                    currentPageTextBox.Text = currentPage.ToString();
                    totalPagesLabel.Content = $"{totalPages}";
                    recordsCountLabel.Content = totalItems.ToString();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"1111 {ex.InnerException}");
            }
        }

        private void AdjustColumnWidths(DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                // Устанавливаем ширину на Auto, чтобы изначально подогнать под содержимое
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                // Принудительно обновляем макет, чтобы получить реальную ширину
                dataGrid.UpdateLayout();

                // Получаем текущую ширину (по содержимому)
                double maxWidth = column.ActualWidth;

                // Добавляем небольшой отступ (например, 20 пикселей) для читаемости
                column.Width = new DataGridLength(maxWidth + 20);
            }
        }

        private void CurrentPageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    // Пробуем распарсить введённое значение
                    if (!int.TryParse(currentPageTextBox.Text, out int newPage))
                    {
                        MessageBox.Show("Пожалуйста, введите корректное число.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        currentPageTextBox.Text = currentPage.ToString(); // Возвращаем предыдущее значение
                        return;
                    }

                    // Вычисляем общее количество страниц
                    var totalRecords = dbContext.Vacancies.Count();
                    var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                    // Проверяем, что введённое число в допустимом диапазоне
                    if (newPage < 1 || newPage > totalPages)
                    {
                        MessageBox.Show($"Пожалуйста, введите число от 1 до {totalPages}.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        currentPageTextBox.Text = currentPage.ToString(); // Возвращаем предыдущее значение
                        return;
                    }

                    // Если валидация пройдена, переходим на новую страницу
                    currentPage = newPage;
                    LoadVacancies();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при переходе на страницу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    currentPageTextBox.Text = currentPage.ToString(); // Возвращаем предыдущее значение
                }
            }
        }

        private void CurrentPageTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _); // Разрешаем ввод только цифр
        }

        // Обработчики пагинации
        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            LoadVacancies();
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                LoadVacancies();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                LoadVacancies();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage = totalPages;
            LoadVacancies();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadVacancies();
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckStartButtonEnabled();
        }

        private void CheckStartButtonEnabled()
        {
            Dispatcher.Invoke(() =>
            {
                var vacancyTextBox = VacancyTitleTextBox;
                if (string.IsNullOrWhiteSpace(vacancyTextBox.Text) || CityComboBox.SelectedItem == null)
                {
                    vacancyTextBox.BorderBrush = string.IsNullOrWhiteSpace(vacancyTextBox.Text) ? Brushes.Red : Brushes.Gray;
                    startButton.IsEnabled = false;
                }
                else
                {
                    vacancyTextBox.BorderBrush = Brushes.Gray;
                    startButton.IsEnabled = true;
                }
            });
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Останавливаем предыдущий парсинг, если он идёт
            if (cts != null)
            {
                cts.Cancel();
                Dispatcher.Invoke(() =>
                {
                    stopButton.IsEnabled = false;
                    logTextBox.Text += "Предыдущий парсинг остановлен.\n";
                });
                // Ждём завершения предыдущего парсинга
                await Task.Delay(1000); // Даём время на завершение
            }

            string searchKeyword = string.Empty;
            string city = string.Empty;
            Dispatcher.Invoke(() =>
            {
                startButton.IsEnabled = false;
                stopButton.IsEnabled = true; // Активируем "Стоп"
                logTextBox.Text = "Парсинг начался...\n";
                searchKeyword = VacancyTitleTextBox.Text.Trim();
                city = CityComboBox?.SelectedItem is ComboBoxItem selectedItem ? selectedItem.Content?.ToString() : string.Empty;
            });

            cts = new CancellationTokenSource(); // Создаём токен отмены
            try
            {
                await ParseAllPages(searchKeyword, city, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => logTextBox.Text += "Парсинг остановлен пользователем.\n");
            }
            finally
            {

            }

            Dispatcher.Invoke(() =>
            {
                logTextBox.Text += "Парсинг завершён.\n";
                LoadVacancies();
                startButton.IsEnabled = true;
                stopButton.IsEnabled = false; // Деактивируем "Стоп"
            });
            cts.Dispose();
            cts = null;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                Dispatcher.Invoke(() =>
                {
                    stopButton.IsEnabled = false;
                    logTextBox.Text += "Остановка парсинга инициирована...\n";
                });
            }
        }

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = false;

            Dispatcher.InvokeAsync(() =>
            {
                var view = dataGrid.ItemsSource as ICollectionView;
                var items = view?.Cast<VacancyViewModel>().ToList();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        items[i].RowNumber = i + 1;
                    }
                    dataGrid.Items.Refresh();
                }
            }, DispatcherPriority.Background);
        }

        private object GetPropertyValue(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
        }

        private async void StartAutoParsing()
        {
            autoCts = new CancellationTokenSource();
            try
            {
                while (isAutoParsing)
                {
                    autoCts.Token.ThrowIfCancellationRequested();

                    string parseTimeRange = parseTimeTextBox.Text;
                    var times = parseTimeRange.Split(',');
                    if (times.Length != 2 ||
                        !TimeSpan.TryParseExact(times[0].Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan startTime) ||
                        !TimeSpan.TryParseExact(times[1].Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan endTime))
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Неверный формат времени. Используйте ЧЧ:ММ,ЧЧ:ММ (например, 14:00,23:00).\n");
                        break;
                    }

                    if (startTime >= endTime)
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Начальное время должно быть меньше конечного.\n");
                        break;
                    }

                    DateTime now = DateTime.Now;
                    Random random = new Random();
                    TimeSpan randomTimeSpan = startTime + TimeSpan.FromMinutes(random.Next((int)(endTime - startTime).TotalMinutes));
                    DateTime nextRun = now.Date.Add(randomTimeSpan);
                    if (nextRun < now)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    TimeSpan delay = nextRun - now;
                    Dispatcher.Invoke(() => logTextBox.Text += $"Следующий автоматический парсинг в {nextRun:dd.MM.yyyy HH:mm}.\n");
                    await Task.Delay(delay, autoCts.Token);

                    if (!isAutoParsing) break;

                    string searchKeyword = string.Empty;
                    string city = string.Empty;
                    Dispatcher.Invoke(() =>
                    {
                        startButton.IsEnabled = false;
                        stopButton.IsEnabled = true;
                        logTextBox.Text += $"Автоматический парсинг начался в {DateTime.Now:dd.MM.yyyy HH:mm}...\n";
                        searchKeyword = VacancyTitleTextBox.Text.Trim();
                        city = CityComboBox?.SelectedItem is ComboBoxItem selectedItem ? selectedItem.Content?.ToString() : string.Empty;
                    });

                    cts = new CancellationTokenSource();
                    try
                    {
                        await Task.Run(() => ParseAllPages(searchKeyword, city, cts.Token));
                    }
                    catch (OperationCanceledException)
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Автоматический парсинг остановлен.\n");
                    }
                    finally
                    {

                    }

                    Dispatcher.Invoke(() =>
                    {
                        logTextBox.Text += "Автоматический парсинг завершён.\n";
                        LoadVacancies();
                        startButton.IsEnabled = true;
                        stopButton.IsEnabled = false;
                    });
                    cts.Dispose();
                    cts = null;
                }
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => logTextBox.Text += "Автоматический парсинг отключён.\n");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var view = dataGrid.ItemsSource as ICollectionView;
            var viewModels = view?.Cast<VacancyViewModel>().ToList();
            if (viewModels == null || !viewModels.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DateTime? dateFrom = dateFromPicker.SelectedDate;
            DateTime? dateTo = dateToPicker.SelectedDate;

            if (dateFrom.HasValue && dateTo.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.ParseDate.Date >= dateFrom.Value.Date && vm.ParseDate.Date <= dateTo.Value.Date).ToList();
            }
            else if (dateFrom.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.ParseDate.Date >= dateFrom.Value.Date).ToList();
            }
            else if (dateTo.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.ParseDate.Date <= dateTo.Value.Date).ToList();
            }

            if (!viewModels.Any())
            {
                MessageBox.Show("Нет данных для экспорта в указанном диапазоне дат.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Вакансии");
                worksheet.Cells[1, 1].Value = "№п/п";
                worksheet.Cells[1, 2].Value = "Дата парсинга";
                worksheet.Cells[1, 3].Value = "Дата на сайте";
                worksheet.Cells[1, 4].Value = "ID на сайте";
                worksheet.Cells[1, 5].Value = "Ссылка";
                worksheet.Cells[1, 6].Value = "Название сайта";
                worksheet.Cells[1, 7].Value = "Телефон";
                worksheet.Cells[1, 8].Value = "Вакансия";
                worksheet.Cells[1, 9].Value = "Адрес";
                worksheet.Cells[1, 10].Value = "Компания";
                worksheet.Cells[1, 11].Value = "ФИО";

                for (int i = 0; i < viewModels.Count; i++)
                {
                    var vm = viewModels[i];
                    worksheet.Cells[i + 2, 1].Value = vm.RowNumber;
                    worksheet.Cells[i + 2, 2].Value = vm.ParseDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[i + 2, 3].Value = vm.Date.ToString("dd.MM.yyyy");
                    worksheet.Cells[i + 2, 4].Value = vm.SiteId;
                    worksheet.Cells[i + 2, 5].Value = vm.Link;
                    worksheet.Cells[i + 2, 6].Value = vm.Domain;
                    worksheet.Cells[i + 2, 7].Value = vm.Phone;
                    worksheet.Cells[i + 2, 8].Value = vm.Title;
                    worksheet.Cells[i + 2, 9].Value = vm.Address;
                    worksheet.Cells[i + 2, 10].Value = vm.Company;
                    worksheet.Cells[i + 2, 11].Value = vm.ContactName;
                }

                worksheet.Cells.AutoFitColumns();

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    DefaultExt = "xlsx",
                    FileName = $"Vacancies_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, package.GetAsByteArray());
                    MessageBox.Show($"Файл успешно сохранён: {saveFileDialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async Task ParseAllPages(string searchKeyword, string city, CancellationToken token)
        {
            try
            {
                int waitTime = int.TryParse(waitTimeTextBox.Text, out int seconds) ? seconds : 5;

                // Перед парсингом
                if (vpnRotateAllCheckBox.IsChecked == true)
                {
                    string[] countries = { "US", "NL", "JP" };
                    await DisconnectWindscribe();
                    await ConnectToWindscribe(countries[new Random().Next(0, 3)]);
                }
                else if (vpnSelectCountryCheckBox.IsChecked == true)
                {
                    string country = (vpnCountryComboBox.SelectedItem as ComboBoxItem)?.Tag.ToString();
                    await ConnectToWindscribe(country);
                }

                // Определяем поддомен на основе выбранного города
                string subdomain = cityToSubdomainMap[city]; // Теперь мы уверены, что город есть в словаре
                string baseUrl = $"https://{subdomain}.rabota.ru";
                if (subdomain == "msk")
                {
                    baseUrl = $"https://rabota.ru";
                }
                string searchUrl = $"{baseUrl}/vacancy/?query={Uri.EscapeDataString(searchKeyword)}&sort=relevance";
                int currentPage = 1;

                while (true)
                {
                    token.ThrowIfCancellationRequested(); // Проверяем отмену

                    Dispatcher.Invoke(() => logTextBox.Text += $"Обрабатываем страницу {currentPage}: {searchUrl}\n");
                    var htmlDoc = await LoadPageWithAgility(searchUrl); // Синхронно в фоновом потоке
                    if (htmlDoc == null)
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Ошибка загрузки страницы поиска. Пауза 5 секунд.\n");
                        await Task.Delay(5000);
                        ParseWithSelenium(searchUrl);
                        return;
                    }

                    Random random = new Random();
                    var vacancyNodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@class, 'vacancy-preview-card__title_border')]");
                    if (vacancyNodes == null || !vacancyNodes.Any())
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Вакансии не найдены или страницы закончились.\n");
                        break;
                    }

                    Dispatcher.Invoke(() => logTextBox.Text += $"Найдено вакансий: {vacancyNodes.Count}\n");
                    foreach (var node in vacancyNodes)
                    {
                        token.ThrowIfCancellationRequested(); // Проверяем отмену перед каждой вакансией

                        // 1. Исправленный блок ротации VPN в ParseAllPages
                        if (vpnRotateAllCheckBox.IsChecked == true)
                        {
                            string[] countries = { "US", "NL", "JP" };
                            string randomCountry = countries[new Random().Next(0, 3)];
                            await DisconnectWindscribe();
                            await Task.Delay(waitTime * 1000 / 2);
                            await ConnectToWindscribe(randomCountry);
                            await Task.Delay(waitTime * 1000 / 2);
                        }
                        else if (vpnSelectCountryCheckBox.IsChecked == true)
                        {
                            string countryCode = (vpnCountryComboBox.SelectedItem as ComboBoxItem)?.Tag.ToString();
                            await ConnectToWindscribe(countryCode);
                            await Task.Delay(waitTime * 1000);
                        }
                        else
                        {
                            await Task.Delay(waitTime * 1000); // Обычное ожидание без VPN
                        }

                        var href = node.GetAttributeValue("href", "");
                        var vacancy = new Vacancy
                        {
                            Link = baseUrl + href,
                            Domain = new Uri(baseUrl).Host,
                            ParseDate = DateTime.UtcNow // Устанавливаем дату парсинга
                        };

                        vacancy.Title = node.InnerText.Trim();

                        var hrefParts = vacancy.Link.Split('/');
                        var vacancyIndex = Array.IndexOf(hrefParts, "vacancy");
                        if (vacancyIndex != -1 && vacancyIndex + 1 < hrefParts.Length)
                        {
                            var potentialId = hrefParts[vacancyIndex + 1].Split('?')[0];
                            vacancy.SiteId = potentialId.All(char.IsDigit) ? potentialId : null;
                        }

                        Dispatcher.Invoke(() => logTextBox.Text += $"Загружаем вакансию с ID: {vacancy.SiteId}\n");

                        var addressNode = node.SelectSingleNode("following::span[@class='vacancy-preview-location__address-text']");
                        vacancy.Address = addressNode?.InnerText.Trim();

                        var companyNode = node.SelectSingleNode("following::a[@itemprop='name']");
                        vacancy.Company = companyNode?.InnerText.Trim()
                            .Replace("\"", "")
                            .Replace("(", "")
                            .Replace(")", "")
                            .Replace(" - ", " ")
                            .Replace(",", "")
                            .Replace("  ", " ")
                            .Replace(@"&nbsp;|\s+", " ")
                            .Replace(@"&quot;|\s+", " ")
                            .Replace(@"&quot;", " ")
                            .Replace(@"&nbsp;", " ");

                        string firstWord = vacancy.Title?.Split(' ')[0];
                        vacancy.ShortTitle = firstWord?.Length > 3 ? firstWord.Substring(0, firstWord.Length - 3) : firstWord;

                        var (phone, contactName, date, address) = ParseDetailsWithSelenium(vacancy.Link, vacancy.Address).GetAwaiter().GetResult();
                        if (phone == null && contactName == null && date == null)
                        {
                            Dispatcher.Invoke(() => logTextBox.Text += $"Ошибка загрузки данных для ID {vacancy.SiteId}. Пауза 5 секунд.\n");
                            await Task.Delay(5000);
                            continue;
                        }

                        vacancy.Phone = phone;
                        vacancy.ContactName = contactName;
                        vacancy.Date = date ?? DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
                        vacancy.Address = address; // Обновляем адрес, если он был взят из Selenium

                        // Проверяем, установлен ли флаг "Только с телефонами"
                        if (onlyWithPhones && string.IsNullOrEmpty(vacancy.Phone))
                        {
                            Dispatcher.Invoke(() => logTextBox.Text += $"Вакансия ID {vacancy.SiteId} пропущена: отсутствует телефон.\n");
                            continue; // Пропускаем вакансию, если телефон отсутствует
                        }
                        if (vacancy.Phone == null)
                            vacancy.Phone = "Нет телефона";

                        if (!IsDuplicate(vacancy))
                        {
                            try
                            {
                                await dbContext.Vacancies.AddAsync(vacancy);
                                await dbContext.SaveChangesAsync();

                                Dispatcher.Invoke(() =>
                                    logTextBox.Text += $"Вакансия ID {vacancy.SiteId} добавлена.\n");
                            }
                            catch (DbUpdateException ex)
                            {
                                Dispatcher.Invoke(() =>
                                    logTextBox.Text += $"Ошибка сохранения (SQLite): {ex.InnerException?.Message}\n");
                                await Task.Delay(3000);
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() =>
                                    logTextBox.Text += $"Общая ошибка: {ex.Message}\n");
                                await Task.Delay(3000);
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                                logTextBox.Text += $"Дубликат вакансии: {vacancy.Title}\n");
                        }

                        int waitTimeSeconds = int.TryParse(waitTimeTextBox.Text, out int second) ? second : 5;
                        Dispatcher.Invoke(() => logTextBox.Text += $"Ожидание {waitTimeSeconds} секунд перед следующей вакансией...\n");
                        await Task.Delay(waitTimeSeconds * 1000, token);
                    }

                    // Пагинация
                    var nextPageLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@class, 'pagination-list__item')]");
                    if (nextPageLinks != null && nextPageLinks.Any())
                    {
                        var nextPageLink = nextPageLinks.FirstOrDefault(link =>
                            int.TryParse(link.InnerText.Trim(), out int pageNum) && pageNum == currentPage + 1);
                        if (nextPageLink != null)
                        {
                            searchUrl = baseUrl + nextPageLink.GetAttributeValue("href", "");
                            currentPage++;
                            Dispatcher.Invoke(() => logTextBox.Text += $"Переход на следующую страницу ({currentPage}): {searchUrl}\n");
                        }
                        else
                        {
                            Dispatcher.Invoke(() => logTextBox.Text += "Следующей страницы не найдено. Парсинг завершён.\n");
                            break;
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Ссылки на следующую страницу не найдены. Парсинг завершён.\n");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => logTextBox.Text += $"Критическая ошибка: {ex.Message}\n");
                await Task.Delay(5000);
            }
            finally
            {
                if (vpnRotateAllCheckBox.IsChecked == true || vpnSelectCountryCheckBox.IsChecked == true)
                {
                    await DisconnectWindscribe();
                }
            }
        }

        private async Task<(string, string, DateTime?, string)> ParseDetailsWithSelenium(string url, string existingAddress)
        {
            IWebDriver driver = null;
            try
            {
                try
                {
                    if (driver == null)
                    {
                        var chromeOptions = new ChromeOptions();
                        chromeOptions.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
                        chromeOptions.AddArgument("--headless"); // Запуск в безголовом режиме
                        chromeOptions.AddArgument("--disable-gpu"); // Отключаем GPU для стабильности в headless
                        chromeOptions.AddArgument("--no-sandbox"); // Упрощаем запуск в некоторых средах
                        var service = ChromeDriverService.CreateDefaultService();
                        service.HideCommandPromptWindow = true; // Скрываем консоль
                        driver = new ChromeDriver(service, chromeOptions);
                    }

                    Console.WriteLine($"Открываем карточку: {url}");
                    driver.Navigate().GoToUrl(url);
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

                    // Телефон
                    string phone = null;
                    try
                    {
                        // Шаг 1: Проверяем наличие кнопки "Показать телефон"
                        Console.WriteLine("Ищем кнопку 'Показать телефон'...");
                        var showPhoneLink = wait.Until(d =>
                        {
                            try
                            {
                                var element = d.FindElement(By.CssSelector(".vacancy-response__phones-show-link, [data-qa='vacancy-contacts__phone-show']"));
                                Console.WriteLine("Кнопка найдена: " + element.Text);
                                return element.Displayed && element.Enabled ? element : null;
                            }
                            catch (NoSuchElementException)
                            {
                                Console.WriteLine("Кнопка 'Показать телефон' не найдена!");
                                return null;
                            }
                        });

                        if (showPhoneLink == null)
                        {
                            Console.WriteLine("Не удалось найти кнопку. Проверяем исходный HTML...");
                            Console.WriteLine(driver.PageSource.Substring(0, Math.Min(500, driver.PageSource.Length))); // Первые 500 символов
                        }

                        // Шаг 2: Клик по кнопке
                        Console.WriteLine("Кликаем на кнопку...");
                        showPhoneLink.Click();

                        // Шаг 3: Ждём телефон
                        Console.WriteLine("Ищем телефон...");
                        var phoneElement = wait.Until(d =>
                        {
                            try
                            {
                                var element = d.FindElement(By.CssSelector(".vacancy-response__phone span"));
                                Console.WriteLine("Телефон найден: " + element.Text);
                                return element.Displayed ? element : null;
                            }
                            catch (NoSuchElementException)
                            {
                                Console.WriteLine("Телефон не найден после клика!");
                                return null;
                            }
                        });
                        phone = phoneElement.Text.Trim();

                        if (phoneElement == null)
                        {
                            Console.WriteLine("Телефон не появился. Проверяем HTML после клика...");
                            Console.WriteLine(driver.PageSource.Substring(0, Math.Min(500, driver.PageSource.Length)));
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Телефон не найден.");
                    }


                    // ФИО
                    string contactName = null;
                    try
                    {
                        var contactElement = driver.FindElement(By.CssSelector(".vacancy-response__name, [data-qa='vacancy-contacts__fio']"));
                        contactName = contactElement.Text.Trim();
                    }
                    catch
                    {
                        Console.WriteLine("ФИО не найдено.");
                    }

                    // Дата
                    DateTime? date = null;
                    try
                    {
                        var dateElement = driver.FindElement(By.XPath("//span[contains(text(), 'января') or contains(text(), 'февраля') or contains(text(), 'марта') or contains(text(), 'апреля') or contains(text(), 'мая') or contains(text(), 'июня') or contains(text(), 'июля') or contains(text(), 'августа') or contains(text(), 'сентября') or contains(text(), 'октября') or contains(text(), 'ноября') or contains(text(), 'декабря')]"));
                        string dateText = dateElement.Text.Trim(); // "20 марта 2025"
                        date = DateTime.Parse(dateText, new CultureInfo("ru-RU")); // Парсим с русской локализацией
                        date = DateTime.SpecifyKind(date.Value, DateTimeKind.Utc); // Указываем UTC
                    }
                    catch
                    {
                        Console.WriteLine("Дата не найдена или не распарсилась.");
                        try
                        {
                            // Пробуем JSON-LD как запасной вариант
                            var jsonLdNode = driver.FindElement(By.XPath("//script[@type='application/ld+json']"));
                            var jsonText = jsonLdNode.GetAttribute("innerHTML");
                            var jsonArray = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(jsonText);
                            var jobData = jsonArray.FirstOrDefault();
                            if (jobData != null && jobData.TryGetValue("datePosted", out dynamic datePosted))
                            {
                                date = DateTime.SpecifyKind(DateTime.Parse(datePosted.ToString()), DateTimeKind.Utc);
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Дата не найдена и в JSON-LD.");
                        }
                    }

                    string address = existingAddress; // Используем существующий адрес, если он есть
                    if (string.IsNullOrEmpty(address))
                    {
                        try
                        {
                            var addressElement = driver.FindElement(By.CssSelector(".vacancy-locations__address, [data-qa='vacancy-address']"));
                            address = addressElement.Text.Trim();
                        }
                        catch
                        {
                            Console.WriteLine("Адрес не найден на странице вакансии.");
                        }
                    }

                    return (phone, contactName, date, address);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в Selenium: {ex.Message}");
                    return (null, null, null, null);
                }
            }
            finally
            {
                if (driver != null)
                {
                    try
                    {
                        driver.Quit();
                        driver.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += $"Ошибка при закрытии WebDriver: {ex.Message}");
                    }
                }
            }
        }

        private async Task<HtmlDocument> LoadPageWithAgility(string url)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Бросает исключение при ошибке HTTP
                var html = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                return htmlDoc;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Ошибка HTTP-запроса к {url}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки страницы {url}: {ex.Message}");
                return null;
            }
        }

        private HashSet<string> blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void ParseWithSelenium(string searchUrl)
        {
            // Логика парсинга через Selenium как в предыдущем варианте
            // Добавь сюда старый метод ParseAllPages, если нужно
        }

        private bool IsDuplicate(Vacancy newVacancy)
        {
            // Приводим Date к UTC, если оно не null
            var allVacancies = dbContext.Vacancies
        .AsNoTracking()
        .AsEnumerable() // Переносим обработку на клиент
        .ToList();

            var newVacancyDateUtc = newVacancy.Date.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(newVacancy.Date, DateTimeKind.Utc)
                : newVacancy.Date.ToUniversalTime();

            return allVacancies.Any(v =>
                (v.Company == newVacancy.Company &&
                 v.ShortTitle == newVacancy.ShortTitle &&
                 Math.Abs((v.Date - newVacancyDateUtc).Days) <= 40) ||
                (v.Phone == newVacancy.Phone &&
                 v.Title.StartsWith(newVacancy.Title?.Split(' ')[0] ?? "") &&
                 Math.Abs((v.Date - newVacancyDateUtc).Days) <= 40));
        }

        private DateTime ParseDate(string dateText)
        {
            if (string.IsNullOrEmpty(dateText)) return DateTime.Today;
            if (dateText.Contains("сегодня")) return DateTime.Today;
            if (dateText.Contains("вчера")) return DateTime.Today.AddDays(-1);
            return DateTime.TryParse(dateText, out var date) ? date : DateTime.Today;
        }

        protected override void OnClosed(EventArgs e)
        {
            //driver?.Quit();
            dbContext?.Dispose();
            httpClient?.Dispose();
            base.OnClosed(e);
        }

        // Обновленные обработчики для новых чекбоксов
        private void VpnRotateCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            vpnSelectCountryCheckBox.IsEnabled = !vpnRotateAllCheckBox.IsChecked == true;
            vpnCountryComboBox.IsEnabled = vpnSelectCountryCheckBox.IsChecked == true &&
                                         !vpnRotateAllCheckBox.IsChecked == true;
            SaveVpnSettings();
        }

        private void VpnCountryCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            vpnCountryComboBox.IsEnabled = vpnSelectCountryCheckBox.IsChecked == true;
            testVpnButton.IsEnabled = vpnSelectCountryCheckBox.IsChecked == true ||
                                     vpnRotateAllCheckBox.IsChecked == true;
            SaveVpnSettings();
        }

        // Новый метод для сохранения настроек VPN
        private void SaveVpnSettings()
        {
            Properties.Settings.Default.VpnEnabled = vpnRotateAllCheckBox.IsChecked == true ||
                                                  vpnSelectCountryCheckBox.IsChecked == true;
            Properties.Settings.Default.VpnRotate = vpnRotateAllCheckBox.IsChecked == true;
            Properties.Settings.Default.VpnCountryIndex = vpnCountryComboBox.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        // 3. Исправленный тест VPN
        private async void TestVpnButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                testVpnButton.IsEnabled = false;
                logTextBox.Text += "Проверка VPN соединения...\n";

                string countryCode;
                if (vpnRotateAllCheckBox.IsChecked == true)
                {
                    string[] countries = { "US", "NL", "JP" };
                    countryCode = countries[new Random().Next(0, 3)];
                }
                else
                {
                    countryCode = (vpnCountryComboBox.SelectedItem as ComboBoxItem)?.Tag.ToString();
                }

                await ConnectToWindscribe(countryCode);

                using var client = new HttpClient();
                var response = await client.GetStringAsync("https://api.ipify.org");
                logTextBox.Text += $"Текущий IP: {response}\n";
                logTextBox.Text += "VPN соединение успешно проверено.\n";
            }
            catch (Exception ex)
            {
                logTextBox.Text += $"Ошибка проверки VPN: {ex.Message}\n";
                //UpdateVpnStatus("Ошибка подключения", Brushes.Red);
            }
            finally
            {
                testVpnButton.IsEnabled = true;
                if (vpnRotateAllCheckBox.IsChecked == true || vpnSelectCountryCheckBox.IsChecked == true)
                {
                    await DisconnectWindscribe();
                }
            }
        }

        // Основные методы VPN
        private async Task ConnectToWindscribe(string countryCode)
        {
            try
            {
                // Полный путь к Windscribe CLI (обычно здесь)
                string windscribePath = @"C:\Program Files\Windscribe\Windscribe.exe";

                var psi = new ProcessStartInfo
                {
                    FileName = windscribePath,
                    Arguments = $"connect {countryCode}", // Например: "connect US"
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var process = Process.Start(psi))
                {
                    await Task.Delay(15000); // Windscribe медленный, ждем 15 сек
                }

                logTextBox.Text += $"Подключено: {countryCode}\n";
            }
            catch (Exception ex)
            {
                logTextBox.Text += $"Ошибка: {ex.Message}\n";
            }
        }

        private async Task DisconnectWindscribe()
        {
            Process.Start("cmd.exe", "/C windscribe disconnect").WaitForExit();
            await Task.Delay(3000);
            logTextBox.Text += "Windscribe отключен\n";
        }

        private async Task DisconnectVpn()
        {
            try
            {
                logTextBox.Text += "Отключение VPN...\n";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C \"C:\\Program Files\\OpenVPN\\bin\\openvpn-gui.exe\" --disconnect",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await Task.Delay(3000);
                }

                _isVpnConnected = false;
                //UpdateVpnStatus("Не подключен", Brushes.Gray);
                logTextBox.Text += "VPN отключен.\n";
            }
            catch (Exception ex)
            {
                //UpdateVpnStatus("Ошибка отключения", Brushes.Red);
                logTextBox.Text += $"Ошибка отключения VPN: {ex.Message}\n";
                throw;
            }
        }

        private void LoadBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt",
                Title = "Выберите чёрный список"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    blacklist = new HashSet<string>(lines.Where(line => !string.IsNullOrWhiteSpace(line)));
                    logTextBox.Text += $"Загружен чёрный список: {blacklist.Count} записей\n";
                }
                catch (Exception ex)
                {
                    logTextBox.Text += $"Ошибка загрузки: {ex.Message}\n";
                }
            }
        }
    }
}
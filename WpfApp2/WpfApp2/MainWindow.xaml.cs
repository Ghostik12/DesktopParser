using HtmlAgilityPack;
using OfficeOpenXml;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp2.Data;
using WpfApp2.Models;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace WpfApp2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient httpClient;
        private IWebDriver driver;
        private readonly AppDbContext dbContext;
        private CancellationTokenSource cts; // Для остановки парсинга
        private CancellationTokenSource autoCts;
        private bool isAutoParsing;
        private int currentPage = 1;
        private int pageSize = 100; // Значение по умолчанию (соответствует SelectedIndex="1")
        private int totalItems = 0;
        private int totalPages = 1;

        public MainWindow()
        {
            dbContext = new AppDbContext();
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
            LoadBlacklist();
            LoadVacancies();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            dateToPicker.SelectedDate = DateTime.Today;
        }

        // Обработчик выбора количества записей на странице
        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pageSizeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                pageSize = int.Parse(selectedItem.Content.ToString());
                currentPage = 1; // Сбрасываем на первую страницу
                LoadVacancies();
            }
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

        private void VacancyTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckStartButtonEnabled();
        }

        private void CityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckStartButtonEnabled();
        }

        private void CheckStartButtonEnabled()
        {
            Dispatcher.Invoke(() =>
            {
                var vacancyTextBox = VacancyTitleTextBox;
                var cityTextBox = CityTextBox;
                if (string.IsNullOrWhiteSpace(vacancyTextBox.Text) || string.IsNullOrWhiteSpace(cityTextBox.Text))
                {
                    vacancyTextBox.BorderBrush = string.IsNullOrWhiteSpace(vacancyTextBox.Text) ? Brushes.Red : Brushes.Gray;
                    cityTextBox.BorderBrush = string.IsNullOrWhiteSpace(cityTextBox.Text) ? Brushes.Red : Brushes.Gray;
                    startButton.IsEnabled = false;
                }
                else
                {
                    vacancyTextBox.BorderBrush = Brushes.Gray;
                    cityTextBox.BorderBrush = Brushes.Gray;
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
                city = CityTextBox.Text.Trim();
            });

            cts = new CancellationTokenSource(); // Создаём токен отмены
            try
            {
                await Task.Run(() => ParseAllPages(searchKeyword, city, cts.Token));
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => logTextBox.Text += "Парсинг остановлен пользователем.\n");
            }
            finally
            {
                if (driver != null)
                {
                    driver.Quit(); // Закрываем браузер
                    driver = null; // Сбрасываем ссылку
                    Dispatcher.Invoke(() => logTextBox.Text += "Selenium-браузер закрыт.\n");
                }
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

        private void AutoParseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            parseTimeTextBox.IsEnabled = true;
            isAutoParsing = true;
            StartAutoParsing();
        }

        private void AutoParseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            parseTimeTextBox.IsEnabled = false;
            isAutoParsing = false;
            if (autoCts != null)
            {
                autoCts.Cancel();
                autoCts.Dispose();
                autoCts = null;
            }
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
                        city = CityTextBox.Text.Trim();
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
                        if (driver != null)
                        {
                            driver.Quit();
                            driver = null;
                            Dispatcher.Invoke(() => logTextBox.Text += "Selenium-браузер закрыт.\n");
                        }
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

        private async void ParseAllPages(string searchKeyword, string city, CancellationToken token)
        {
            try
            {
                string baseUrl = "https://nn.rabota.ru";
                string searchUrl = $"{baseUrl}/vacancy/?query={Uri.EscapeDataString(searchKeyword)}&location.name={Uri.EscapeDataString(city)}&sort=relevance";
                int currentPage = 1;

                while (true)
                {
                    token.ThrowIfCancellationRequested(); // Проверяем отмену

                    Dispatcher.Invoke(() => logTextBox.Text += $"Обрабатываем страницу {currentPage}: {searchUrl}\n");
                    var htmlDoc = LoadPageWithAgility(searchUrl).GetAwaiter().GetResult(); // Синхронно в фоновом потоке
                    if (htmlDoc == null)
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Ошибка загрузки страницы поиска. Пауза 5 секунд.\n");
                        Task.Delay(5000).GetAwaiter().GetResult();
                        ParseWithSelenium(searchUrl);
                        return;
                    }

                    Random random = new Random();
                    var vacancyNodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@class, 'vacancy-preview-card__title_border')]");
                    if (vacancyNodes == null || !vacancyNodes.Any())
                    {
                        Dispatcher.Invoke(() => logTextBox.Text += "Вакансии не найдены или страницы закончились.\n");
                        return;
                    }

                    Dispatcher.Invoke(() => logTextBox.Text += $"Найдено вакансий: {vacancyNodes.Count}\n");
                    foreach (var node in vacancyNodes)
                    {
                        token.ThrowIfCancellationRequested(); // Проверяем отмену перед каждой вакансией

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
                            Task.Delay(5000).GetAwaiter().GetResult();
                            continue;
                        }

                        vacancy.Phone = phone;
                        vacancy.ContactName = contactName;
                        vacancy.Date = date ?? DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
                        vacancy.Address = address; // Обновляем адрес, если он был взят из Selenium

                        if (!IsDuplicate(vacancy))
                        {
                            try
                            {
                                dbContext.Vacancies.Add(vacancy);
                                await dbContext.SaveChangesAsync(); // Синхронно в фоновом потоке
                                Dispatcher.Invoke(() => logTextBox.Text += $"Вакансия ID {vacancy.SiteId} добавлена в базу.\n");
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => logTextBox.Text += $"Ошибка сохранения ID {vacancy.SiteId}: {ex.Message}. Пауза 3 секунды.\n");
                                Task.Delay(3000).GetAwaiter().GetResult();
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() => logTextBox.Text += $"Вакансия ID {vacancy.SiteId} уже существует.\n");
                        }

                        int waitTimeSeconds = int.TryParse(waitTimeTextBox.Text, out int seconds) ? seconds : 5;
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
                Task.Delay(5000).GetAwaiter().GetResult();
            }
        }

        private async Task<(string, string, DateTime?, string)> ParseDetailsWithSelenium(string url, string existingAddress)
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

        private void LoadBlacklist()
        {
            try
            {
                if (File.Exists("blacklist.txt"))
                {
                    var lines = File.ReadAllLines("blacklist.txt");
                    blacklist = new HashSet<string>(lines.Select(line => line.Trim()), StringComparer.OrdinalIgnoreCase);
                    Dispatcher.Invoke(() => logTextBox.Text += $"Загружен черный список: {blacklist.Count} записей.\n");
                }
                else
                {
                    Dispatcher.Invoke(() => logTextBox.Text += "Файл blacklist.txt не найден.\n");
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => logTextBox.Text += $"Ошибка загрузки черного списка: {ex.Message}\n");
            }
        }

        private void ParseWithSelenium(string searchUrl)
        {
            // Логика парсинга через Selenium как в предыдущем варианте
            // Добавь сюда старый метод ParseAllPages, если нужно
        }

        private bool IsDuplicate(Vacancy newVacancy)
        {
            // Приводим Date к UTC, если оно не null
            var newVacancyDateUtc = newVacancy.Date.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(newVacancy.Date, DateTimeKind.Utc)
                : newVacancy.Date.ToUniversalTime();

            var existing = dbContext.Vacancies
                .FirstOrDefault(v => v.Company == newVacancy.Company &&
                                     v.ShortTitle == newVacancy.ShortTitle &&
                                     Math.Abs((v.Date - newVacancyDateUtc).Days) <= 40);

            if (existing != null) return true;

            string newFirstWord = newVacancy.Title?.Split(' ')[0];
            return dbContext.Vacancies
                .Any(v => v.Phone == newVacancy.Phone &&
                          v.Title.StartsWith(newFirstWord) &&
                          Math.Abs((v.Date - newVacancyDateUtc).Days) <= 40);
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
            driver?.Quit();
            dbContext?.Dispose();
            httpClient?.Dispose();
            base.OnClosed(e);
        }
    }
}
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppDbContext dbContext;
        private int currentPage = 1;
        private int pageSize = 100;
        private bool isInitialized = false; // Флаг инициализации

        public MainWindow()
        {
            try
            {
                dbContext = new AppDbContext();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                dbContext = null;
            }

            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            isInitialized = true; // Устанавливаем флаг после полной инициализации

            // Устанавливаем начальное значение pageSize
            pageSize = 100; // Соответствует SelectedIndex="1" (второй элемент в ComboBox)
            LoadVacancies();

            if (dateToPicker != null)
            {
                dateToPicker.SelectedDate = DateTime.Today;
            }
        }

        private void LoadVacancies()
        {
            try
            {
                if (dbContext == null)
                {
                    logTextBox.AppendText("База данных не инициализирована.\n");
                    return;
                }

                var query = dbContext.Vacancies.AsQueryable();

                // Фильтрация по датам
                if (dateFromPicker != null && dateFromPicker.SelectedDate.HasValue)
                {
                    var dateFrom = dateFromPicker.SelectedDate.Value;
                    query = query.Where(v => v.Date >= dateFrom);
                }
                if (dateToPicker != null && dateToPicker.SelectedDate.HasValue)
                {
                    var dateTo = dateToPicker.SelectedDate.Value;
                    query = query.Where(v => v.Date <= dateTo);
                }

                var totalRecords = query.Count();
                var vacancies = query
                    .OrderByDescending(v => v.ParseDate)
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var viewModels = vacancies
                    .Select((v, index) => new VacancyViewModel(v, (currentPage - 1) * pageSize + index + 1))
                    .ToList();

                var collectionView = CollectionViewSource.GetDefaultView(viewModels);
                dataGrid.ItemsSource = collectionView;

                if (recordsCountLabel != null)
                    recordsCountLabel.Content = totalRecords.ToString();
                if (totalPagesLabel != null)
                    totalPagesLabel.Content = $"из {Math.Ceiling((double)totalRecords / pageSize)}";
                if (currentPageTextBox != null)
                    currentPageTextBox.Text = currentPage.ToString();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка в LoadVacancies: {ex.Message}\n\nStackTrace: {ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nInnerException: {ex.InnerException.Message}\n\nInner StackTrace: {ex.InnerException.StackTrace}";
                }
                logTextBox.AppendText(errorMessage + "\n");
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Пустые обработчики событий
        private void VacancyTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStartButtonState();
        }

        private void CityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStartButtonState();
        }

        private void UpdateStartButtonState()
        {
            startButton.IsEnabled = !string.IsNullOrWhiteSpace(VacancyTitleTextBox.Text) && !string.IsNullOrWhiteSpace(CityTextBox.Text);
        }
        private void StartButton_Click(object sender, RoutedEventArgs e) { }
        private void StopButton_Click(object sender, RoutedEventArgs e) { }
        private void ExportButton_Click(object sender, RoutedEventArgs e) { }
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { }
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
            try
            {
                if (dbContext == null)
                {
                    logTextBox.AppendText("База данных не инициализирована.\n");
                    return;
                }

                var totalRecords = dbContext.Vacancies.Count();
                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
                if (currentPage < totalPages)
                {
                    currentPage++;
                    LoadVacancies();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в NextPage_Click: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dbContext == null)
                {
                    logTextBox.AppendText("База данных не инициализирована.\n");
                    return;
                }

                var totalRecords = dbContext.Vacancies.Count();
                currentPage = (int)Math.Ceiling((double)totalRecords / pageSize);
                if (currentPage == 0) currentPage = 1;
                LoadVacancies();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в LastPage_Click: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!isInitialized) // Пропускаем вызов во время инициализации
                {
                    return;
                }

                if (pageSizeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
                {
                    if (int.TryParse(selectedItem.Content.ToString(), out int newPageSize))
                    {
                        pageSize = newPageSize;
                        currentPage = 1; // Сбрасываем на первую страницу
                        LoadVacancies();
                    }
                    else
                    {
                        logTextBox.AppendText("Ошибка: Неверный формат размера страницы.\n");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в PageSizeComboBox_SelectionChanged: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e) { }
    }
    public class VacancyViewModel
    {
        public int RowNumber { get; set; }
        public DateTime ParseDate { get; set; }
        public DateTime Date { get; set; }
        public string SiteId { get; set; }
        public string Link { get; set; }
        public string Domain { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string Address { get; set; }
        public string Company { get; set; }
        public string ContactName { get; set; }

        public VacancyViewModel(Vacancy vacancy, int rowNumber)
        {
            RowNumber = rowNumber;
            ParseDate = vacancy.ParseDate;
            Date = vacancy.Date;
            SiteId = vacancy.SiteId;
            Link = vacancy.Link;
            Domain = vacancy.Domain;
            Phone = vacancy.Phone;
            Title = vacancy.Title;
            Address = vacancy.Address;
            Company = vacancy.Company;
            ContactName = vacancy.ContactName;
        }
    }
}
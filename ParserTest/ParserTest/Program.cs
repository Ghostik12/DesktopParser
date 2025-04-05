using HtmlAgilityPack;
using System;
using System.Data.SQLite;
using System.Linq;
using System.Net;

namespace ParserTest
{
    class Program
    {
        static void Main()
        {
            // Инициализация базы данных
            InitializeDatabase();

            // URL страницы с вакансиями
            string url = "https://nn.rabota.ru/vacancy/?query=Уборщик";
            string urlDate = "https://nn.rabota.ru";

            // Загрузка HTML-страницы
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(url);

            // Парсинг списка вакансий
            var vacancies = doc.DocumentNode.SelectNodes("//div[contains(@class, 'vacancy-preview-card__top')]");

            if (vacancies != null)
            {
                foreach (var vacancy in vacancies)
                {
                    // Название вакансии
                    var titleNode = vacancy.SelectSingleNode(".//a[contains(@class, 'vacancy-preview-card__title_border')]");
                    string title = titleNode?.InnerText.Trim();

                    // Название компании
                    var companyNode = vacancy.SelectSingleNode(".//a[contains(@itemprop, 'name')]");
                    string company = WebUtility.HtmlDecode(companyNode?.InnerText.Trim())
                        .Replace(@"&nbsp;|\s+", " ");

                    // Зарплата (если есть)
                    var salaryNode = vacancy.SelectSingleNode(".//a[contains(@itemprop, 'title')]");
                    string salary = WebUtility.HtmlDecode(salaryNode?.InnerText.Trim())
                        .Replace(@"&nbsp;|\s+", " ");

                    // Ссылка на вакансию
                    string link = titleNode?.GetAttributeValue("href", "");
                    
                    doc = web.Load($"{urlDate}{link}");

                    // Дата публикации
                    var dateNode = doc.DocumentNode.SelectSingleNode(".//span[contains(@class, 'vacancy-system-info__updated-date')]");
                    string date = dateNode?.InnerText.Trim();


                    // Вывод данных в консоль
                    Console.WriteLine($"Вакансия: {title}");
                        Console.WriteLine($"Компания: {company}");
                        Console.WriteLine($"Дата: {date}");
                        Console.WriteLine($"Зарплата: {salary}");
                        Console.WriteLine($"Ссылка: {link}");
                        Console.WriteLine(new string('-', 50));
                }
            }
            else
            {
                Console.WriteLine("Вакансии не найдены.");
            }
        }

        // Инициализация базы данных
        static void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection("Data Source=vacancies.db"))
            {
                connection.Open();
                var command = new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS Vacancies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Company TEXT NOT NULL,
                    Date TEXT NOT NULL,
                    Salary TEXT,
                    Link TEXT NOT NULL
                )", connection);
                command.ExecuteNonQuery();
            }
        }

        // Сохранение вакансии в базу данных
        static void SaveVacancy(string title, string company, string date, string salary, string link)
        {
            using (var connection = new SQLiteConnection("Data Source=vacancies.db"))
            {
                connection.Open();
                var command = new SQLiteCommand(@"
                INSERT INTO Vacancies (Title, Company, Date, Salary, Link)
                VALUES (@title, @company, @date, @salary, @link)", connection);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@company", company);
                command.Parameters.AddWithValue("@date", date);
                command.Parameters.AddWithValue("@salary", salary);
                command.Parameters.AddWithValue("@link", link);
                command.ExecuteNonQuery();
            }
        }
    }
}

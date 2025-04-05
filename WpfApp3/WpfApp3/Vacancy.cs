namespace WpfApp3
{
    public class Vacancy
    {
        public int Id { get; set; }
        public DateTime ParseDate { get; set; } // Дата парсинга
        public DateTime Date { get; set; } // Дата на сайте
        public string SiteId { get; set; }
        public string Link { get; set; }
        public string Domain { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; }
        public string Address { get; set; }
        public string Company { get; set; }
        public string ContactName { get; set; }
    }
}
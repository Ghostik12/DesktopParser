using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabotaRuParser.Models
{
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

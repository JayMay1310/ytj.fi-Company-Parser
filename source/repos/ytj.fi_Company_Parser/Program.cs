using KBCsv;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ytj.fi_Company_Parser.Model;

namespace ytj.fi_Company_Parser
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> listQuery = new List<string>();
            StreamReader sr = new StreamReader("url.txt");
            string line;

            while (!sr.EndOfStream)
            {
                line = sr.ReadLine();
                listQuery.Add(line);
            }

            sr.Close();

            ChromeOptions options = new ChromeOptions();
            ChromeDriver driver = new ChromeDriver(options);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);


            using (var streamWriter = new StreamWriter("Result.csv", false, Encoding.UTF8))
            using (var writer = new CsvWriter(streamWriter))
            {
                writer.ForceDelimit = true;
                writer.ValueSeparator = ';';
                writer.ValueDelimiter = '*';

                writer.WriteRecord("Компания", "Телефон компаний", "Сайт компаний", "Данные из регистра");

                foreach (string itemQuery in listQuery)
                {
                    Console.WriteLine("Get - " + itemQuery);
                    try
                    {
                        List<Company> companyBatch = GetData(driver, itemQuery);

                        foreach (Company itemCompany in companyBatch)
                        {
                            if (itemCompany.Url == "-")
                            {

                            }

                            writer.WriteRecord(itemCompany.Name, itemCompany.Phone, itemCompany.Url, itemCompany.DataRegistry);
                        }
                    }
                    catch
                    {
                      
                        try
                        {
                            IAlert alert = driver.SwitchTo().Alert();
                            alert.Accept();

                            driver.Navigate().Refresh();
                        }
                        catch (NoAlertPresentException e)
                        {
                            //Console.WriteLine("Алерта нет. Все ок");
                        }
                    
                        Console.WriteLine("Browser refresh. Wait 30 s");
                        Thread.Sleep(30000);
                        continue;
                    }
                }
            }
            driver.Quit();
        }

        static List<Company> GetData(IWebDriver driver, string Query)
        {
            List<Company> listCompany = new List<Company>();
            driver.Navigate().GoToUrl("https://www.ytj.fi/");
            driver.FindElement(By.XPath("//*[@id='search-term']")).SendKeys(Query);

            driver.FindElement(By.XPath("//*[@class='btn btn-lg'][text()='Etsi']")).Click();

            try
            {
                IAlert alert = driver.SwitchTo().Alert();
                alert.Accept();
            }
            catch (NoAlertPresentException e)
            {
                //Console.WriteLine("Алерта нет. Все ок");
            }

            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(_driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));

            //Тут переходим на результат поиска и подучаем все ссылки
            //IReadOnlyCollection<IWebElement> listLink = driver.FindElements(By.XPath("//*[@id='search-result']/table/tbody/tr/td/a"));

            Dictionary<string, string> listOneTableValue = GetOneTableValue(driver);

            foreach (var itemLink in listOneTableValue)
            {
                Company company = new Company();
                company.Name = itemLink.Value;

                driver.Navigate().GoToUrl(itemLink.Key);

                wait.Until(_driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));

                //С этой карточки собираем все данные и записываем в CSV
                //Получаем все таблицы, потом исходя из индекса будем сканировать их
                IReadOnlyCollection<IWebElement> listTableDom = driver.FindElements(By.XPath("//*[@id='detail-result']/table/tbody"));
                int countTable = 0;
                foreach (IWebElement table in listTableDom)
                {
                    //Тут разбираем нужные таблицы на нужные значение
                    if (countTable == 0)
                    {
                        company.Url = GetSite(table);
                        company.Phone = GetPhone(table);
                    }

                    //В этой таблице берем данные из регистра
                    if (countTable == 1)
                    {
                        company.DataRegistry = GetRegistryData(table);
                    }

                    countTable++;
                }

                listCompany.Add(company);
            }

            return listCompany;
        }
        /// <summary>
        /// return Dictionary object Company for further handler
        /// </summary>
        /// <param name="driver"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetOneTableValue(IWebDriver driver)
        {
            Dictionary<string, string> listCompany = new Dictionary<string, string>();

            //Получаем все строки первой таблицы
            IReadOnlyCollection<IWebElement> listRow = driver.FindElements(By.XPath("//*[@id='search-result']/table/tbody/tr"));
            int count = 0;
            foreach (IWebElement row in listRow)
            {
                if (count == 0)
                {
                    count++;
                    continue;
                }

                string link = driver.FindElement(By.XPath("//td/a")).GetAttribute("href");
                string name = driver.FindElement(By.XPath("//td[2]")).Text;
                //string name = driver.FindElement()
                try
                {
                    listCompany.Add(link, name);
                }
                catch (ArgumentException e)
                {
                    continue;
                }

                count++;
            }

            return listCompany;
        }

        private static string GetSite(IWebElement table)
        {
            string url = "-";
            bool isElement = false;

            try
            {
                table.FindElement(By.XPath("//td/a[text()='www                                     ']"));
                isElement = true;
            }
            catch
            {
                isElement = false; ;
            }

            if (isElement)
            {
                //Теперь нужно узнать индекс строки второго столбца

                IReadOnlyCollection<IWebElement> listRow = table.FindElements(By.XPath("//td[1]"));
                int countRow = 0;
                foreach (IWebElement item in listRow)
                {
                    if (item.Text.Contains("www"))
                    {
                        url = table.FindElements(By.XPath("//td[2]"))[countRow].Text;

                    }
                    countRow++;
                }
            }

            return url;
        }

        private static string GetPhone(IWebElement table)
        {
            string phone = "-";

            bool isElement = false;

            try
            {
                table.FindElement(By.XPath("//td/a[text()='Matkapuhelin']"));
                isElement = true;
            }
            catch
            {
                isElement = false; ;
            }

            if (isElement)
            {
                //Теперь нужно узнать индекс строки второго столбца

                IReadOnlyCollection<IWebElement> listRow = table.FindElements(By.XPath("//td[1]"));
                int countRow = 0;
                foreach (IWebElement item in listRow)
                {
                    if (item.Text.Contains("Matkapuhelin"))
                    {
                        phone = table.FindElements(By.XPath("//td[2]"))[countRow].Text;
                    }
                    countRow++;
                }            
            }

            return phone;
        }

        private static string GetRegistryData(IWebElement table)
        {
            string dataRegistry = "{";

            string tableText = table.Text;
            string[] rows = tableText.Split(new char[] { '\n' });

            //Формруем регистрационные данные без header'a
            for (int i = 1; i <= rows.Count() - 1; i++)
            {
                dataRegistry += rows[i].TrimEnd('\r', '\n') + "|";
            }

            dataRegistry += "}";
            return dataRegistry;
        }
        /// <summary>
        /// Пытаемся получить сайт компаний во второй вкладке.
        /// </summary>
        /// <param name="companyName"></param>
        /// <returns></returns>
        private static string GetSiteIs(IWebDriver driver, string companyName)
        {
            string site = String.Empty;

            driver.FindElement(By.CssSelector("body")).SendKeys(Keys.Control + "t");


            return site;
        }
    }
}

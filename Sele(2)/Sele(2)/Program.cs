using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using Sele_2_;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static string servAdd = "https://api.cherrypick.com/api";

    static IWebDriver driver;
    static int size = 1500;

    public static string[] Sep(string prompt)
    {
        prompt = prompt.Replace("\r", "").Replace("\n", "");
        prompt = string.Join(" ", prompt.Split(" "));
        var chunks = new string[prompt.Length / size + 1];
        for (int i = 0; i < prompt.Length; i += size)
        {
            int j = Math.Min(size, prompt.Length - i - 1);
            chunks[i / size] = prompt.Substring(i, j + 1);
        }
        return chunks;
    }

    public static string GetAns(string src)
    {
        int b = src.LastIndexOf("(ANS BEGIN)") + "(ANS BEGIN)".Length;
        int e = src.LastIndexOf("(ANS END)");
        return src.Substring(b, e - b);
    }

    public static string Ask(string prompt, string ctx)
    {
        var chunks = Sep(ctx);
        
        driver.Navigate().GoToUrl("https://www.perplexity.ai/");

        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        Thread.Sleep(2000);
        var answer = "";
        var inputBox = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//textarea[@placeholder and string-length(@placeholder) > 1]")));

        foreach (var chunk in chunks)
        {
            string suff = $"\n\n{prompt}\n\n***Please format your response as follows, even if your response is not the answer I asked: \n(ANS BEGIN) Example answer.... (ANS END)";
            var p = chunk + suff;
            inputBox.SendKeys(p);
            inputBox.SendKeys(Keys.Enter);

            // Wait for the page to stabilize
            string lastSrc = "";
            DateTime lastChangeTime = DateTime.Now;

            while (true)
            {
                Thread.Sleep(500); // Check every half second
                var src = driver.FindElements(By.TagName("body"))[0].GetAttribute("innerHTML");

                if (src != lastSrc)
                {
                    lastChangeTime = DateTime.Now; // Update change time if content has changed
                    lastSrc = src; // Update last source to the current one
                }

                // Check if the content has been stable for 2 seconds
                if ((DateTime.Now - lastChangeTime).TotalSeconds >= 2)
                {
                    var resp = GetAns(src);
                    if (resp.Length < 50)
                        continue;
                    answer += "\n" + resp + "\n";
                    break; // Exit the loop to process the next chunk
                }
            }

            inputBox = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//textarea[@placeholder='Ask follow-up']")));
        }
        return answer;
    }

    public static string Visit(string url)
    {
        // Navigate to Perplexity AI
        driver.Navigate().GoToUrl(url);

        // Wait for the input box to be visible
        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        Thread.Sleep(2000);
        // Enter the prompt
        var src = driver.FindElements(By.TagName("body"))[0].GetAttribute("innerText");
        // Output the response
        return src;
    }

    static async Task Search(Query q)
    {
        driver = new FirefoxDriver();
        string src = "";
        if (q.Url != null)
        {
            src = Visit(q.Url);
            Thread.Sleep(500);
        }
        if(q.File != null)
        {
            src += $"\n {q.File}\n";
        }

        var ctx = $"SOURCE BEGIN\n {src}\n SOURCE END";
        var prompt = q.Content;
        var ans = Ask(prompt, ctx);

        var client = new HttpClient();
        var target = servAdd + "/mindeye/add-answer";
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            Question = new { OrganizationQueryId = q.Id },
            Answer = ans,
            Sources = new string[] { }
        });
        HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync(target, content);
        driver.Quit();
        return;
    }

    static async Task Main()
    {
        var wait = 10;

        while (true)
        {
            try
            {
                servAdd = "https://api.cherrypick.com/api";
                servAdd = "http://10.0.0.15:8082/api";
                servAdd = "http://localhost:44351/api";
                servAdd = "https://cherrypick-crepbvgncqb8g6gw.centralus-01.azurewebsites.net/api";
                var target = servAdd + "/query/latest";
                string jsonResponse = await new HttpClient().GetStringAsync(target);
                var query = JsonConvert.DeserializeObject<Query>(jsonResponse);
                
                wait = 10;
                if (query != null)
                {
                    query.Content = query.Content.Replace("XXYYZZ", query.Organization);
                    await Search(query);
                }
                else
                    wait = 12;
            }
            catch (Exception e)
            {
                wait = 12;
                try
                {
                    driver.Quit();
                }
                catch (Exception ex)
                {
                }
            }
            Thread.Sleep(wait * 1000);
        }
    }
}

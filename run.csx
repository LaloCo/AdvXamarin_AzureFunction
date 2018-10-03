#r "System.Runtime.Serialization"
#r "Newtonsoft.Json"

using System;
using System.Xml;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Runtime.Serialization;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    RSSReader reader = new RSSReader(log);
    reader.ReadRss();
}

public class RSSReader
{
    public TraceWriter Log;
    public Blog Posts;

    public RSSReader(TraceWriter log)
    {
        Log = log;
    }

    public void ReadRss()
    {
        Log.Info("Reading RSS");

        using (WebClient client = new WebClient())
        {
            string xml = Encoding.UTF8.GetString(client.DownloadData("https://www.finzen.mx/blog-feed.xml"));
            Log.Info(xml);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            string json = JsonConvert.SerializeXmlNode(doc);
            json = json.Replace("#", "");
            json = json.Replace("cdata-section", "actualstring");

            Posts = JsonConvert.DeserializeObject<Blog>(json);
        }

        CheckIfNewPosts();
    }

    public void CheckIfNewPosts()
    {
        Log.Info("Checking if new posts");
        
        var newPosts = Posts.Rss.Channel.Item.Where(p => p.PublishedDate > DateTime.Now.AddDays(-1)).ToList();

        if(newPosts != null)
        {
            if(newPosts.Count > 0)
            {
                Log.Info($"{newPosts.Count} new posts found!");
                var newestPost = newPosts.FirstOrDefault();
                if(newestPost != null)
                {
                    SendNotification(newestPost);
                }
            }
        }
    }

    public async void SendNotification(Item post)
    {
        Log.Info($"Sending notification about {post.Title.ActualString}");

        using(HttpClient client = new HttpClient())
        {
            client.BaseAddress = new Uri("https://api.appcenter.ms/v0.1/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-API-Token", "d268f23ba60ef47cd75e270656cdbcc38e641014");

            var notificationObject = new {
                notification_content = new {
                    name = "LPANotification_" + post.Title.ActualString,
                    title = post.Title.ActualString,
                    body = post.Description.ActualString.Substring(0,200).Trim() + "...",
                    custom_data = new {
                        sound = "default"
                    }
                }
            };
            HttpResponseMessage response = await client.PostAsJsonAsync("apps/LearnProgrammingAcademy/Finance.iOS/push/notifications", notificationObject);
            HttpResponseMessage response2 = await client.PostAsJsonAsync("apps/LearnProgrammingAcademy/Finance.Android/push/notifications", notificationObject);
        }
    }
}

public class CData
{
    public string ActualString { get; set; }
}

public class Item
{
    public CData Title { get; set; }
    public CData Description { get; set; }
    public string Link { get; set; }

    private string pubDate;
    public string PubDate
    { 
        get { return pubDate; }
        set
        {
            pubDate = value;
            PublishedDate = DateTime.ParseExact(pubDate, "ddd, dd MMM yyyy HH:mm:ss GMT", CultureInfo.InvariantCulture);
        }
    }

    public DateTime PublishedDate { get; set; }
    public string Creator { get; set; }
}

public class Channel 
{
    public List<Item> Item { get; set; }
    public string Link { get; set; }
}

public class Rss
{
    public Channel Channel { get; set; }
}

public class Blog
{
    public Rss Rss { get; set; }
}

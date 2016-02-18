using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GitHubApi
{
    class Program
    {
        private static readonly string AccountName = "";
        private static readonly string AccountKey = "";

        public class Issue
        {
            public string Url { get; set; }
            public string EventsUrl { get; set; }
            public string HtmlUrl { get; set; }
            public string CommentsUrl { get; set; }
        }

        public class Event
        {
            public string Type { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Label { get; set; }
            public string CommentUrl { get; set; }
        }

        public class Comment
        {
            public DateTime CreatedAt { get; set; }
        }

        //TODO: cache httpclient
        //TODO: do proper asnyc/await 
        //TODO: extract common get-all-pages logic
        static void Main(string[] args)
        {
            var issues = GetIssues("q=is:open is:issue label:\"Tag: Core v6\" label:\"State: In Progress\" user:Particular");

            Console.WriteLine("######## Checking issues ############");

            foreach (var issue in issues)
            {
                var lastLabeledAsInProgressEvent = GetEvents(issue.EventsUrl)
                    .OrderBy(e => e.CreatedAt)
                    .Last(e => e.Type == "labeled" && e.Label == "State: In Progress");

                var lastCommentEvent = GetComments(issue.CommentsUrl)
                    .LastOrDefault();

                var stalenessData = DateTime.UtcNow.Subtract(TimeSpan.FromDays(7));

                if (lastCommentEvent == null || 
                    (lastCommentEvent.CreatedAt < stalenessData && lastLabeledAsInProgressEvent.CreatedAt < stalenessData))
                {
                    Console.WriteLine("Stale: " + issue.HtmlUrl);
                }
            }

            Console.WriteLine("######## Checking finished ############");

            Console.ReadLine();
        }

        public static List<Comment> GetComments(string commentsUrl)
        {
            var comments = new List<Comment>();

            var pageNo = 1;
            var hasMorePages = true;

            while (hasMorePages)
            {
                var response = GetResourcePage(commentsUrl, null, pageNo);
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic result = JsonConvert.DeserializeObject(content);

                foreach (var item in result)
                {
                    comments.Add(new Comment
                    {
                        CreatedAt = DateTime.Parse(item.created_at.ToString()),
                    });
                }

                hasMorePages = HasMorePages(response);
                pageNo++;
            }

            return comments;
        } 

        public static List<Event> GetEvents(string eventsUrl)
        {
            var issues = new List<Event>();

            var pageNo = 1;
            var hasMorePages = true;

            while (hasMorePages)
            {
                var response = GetResourcePage(eventsUrl, null, pageNo);
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic result = JsonConvert.DeserializeObject(content);

                foreach (var item in result)
                {
                    issues.Add(new Event
                    {
                        Type = item.@event.ToString(),
                        CreatedAt = DateTime.Parse(item.created_at.ToString()),
                        Label = item.label != null ? item.label.name.ToString() : null,
                        CommentUrl = item.comment != null ? item.comment.url.ToString() : null
                    });
                }

                hasMorePages = HasMorePages(response);
                pageNo++;
            }

            return issues;
        } 

        public static List<Issue> GetIssues(string queryParameters)
        {
            var issues = new List<Issue>();

            var pageNo = 1;
            var hasMorePages = true;

            while (hasMorePages)
            {
                var response = GetResourcePage("https://api.github.com/search/issues", queryParameters, pageNo);
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic result = JsonConvert.DeserializeObject(content);

                foreach (var item in result.items)
                {
                    issues.Add(new Issue {
                        Url = item.url.ToString(),
                        EventsUrl = item.events_url.ToString(),
                        CommentsUrl = item.comments_url.ToString(),
                        HtmlUrl = item.html_url.ToString()
                    });
                }

                hasMorePages = HasMorePages(response);
                pageNo++;
            }

            return issues;
        } 

        public static bool HasMorePages(HttpResponseMessage response)
        {
            if (response.Headers.Any(h => h.Key == "Link"))
            {
                var links = response.Headers.First(h => h.Key == "Link").Value.First();

                return links.Contains("rel=\"last\"");
            }

            return false;
        }

        public static HttpResponseMessage GetResourcePage(string resource, string queryParameters, int pageNo, int pageSize = 100)
        {
            var url = !string.IsNullOrEmpty(queryParameters)
                ? $"{resource}?{queryParameters}&page={pageNo}&per_page={pageSize}"
                : $"{resource}?page={pageNo}&per_page={pageSize}";

            return GetResource(url);
        }

        public static HttpResponseMessage GetResource(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident / 6.0)");

                var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AccountName}:{AccountKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

                var response = client.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode == false)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                return response;
            }
        }
    }
}

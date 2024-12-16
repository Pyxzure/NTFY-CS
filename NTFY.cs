using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NTFY
{
    public static class Client
    {
        private static string _BASE_URL = "https://ntfy.sh/";
        private static string? _TOKEN = null;

        /// <summary>
        /// Sets default authentication. For token mode, pass the token to the password field and leave username blank.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="username"></param>
        /// <returns>null</returns>
        public static void SetAuthentication(string password, string username = "")
        {
            if (password == "" && username == "")
                _TOKEN = null;
            else
                _TOKEN = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
        }

        /// <summary>
        /// Sets default server url
        /// </summary>
        /// <returns>null</returns>
        public static void SetServer(string url)
        {
            _BASE_URL = url;
        }

        public static async Task<NMessage> Publish(string topic, string msg, string username, string password,
            Header? header = null, string? server = null)
        {
            string token = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            return await Publish(topic, msg, header, token, server);
        }

        public static async Task<NMessage> Publish(string topic, string msg,
            Header? header = null, string? token = null, string? server = null)
        {
            Dictionary<string, string> hdr = (header is null) ? new() : header.ToDict();
            server ??= _BASE_URL;
            server += (server[^1] != '/') ? "/" : "";
            token = (token is null) ? _TOKEN : (token[0..6] == "Basic " ? token : $"Bearer {token}");
            string url = server + topic;
            Encoding enc = Encoding.Default;
            HttpClient _HttpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
            foreach (KeyValuePair<string, string> kvp in hdr) request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            if (token != null) request.Headers.TryAddWithoutValidation("Authorization", token);
            request.Content = new StringContent(msg, Encoding.UTF8, "text/plain");

            using var response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var streamReader = new StreamReader(responseStream, enc);
            string o = await streamReader.ReadToEndAsync().ConfigureAwait(false);
            NMessage obj = JsonConvert.DeserializeObject<NMessage>(o)!;
            return obj;
        }
    }

    /// <summary>
    /// The Server class for subscribing to messages. Attach to the event handler NewNotification to act on new messages. Await on the Start() method to start listening, and call the Stop() method to end.
    /// </summary>
    public class Server
    {
        public event EventHandler<NotiEventArgs>? NewNotification;
        public event EventHandler<DCEventArgs>? Disconnected;

        private readonly string endpoint;
        private readonly string? token;

        private bool Listening;
        private CancellationTokenSource cts = new();
        private CancellationToken ctoken;

        public Server(string topic, string? token = null, string server = "https://ntfy.sh/")
        {
            this.endpoint = server + (server[^1] != '/' ? "/" : "") + topic + "/json";
            if (token is not null) this.token = $"Bearer {token}";
        }
        public Server(string topic, string username, string password, string server = "https://ntfy.sh/")
        {
            this.endpoint = server + (server[^1] != '/' ? "/" : "") + topic + "/json";
            this.token = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
        }
        /// <summary>
        /// Start listening
        /// </summary>
        /// <param name="reconnect">If set to true, will automatically reconnect when disconnected for any reasons except calling Stop(). Disconnect event will be called with the Exception as the EventArg. Will not attempt to reconnect if set to false, and any exceptions will be re-thrown.</param>
        async public Task Start(bool reconnect = false)
        {
            if (!Listening)
            {
                cts = new CancellationTokenSource();
                ctoken = cts.Token;
                Listening = true;
                while (reconnect && Listening && !ctoken.IsCancellationRequested)
                {
                    try
                    {
                        await Listen();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        if (!reconnect) throw;
                        OnDisconnected(new(ex));
                    }
                    await Task.Delay(1000);
                }
            }
        }
        public void Stop()
        {
            Listening = false;
            cts.Cancel();
        }
        async private Task Listen()
        {
            string url = endpoint;
            Encoding enc = Encoding.Default;
            HttpClient _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);

            using Stream response = await _HttpClient.GetStreamAsync(endpoint, ctoken).ConfigureAwait(false);
            using StreamReader streamReader = new StreamReader(response, enc);

            while (!ctoken.IsCancellationRequested)
            {
                string? o = await streamReader.ReadLineAsync().ConfigureAwait(false);
                if (o is not null)
                {
                    NMessage obj = JsonConvert.DeserializeObject<NMessage>(o)!;
                    OnNewNotification(new NotiEventArgs(obj));
                }
                else
                {
                    throw new InvalidOperationException("Stream closed");
                }
            }
            throw new OperationCanceledException("Task cancelled");
        }
        protected virtual void OnNewNotification(NotiEventArgs e)
        {
            NewNotification?.Invoke(this, e);
        }
        protected virtual void OnDisconnected(DCEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }
    }
    public class Header
    {
        public bool Markdown;
        /// <summary>
        /// Set to false to disable caching
        /// </summary>
        public bool Cache = true;
        /// <summary>
        /// Set to false to disable FCM forwarding
        /// </summary>
        public bool Firebase = true;
        /// <summary>
        /// Priority (1-5)
        /// </summary>
        public int? Priority;
        /// <summary>
        /// Comma separated Tags
        /// </summary>
        public string? Tags;
        public string? Title;
        /// <summary>
        /// Scheduled message
        /// Unix timestamp (e.g. "1639194738"), a duration (e.g. 30m, 3h, 2 days), or a natural language time string (e.g. 10am, 8:30pm, tomorrow, 3pm, Tuesday, 7am, and more)
        /// </summary>
        public string? Delay;
        /// <summary>
        /// Attachment URL
        /// </summary>
        public string? Attach;
        /// <summary>
        /// Icon URL
        /// </summary>
        public string? Icon;
        /// <summary>
        /// Override attachment filename
        /// </summary>
        public string? Filename;
        /// <summary>
        /// URL to open when clicked
        /// </summary>
        public string? Click;
        /// <summary>
        /// E-mail address for e-mail notifications
        /// </summary>
        public string? Email;
        public readonly List<string> Actions = new();

        public void AddViewAction(string label, string url, bool clear = false)
        {
            string action = $"view, {label}, url={url}, clear={(clear ? "true" : "false")}";
            Actions.Add(action);
        }

        public void AddHTTPAction(string label, string url, string? method, string? headers, string? body, bool clear = false)
        {
            string action = $"broadcast, {label}, url={url}, clear={(clear ? "true" : "false")}";
            if (method is not null) action += $", intent={method}";
            if (headers is not null) action += $", intent={headers}";
            if (body is not null) action += $", intent={body}";
            Actions.Add(action);
        }

        public void AddBroadcastAction(string label, string? intent, string? extras, bool clear = false)
        {
            string action = $"broadcast, {label}, clear={(clear ? "true" : "false")}";
            if (intent is not null) action += $", intent={intent}";
            if (extras is not null) action += $", intent={extras}";
            Actions.Add(action);
        }

        public Dictionary<string, string> ToDict()
        {
            Dictionary<string, string> output = new();
            if (Tags is not null) output["Tags"] = Tags;
            if (Title is not null) output["Title"] = Title;
            if (Delay is not null) output["Delay"] = Delay;
            if (Attach is not null) output["Attach"] = Attach;
            if (Icon is not null) output["Icon"] = Icon;
            if (Filename is not null) output["Filename"] = Filename;
            if (Email is not null) output["Email"] = Email;
            if (Click is not null) output["Click"] = Click;
            if (Priority is not null && Priority >= 1 && Priority <= 5) output["Priority"] = Priority.ToString()!;
            if (Markdown) output["Markdown"] = "yes";
            if (!Cache) output["Cache"] = "no";
            if (!Firebase) output["Firebase"] = "no";
            if (Actions.Count > 0) output["Actions"] = string.Join(";", Actions.ToArray());
            return output;
        }
    }

    /// <summary>
    /// The event args for the new message notification. Extract the Message parameter to ge the NMessage object
    /// </summary>
    public class NotiEventArgs : EventArgs
    {
        public NMessage Message { get; init; }
        public NotiEventArgs(NMessage Message)
        {
            this.Message = Message;
        }
    }
    /// <summary>
    /// The event args for the Disconnect event. Stores the Exception causing the Disconnect event.
    /// </summary>
    public class DCEventArgs : EventArgs
    {
        public Exception? Exception { get; init; }
        public DCEventArgs(Exception? Exception)
        {
            this.Exception = Exception;
        }
    }
    public class NMessage
    {
        public enum EventType
        {
            Open, Keepalive, Message, Poll_request
        }

        /// <summary>
        /// Randomly chosen message identifier.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Message date and time as a Unix time stamp.
        /// </summary>
        public long Time { get; init; }

        /// <summary>
        /// Unix time stamp indicating when the message will expire. 
        /// This value is nullable if not set.
        /// </summary>
        public long? Expires { get; init; }

        /// <summary>
        /// The type of event. Possible values are: open, keepalive, message, or poll_request.
        /// </summary>
        public EventType Event { get; init; }

        /// <summary>
        /// A comma-separated list of topics the message is associated with.
        /// </summary>
        public string Topic { get; init; }

        /// <summary>
        /// The message body. This is only present in message events.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// The title of the message. Defaults to "ntfy.sh/&lt;topic&gt;" if not set.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// A list of tags that may map to emojis.
        /// </summary>
        public List<string>? Tags { get; init; }

        /// <summary>
        /// The priority of the message. 
        /// Values range from 1 (minimum priority) to 5 (maximum priority), with 3 as the default.
        /// </summary>
        public int? Priority { get; init; }

        /// <summary>
        /// A URL that will be opened when the notification is clicked.
        /// </summary>
        public string? Click { get; init; }

        /// <summary>
        /// A list of action buttons that can be displayed in the notification.
        /// </summary>
        public List<ActionButton>? Actions { get; init; }

        /// <summary>
        /// Details about an attachment associated with the message.
        /// </summary>
        public Attachment? Attachment { get; init; }

        public NMessage()
        {
            Id = "";
            Topic = "";
        }
    }
    public class ActionButton
    {
        /// <summary>
        /// The type of the action. 
        /// </summary>
        public string Action { get; init; }

        /// <summary>
        /// The label of the action button in the notification.
        /// </summary>
        public string Label { get; init; }

        /// <summary>
        /// The URL to open when the action button is tapped.
        /// </summary>
        public string? Url { get; init; }

        /// <summary>
        /// Indicates whether to clear the notification after the action button is tapped.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool Clear { get; init; }

        public string? Intent { get; init; }
        public JObject? Extras { get; init; }
        public JObject? Headers { get; init; }
        public string? Method { get; init; }
        public string? Body { get; init; }

        public ActionButton()
        {
            Action = "view";
            Label = "";
        }
    }
    public class Attachment
    {
        /// <summary>
        /// Represents the name of the attachment.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Represents the URL of the attachment.
        /// </summary>
        public string Url { get; init; }

        /// <summary>
        /// Represents the size of the attachment in bytes. This value is nullable.
        /// </summary>
        public long? Size { get; init; }

        /// <summary>
        /// Represents the MIME type of the attachment. 
        /// This is only defined if the attachment was uploaded to the NTFY server.
        /// </summary>
        public string? Type { get; init; }

        /// <summary>
        /// Represents the expiry date of the attachment as a Unix time stamp. 
        /// This is only defined if the attachment was uploaded to the NTFY server.
        /// </summary>
        public long? Expires { get; init; }

        public Attachment()
        {
            Name = "";
            Url = "";
        }
    }
}

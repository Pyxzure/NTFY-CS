
# NTFY-CS
Lightweight ntfy library for .NET

### Dependencies

 - Tested on .NET 6.0
 - Requires Newtonsoft.Json from NuGet

### Usage
Publishing a message
```
using NTFY;

//Simplest way to publish to the ntfy server
await Client.Publish("topic", "message");
```

```
//Add some headers, publish to a self-hosted server, capture the response
string svr = "https://my.server:8080";
string user = "user";
string psw = "psw";
string token = "tk_token";
Header hdr = new()
{
    Click = "http://www.google.com",
    Tags = "test1,test2",
    Priority = 5
};
hdr.AddViewAction("To X!", "http://www.x.com");
NMessage re = await Client.Publish("topic", "message2", user, psw, hdr, svr);
Console.WriteLine(re.Id); //Gets the ID of the message
```
```
//Or set the default server and auth token to simplify Publish call
Client.SetServer(svr);
Client.SetAuthentication(token);
await Client.Publish("topic", "message3", hdr);
```

Subscribing to a topic
```
//Start a server to subscribe to messages

Server Svr = new("topic", token, svr);
Svr.NewNotification += CallBack; //Attach an event handler
Task t = Svr.Start(true); //Start listening with auto reconnect

await Task.Delay(100000);

Svr.Stop();

//Callback function to process messages
void CallBack(object? sender, EventArgs e)
{
    NMessage arg = ((NotiEventArgs)e).Message; //Extract the NMessage from the event arg
    if (arg.Event == NMessage.EventType.Message)
    {
        Console.WriteLine($"Message: {arg.Message}, ID: {arg.Id}");
    }
}
```

using NTFY;

//Simplest way to publish to the ntfy server
await Client.Publish("topic", "message");

//Add some headers, publish to a self-hosted server, capture the response
Header hdr = new()
{
    Click = "http://www.google.com",
    Tags = "test1,test2",
    Priority = 5
};
hdr.AddViewAction("To X!", "http://www.x.com");
NMessage re = await Client.Publish("topic", "message2", "user", "psw", hdr, "https://my.server:8080");
Console.WriteLine(re.Id); //Gets the ID of the message

//Or set the default server and auth token to simplify Publish call
Client.SetServer("https://my.server:8080");
Client.SetAuthentication("tk_token");
await Client.Publish("topic", "message3", hdr);

//Start a server to subscribe to messages

Server Svr = new("topic", "token", "https://my.server:8080");
Svr.NewNotification += CallBack; //Attach an event handler
Svr.Start();

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
//github deployment gate payload object
public class Payload
{
    public string action { get; set; }
    public Deployment deployment { get; set; }
    public Installation installation { get; set; }
}

public class Installation
{
    public int id { get; set; }
}

public class Deployment
{
    public int id { get; set; }
    public string environment { get; set; }
    public Uri statuses_url { get; set;}
}
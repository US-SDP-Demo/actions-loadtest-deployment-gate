internal class AccessToken 
{
    public string token { get; set; }
    public DateTime expires_at { get; set; }
    public IDictionary<string, object> permissions { get; set; }
}
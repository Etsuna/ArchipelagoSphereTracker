public class SubElement
{
    public string SubKey { get; set; }
    public List<string> Values { get; set; }
}

public class displayedItemsElement
{
    public string sphere { get; set; }
    public string finder { get; set; }
    public string receiver { get; set; }
    public string item { get; set; }
    public string location { get; set; }
    public string game { get; set; }
}

public class trackerElement
{
    public string hachtag { get; set; }
    public string name { get; set; }
    public string game { get; set; }
    public string status { get; set; }
    public string checks { get; set; }
    public string pourcent { get; set; }
    public string lastActivity { get; set; }
}

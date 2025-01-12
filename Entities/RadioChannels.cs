using ModKit.ORM;
using Socket.Newtonsoft.Json;
using SQLite;
using System.Collections.Generic;

public class RadioChannels : ModEntity<RadioChannels>
{
    public enum RadioTypeEnum
    {
        None = 0,
        Public,
        Biz,
        Staff,
        Custom
    }

    [AutoIncrement][PrimaryKey] public int Id { get; set; }
    public string Name { get; set; }
    public int MaxSlot { get; set; }
    public RadioTypeEnum RadioType { get; set; }

    [Ignore]
    public List<int> BizIdAllowed { get; set; } = new List<int>();

    public string BizIdAllowedSerialized
    {
        get => JsonConvert.SerializeObject(BizIdAllowed);
        set => BizIdAllowed = string.IsNullOrEmpty(value)
            ? new List<int>()
            : JsonConvert.DeserializeObject<List<int>>(value);
    }

    public bool IsPrivate { get; set; }
    public string Code { get; set; }

    public RadioChannels() { }
}
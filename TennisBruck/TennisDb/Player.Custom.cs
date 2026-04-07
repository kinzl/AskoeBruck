namespace TennisDb;

public partial class Player
{
    public override string ToString()
    {
        return $"{Firstname} {Lastname}";
    }

    public string ToStringWithItn()
    {
        string formattedItn = Itn.HasValue
            ? Itn.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            : "10.3";
        return $"{Firstname} {Lastname} ({formattedItn})";
    }
}
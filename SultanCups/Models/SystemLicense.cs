namespace SultanCups.Models
{
    public class SystemLicense
    {
        public int id { get; set; }

        public string device_guid { get; set; } = "";

        public string? note { get; set; }
    }
}
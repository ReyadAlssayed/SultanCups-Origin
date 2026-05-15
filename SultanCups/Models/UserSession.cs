namespace SultanCups.Services
{
    public class UserSession
    {
        public int AdminId { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";

        public string Phone { get; set; } = "";
        public bool IsLoggedIn => AdminId > 0;

        public void Logout()
        {
            AdminId = 0;
            FullName = "";
            Role = "";
            Phone = "";
        }
    }
}
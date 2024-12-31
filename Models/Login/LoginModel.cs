using System;

namespace MES.Solution.Models
{
    public class LoginModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserRole { get; set; }
        public string Email { get; set; }
        public DateTime LoggedInTime { get; set; }
        public bool IsAdmin => UserRole?.ToUpper() == "ADMIN";
        public static LoginModel Empty => new LoginModel
        {
            UserId = 0,
            UserName = string.Empty,
            Email = string.Empty,
            UserRole = string.Empty,
            LoggedInTime = DateTime.MinValue
        };
        public override string ToString()
        {
            return $"User: {UserName} (ID: {UserId}, Role: {UserRole})";
        }
    }
}

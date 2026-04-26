using WpfApp3.Models;

namespace WpfApp3.Services
{
    public interface IAuthenticationService
    {
        User? CurrentUser { get; }
        bool IsLoggedIn { get; }

        event EventHandler<UserLoggedInEventArgs>? UserLoggedIn;
        event EventHandler? UserLoggedOut;

        Task<bool> RegisterAsync(string username, string email, string password, string? fullName = null);
        Task<bool> LoginAsync(string username, string password);
        void Logout();
        Task<bool> UpdateProfileAsync(string? fullName, string? email, string? avatar = null);
    }
}



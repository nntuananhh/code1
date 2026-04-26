using WpfApp3.Models;

namespace WpfApp3.Services
{
    public interface ISessionContext
    {
        User? CurrentUser { get; }
        bool IsLoggedIn { get; }
        int? CurrentUserId { get; }

        void SetCurrentUser(User user);
        void ClearCurrentUser();
    }
}







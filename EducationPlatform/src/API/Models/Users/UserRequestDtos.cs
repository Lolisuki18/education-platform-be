using Domain.IdentityManagement.Enum;

namespace API.Models.Users
{
    public class UpdateUserStatusRequestDto
    {
        public bool IsActive { get; set; }
    }

    public class DeleteAccountRequestDto
    {
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserRoleRequestDto
    {
        public Role Role { get; set; }
    }
}

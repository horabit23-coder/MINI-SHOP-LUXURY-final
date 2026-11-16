
using System.ComponentModel.DataAnnotations;

namespace MINI_SHOP_LUXURY.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [StringLength(254)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}

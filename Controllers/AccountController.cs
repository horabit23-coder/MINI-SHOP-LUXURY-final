
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class AccountController : Controller
    {
        // ================= ĐĂNG KÝ =================

        [HttpGet]
        public ActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string phone = model.Phone ?? "";
            string email = model.Email ?? "";

            if (phone.Length != 10 ||
                phone.Any(c => c < '0' || c > '9'))
            {
                ModelState.AddModelError("Phone",
                    "Số điện thoại phải có đúng 10 chữ số.");
                return View(model);
            }

            if (email.Any(char.IsWhiteSpace))
            {
                ModelState.AddModelError("Email",
                    "Email không được chứa dấu cách.");
                return View(model);
            }

            email = email.ToLowerInvariant();

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    string checkSql =
                        "SELECT COUNT(*) FROM dbo.Customers WHERE Email = @Email";

                    using (SqlCommand check = new SqlCommand(checkSql, connection))
                    {
                        check.Parameters.Add("@Email", SqlDbType.NVarChar, 254)
                            .Value = email;

                        if ((int)check.ExecuteScalar() > 0)
                        {
                            ModelState.AddModelError("Email",
                                "Email này đã được đăng ký.");
                            return View(model);
                        }
                    }

                    string passwordHash = HashPassword(model.Password);

                    string sql = @"
                        INSERT INTO dbo.Customers
                        (FullName, Phone, Email, PasswordHash)
                        VALUES
                        (@FullName, @Phone, @Email, @PasswordHash)";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100)
                            .Value = model.FullName.Trim();

                        command.Parameters.Add("@Phone", SqlDbType.VarChar, 10)
                            .Value = phone;

                        command.Parameters.Add("@Email", SqlDbType.NVarChar, 254)
                            .Value = email;

                        command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500)
                            .Value = passwordHash;

                        command.ExecuteNonQuery();
                    }
                }

                TempData["RegisterSuccess"] = "Đăng ký tài khoản thành công!";
                return RedirectToAction("RegisterSuccess");
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                    ModelState.AddModelError("Email", "Email đã tồn tại.");
                else
                    ModelState.AddModelError("", "Không thể lưu tài khoản.");

                return View(model);
            }
        }

        [HttpGet]
        public ActionResult RegisterSuccess()
        {
            ViewBag.Message = TempData["RegisterSuccess"];
            return View();
        }

        // ================= ĐĂNG NHẬP =================

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["CustomerId"] != null)
            {
                if (Session["IsAdmin"] != null &&
                    Convert.ToBoolean(Session["IsAdmin"]))
                {
                    return RedirectToAction("Index", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email ?? "";

            if (email.Any(char.IsWhiteSpace))
            {
                ModelState.AddModelError("Email",
                    "Email không được chứa dấu cách.");
                return View(model);
            }

            email = email.ToLowerInvariant();

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    string sql = @"
                        SELECT CustomerId, FullName, Email,
                               PasswordHash, IsAdmin
                        FROM dbo.Customers
                        WHERE Email = @Email AND IsActive = 1";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@Email", SqlDbType.NVarChar, 254)
                            .Value = email;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                ModelState.AddModelError("",
                                    "Email hoặc mật khẩu không chính xác.");
                                return View(model);
                            }

                            int customerId =
                                Convert.ToInt32(reader["CustomerId"]);

                            string fullName =
                                reader["FullName"].ToString();

                            string savedEmail =
                                reader["Email"].ToString();

                            string savedHash =
                                reader["PasswordHash"].ToString();

                            bool isAdmin =
                                Convert.ToBoolean(reader["IsAdmin"]);

                            if (!VerifyPassword(model.Password, savedHash))
                            {
                                ModelState.AddModelError("",
                                    "Email hoặc mật khẩu không chính xác.");
                                return View(model);
                            }

                            Session.Clear();

                            Session["CustomerId"] = customerId;
                            Session["CustomerName"] = fullName;
                            Session["CustomerEmail"] = savedEmail;
                            Session["IsAdmin"] = isAdmin;

                            if (isAdmin)
                                return RedirectToAction("Index", "Admin");
                        }
                    }
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception)
            {
                ModelState.AddModelError("",
                    "Có lỗi kết nối Database. Vui lòng thử lại.");

                return View(model);
            }
        }

        // ================= ĐĂNG XUẤT =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }

        // ================= MÃ HÓA MẬT KHẨU =================

        private string HashPassword(string password)
        {
            byte[] salt = new byte[16];

            using (RandomNumberGenerator rng =
                RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            const int iterations = 150000;

            using (Rfc2898DeriveBytes derive =
                new Rfc2898DeriveBytes(password, salt, iterations))
            {
                byte[] hash = derive.GetBytes(32);

                return "PBKDF2$" + iterations + "$" +
                       Convert.ToBase64String(salt) + "$" +
                       Convert.ToBase64String(hash);
            }
        }

        private bool VerifyPassword(string password, string savedHash)
        {
            try
            {
                string[] parts = savedHash.Split('$');

                if (parts.Length != 4 || parts[0] != "PBKDF2")
                    return false;

                int iterations = int.Parse(parts[1]);

                if (iterations < 10000 || iterations > 1000000)
                    return false;

                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] originalHash =
                    Convert.FromBase64String(parts[3]);

                if (originalHash.Length != 32)
                    return false;

                using (Rfc2898DeriveBytes derive =
                    new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    byte[] newHash = derive.GetBytes(32);

                    int difference = 0;

                    for (int i = 0; i < originalHash.Length; i++)
                        difference |= originalHash[i] ^ newHash[i];

                    return difference == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

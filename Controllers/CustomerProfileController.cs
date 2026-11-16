
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class CustomerProfileController : Controller
    {
        private int? GetCustomerId()
        {
            if (Session["CustomerId"] == null)
                return null;

            int id;

            if (!int.TryParse(
                Session["CustomerId"].ToString(), out id))
                return null;

            return id;
        }

        [HttpGet]
        public ActionResult Index()
        {
            int? customerId = GetCustomerId();

            if (!customerId.HasValue)
                return RedirectToAction("Login", "Account");

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    string customerSql = @"
                        SELECT FullName, Phone, Email, Address
                        FROM dbo.Customers
                        WHERE CustomerId = @CustomerId
                          AND IsActive = 1";

                    using (SqlCommand command =
                        new SqlCommand(customerSql, connection))
                    {
                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                Session.Clear();
                                return RedirectToAction(
                                    "Login", "Account");
                            }

                            ViewBag.FullName =
                                reader["FullName"].ToString();

                            ViewBag.Phone =
                                reader["Phone"].ToString();

                            ViewBag.Email =
                                reader["Email"].ToString();

                            ViewBag.Address =
                                reader["Address"] == DBNull.Value
                                    ? ""
                                    : reader["Address"].ToString();
                        }
                    }

                    DataTable orders = new DataTable();

                    string ordersSql = @"
                        SELECT
                            OrderId,
                            CustomerName,
                            Phone,
                            Address,
                            TotalAmount,
                            OrderStatus,
                            CreatedAt,
                            PaymentMethod,
                            PaymentStatus,
                            PaidAt
                        FROM dbo.Orders
                        WHERE CustomerId = @CustomerId
                        ORDER BY CreatedAt DESC, OrderId DESC";

                    using (SqlCommand command =
                        new SqlCommand(ordersSql, connection))
                    {
                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        using (SqlDataAdapter adapter =
                            new SqlDataAdapter(command))
                        {
                            adapter.Fill(orders);
                        }
                    }

                    ViewBag.Orders = orders;

                    DataTable orderDetails = new DataTable();

                    string detailsSql = @"
                        SELECT
                            d.OrderId,
                            d.ProductId,
                            p.ProductName,
                            d.Quantity,
                            d.UnitPrice,
                            (d.Quantity * d.UnitPrice) AS LineTotal
                        FROM dbo.OrderDetails d
                        INNER JOIN dbo.Orders o
                            ON d.OrderId = o.OrderId
                        LEFT JOIN dbo.Products p
                            ON d.ProductId = p.ProductId
                        WHERE o.CustomerId = @CustomerId
                        ORDER BY d.OrderId DESC, d.OrderDetailId";

                    using (SqlCommand command =
                        new SqlCommand(detailsSql, connection))
                    {
                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        using (SqlDataAdapter adapter =
                            new SqlDataAdapter(command))
                        {
                            adapter.Fill(orderDetails);
                        }
                    }

                    ViewBag.OrderDetails = orderDetails;
                }

                return View();
            }
            catch (Exception)
            {
                TempData["ProfileError"] =
                    "Không thể tải thông tin tài khoản hoặc đơn hàng.";

                return View();
            }
        }

        // KHÁCH HÀNG HỦY ĐƠN HÀNG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int id)
        {
            int? customerId = GetCustomerId();

            if (!customerId.HasValue)
                return RedirectToAction("Login", "Account");

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    // Chỉ chủ đơn hàng được hủy.
                    // Chỉ được hủy khi đơn còn chờ xác nhận.
                    // Không cho hủy đơn đã thanh toán.
                    const string sql = @"
                        UPDATE dbo.Orders
                        SET OrderStatus = N'Đã hủy'
                        WHERE OrderId = @OrderId
                          AND CustomerId = @CustomerId
                          AND OrderStatus = N'Chờ xác nhận'
                          AND (
                              PaymentStatus IS NULL
                              OR PaymentStatus <> N'Đã thanh toán'
                          )";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(
                            "@OrderId", SqlDbType.Int
                        ).Value = id;

                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        int affected = command.ExecuteNonQuery();

                        if (affected == 0)
                        {
                            TempData["ProfileError"] =
                                "Không thể hủy đơn hàng. " +
                                "Đơn hàng đã được Admin xác nhận, " +
                                "đã thanh toán hoặc không thuộc tài khoản của bạn.";

                            return RedirectToAction("Index");
                        }
                    }
                }

                TempData["ProfileSuccess"] =
                    "Hủy đơn hàng thành công!";

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["ProfileError"] =
                    "Có lỗi xảy ra khi hủy đơn hàng. Vui lòng thử lại.";

                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(
            string fullName,
            string phone,
            string email,
            string address)
        {
            int? customerId = GetCustomerId();

            if (!customerId.HasValue)
                return RedirectToAction("Login", "Account");

            fullName = (fullName ?? "").Trim();
            phone = (phone ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();
            address = (address ?? "").Trim();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(email))
            {
                TempData["ProfileError"] =
                    "Vui lòng nhập đầy đủ thông tin.";

                return RedirectToAction("Index");
            }

            if (fullName.Length > 100 ||
                email.Length > 254 ||
                address.Length > 500)
            {
                TempData["ProfileError"] =
                    "Thông tin vượt quá độ dài cho phép.";

                return RedirectToAction("Index");
            }

            if (phone.Length != 10)
            {
                TempData["ProfileError"] =
                    "Số điện thoại phải có đúng 10 chữ số.";

                return RedirectToAction("Index");
            }

            foreach (char c in phone)
            {
                if (c < '0' || c > '9')
                {
                    TempData["ProfileError"] =
                        "Số điện thoại chỉ được chứa chữ số.";

                    return RedirectToAction("Index");
                }
            }

            if (email.Contains(" ") ||
                !email.Contains("@"))
            {
                TempData["ProfileError"] =
                    "Email không hợp lệ.";

                return RedirectToAction("Index");
            }

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    string checkSql = @"
                        SELECT COUNT(*)
                        FROM dbo.Customers
                        WHERE Email = @Email
                          AND CustomerId <> @CustomerId";

                    using (SqlCommand check =
                        new SqlCommand(checkSql, connection))
                    {
                        check.Parameters.Add(
                            "@Email", SqlDbType.NVarChar, 254
                        ).Value = email;

                        check.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        if (Convert.ToInt32(
                            check.ExecuteScalar()) > 0)
                        {
                            TempData["ProfileError"] =
                                "Email này đã được sử dụng.";

                            return RedirectToAction("Index");
                        }
                    }

                    string updateSql = @"
                        UPDATE dbo.Customers
                        SET
                            FullName = @FullName,
                            Phone = @Phone,
                            Email = @Email,
                            Address = @Address
                        WHERE CustomerId = @CustomerId
                          AND IsActive = 1";

                    using (SqlCommand command =
                        new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.Add(
                            "@FullName", SqlDbType.NVarChar, 100
                        ).Value = fullName;

                        command.Parameters.Add(
                            "@Phone", SqlDbType.VarChar, 10
                        ).Value = phone;

                        command.Parameters.Add(
                            "@Email", SqlDbType.NVarChar, 254
                        ).Value = email;

                        command.Parameters.Add(
                            "@Address", SqlDbType.NVarChar, 500
                        ).Value = address;

                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId.Value;

                        if (command.ExecuteNonQuery() == 0)
                        {
                            TempData["ProfileError"] =
                                "Không tìm thấy tài khoản hợp lệ.";

                            return RedirectToAction("Index");
                        }
                    }
                }

                Session["CustomerName"] = fullName;
                Session["CustomerEmail"] = email;

                TempData["ProfileSuccess"] =
                    "Cập nhật thông tin thành công!";

                return RedirectToAction("Index");
            }
            catch (SqlException ex)
            {
                TempData["ProfileError"] =
                    ex.Number == 2601 || ex.Number == 2627
                        ? "Email này đã được sử dụng."
                        : "Không thể cập nhật thông tin.";

                return RedirectToAction("Index");
            }
        }
    }
}

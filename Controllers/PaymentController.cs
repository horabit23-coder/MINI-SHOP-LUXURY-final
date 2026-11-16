
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class PaymentController : Controller
    {
        public ActionResult Index(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                return HttpNotFound();
            }

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    const string sql = @"
                        SELECT
                            OrderId,
                            CustomerName,
                            TotalAmount,
                            PaymentMethod,
                            PaymentStatus
                        FROM dbo.Orders
                        WHERE OrderId = @OrderId";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(
                            "@OrderId",
                            SqlDbType.Int
                        ).Value = id.Value;

                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return HttpNotFound();
                            }

                            string paymentMethod =
                                Convert.ToString(
                                    reader["PaymentMethod"]
                                );

                            if (!string.Equals(
                                paymentMethod,
                                "BANK",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                return RedirectToAction(
                                    "Index", "Home");
                            }

                            int orderId = Convert.ToInt32(
                                reader["OrderId"]
                            );

                            decimal amount = Convert.ToDecimal(
                                reader["TotalAmount"]
                            );

                            string paymentStatus =
                                Convert.ToString(
                                    reader["PaymentStatus"]
                                );

                            ViewBag.OrderId = orderId;
                            ViewBag.Amount = amount;
                            ViewBag.PaymentStatus = paymentStatus;

                            ViewBag.BankName = "BIDV";
                            ViewBag.BankAccount = "0363838578";
                            ViewBag.AccountName = "HO TIEN SY";

                            string description =
                                "MINISHOP DH" + orderId;

                            ViewBag.TransferContent = description;

                            string qrUrl =
                                "https://img.vietqr.io/image/" +
                                "BIDV-0363838578-compact2.png" +
                                "?amount=" +
                                amount.ToString(
                                    "0",
                                    System.Globalization.CultureInfo
                                        .InvariantCulture
                                ) +
                                "&addInfo=" +
                                Uri.EscapeDataString(description) +
                                "&accountName=" +
                                Uri.EscapeDataString("HO TIEN SY");

                            ViewBag.QrUrl = qrUrl;
                        }
                    }
                }

                return View();
            }
            catch (SqlException)
            {
                return new HttpStatusCodeResult(
                    503,
                    "Không thể tải thông tin thanh toán."
                );
            }
        }
    }
}

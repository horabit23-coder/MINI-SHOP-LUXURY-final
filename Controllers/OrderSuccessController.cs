
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class OrderSuccessController : Controller
    {
        public ActionResult Index()
        {
            if (Session["LastCreatedOrderId"] == null)
            {
                return RedirectToAction("Index", "Home");
            }

            int orderId;

            if (!int.TryParse(
                Convert.ToString(Session["LastCreatedOrderId"]),
                out orderId))
            {
                return RedirectToAction("Index", "Home");
            }

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string sql = @"
                    SELECT
                        OrderId,
                        CustomerName,
                        Phone,
                        Address,
                        TotalAmount,
                        PaymentMethod,
                        OrderStatus
                    FROM dbo.Orders
                    WHERE OrderId = @OrderId
                      AND PaymentMethod = 'COD'";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@OrderId",
                        SqlDbType.Int
                    ).Value = orderId;

                    using (SqlDataReader reader =
                        command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return RedirectToAction("Index", "Home");
                        }

                        ViewBag.OrderId = reader["OrderId"];
                        ViewBag.CustomerName = reader["CustomerName"];
                        ViewBag.Phone = reader["Phone"];
                        ViewBag.Address = reader["Address"];
                        ViewBag.TotalAmount = reader["TotalAmount"];
                        ViewBag.OrderStatus = reader["OrderStatus"];
                    }
                }
            }

            return View();
        }
    }
}

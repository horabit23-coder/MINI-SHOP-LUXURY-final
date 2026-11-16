
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class OrderAdminController : Controller
    {
        protected override void OnActionExecuting(
            ActionExecutingContext filterContext)
        {
            int customerId;

            if (Session["CustomerId"] == null ||
                !int.TryParse(
                    Convert.ToString(Session["CustomerId"]),
                    out customerId))
            {
                filterContext.Result =
                    RedirectToAction("Login", "Account");
                return;
            }

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    const string sql = @"
                        SELECT COUNT(*)
                        FROM dbo.Customers
                        WHERE CustomerId = @CustomerId
                          AND IsAdmin = 1
                          AND IsActive = 1";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(
                            "@CustomerId", SqlDbType.Int
                        ).Value = customerId;

                        if (Convert.ToInt32(
                            command.ExecuteScalar()) == 0)
                        {
                            filterContext.Result =
                                new HttpStatusCodeResult(403);
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                filterContext.Result =
                    new HttpStatusCodeResult(
                        503, "Không thể kiểm tra quyền Admin.");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index()
        {
            DataTable orders = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string sql = @"
                    SELECT
                        OrderId,
                        CustomerName,
                        Phone,
                        Address,
                        TotalAmount,
                        OrderStatus,
                        PaymentStatus,
                        CreatedAt
                    FROM dbo.Orders
                    ORDER BY CreatedAt DESC, OrderId DESC";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    adapter.Fill(orders);
                }
            }

            return View(orders);
        }

        public ActionResult Details(int id)
        {
            DataTable order = new DataTable();
            DataTable items = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string orderSql = @"
                    SELECT
                        OrderId,
                        CustomerName,
                        Phone,
                        Address,
                        TotalAmount,
                        OrderStatus,
                        PaymentMethod,
                        PaymentStatus,
                        CreatedAt
                    FROM dbo.Orders
                    WHERE OrderId = @Id";

                using (SqlCommand command =
                    new SqlCommand(orderSql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    using (SqlDataAdapter adapter =
                        new SqlDataAdapter(command))
                    {
                        adapter.Fill(order);
                    }
                }

                if (order.Rows.Count == 0)
                {
                    return HttpNotFound();
                }

                const string itemsSql = @"
                    SELECT
                        d.OrderDetailId,
                        p.ProductName,
                        d.Quantity,
                        d.UnitPrice,
                        d.Quantity * d.UnitPrice AS Subtotal
                    FROM dbo.OrderDetails d
                    INNER JOIN dbo.Products p
                        ON d.ProductId = p.ProductId
                    WHERE d.OrderId = @Id
                    ORDER BY d.OrderDetailId";

                using (SqlCommand command =
                    new SqlCommand(itemsSql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    using (SqlDataAdapter adapter =
                        new SqlDataAdapter(command))
                    {
                        adapter.Fill(items);
                    }
                }
            }

            ViewBag.Order = order.Rows[0];

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string orderStatus)
        {
            string[] allowedStatuses =
            {
                "Chờ xác nhận",
                "Đã xác nhận",
                "Đang giao",
                "Hoàn thành",
                "Đã hủy"
            };

            if (Array.IndexOf(allowedStatuses, orderStatus) < 0)
            {
                TempData["Error"] =
                    "Trạng thái đơn hàng không hợp lệ.";

                return RedirectToAction("Index");
            }

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                // Không cho hủy đơn đã thanh toán.
                // Đơn đã hủy cũng không được chuyển lại trạng thái
                // bằng chức năng cập nhật thông thường.
                const string sql = @"
                    UPDATE dbo.Orders
                    SET OrderStatus = @OrderStatus
                    WHERE OrderId = @Id
                      AND (
                          @OrderStatus <> N'Đã hủy'
                          OR ISNULL(PaymentStatus, N'')
                             <> N'Đã thanh toán'
                      )
                      AND (
                          ISNULL(OrderStatus, N'') <> N'Đã hủy'
                          OR @OrderStatus = N'Đã hủy'
                      )";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    command.Parameters.Add(
                        "@OrderStatus", SqlDbType.NVarChar, 50
                    ).Value = orderStatus;

                    int affected = command.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        TempData["Error"] =
                            "Không thể cập nhật. Đơn hàng không tồn tại, " +
                            "đã bị hủy hoặc đã thanh toán nên không thể hủy.";

                        return RedirectToAction("Index");
                    }
                }
            }

            TempData["Success"] =
                "Cập nhật trạng thái đơn hàng thành công.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(int id)
        {
            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                // Chỉ xác nhận đơn chưa thanh toán và chưa bị hủy.
                // Xác nhận lần thứ hai sẽ không cập nhật lại.
                const string sql = @"
                    UPDATE dbo.Orders
                    SET PaymentStatus = N'Đã thanh toán'
                    WHERE OrderId = @Id
                      AND ISNULL(OrderStatus, N'') <> N'Đã hủy'
                      AND ISNULL(PaymentStatus, N'')
                          <> N'Đã thanh toán'";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    int affected = command.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        TempData["Error"] =
                            "Không thể xác nhận. Đơn hàng có thể " +
                            "đã thanh toán hoặc đã bị hủy.";

                        return RedirectToAction(
                            "Details", new { id = id });
                    }
                }
            }

            TempData["Success"] =
                "Đã xác nhận khách hàng thanh toán thành công.";

            return RedirectToAction(
                "Details", new { id = id });
        }
    }
}

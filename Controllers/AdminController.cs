
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class AdminController : Controller
    {
        // Kiểm tra quyền Admin trước khi chạy Action
        protected override void OnActionExecuting(
            ActionExecutingContext filterContext)
        {
            if (Session["CustomerId"] == null)
            {
                filterContext.Result = RedirectToAction(
                    "Login", "Account");
                return;
            }

            int customerId;

            if (!int.TryParse(
                Convert.ToString(Session["CustomerId"]),
                out customerId))
            {
                Session.Clear();

                filterContext.Result = RedirectToAction(
                    "Login", "Account");
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
                            "@CustomerId",
                            SqlDbType.Int
                        ).Value = customerId;

                        bool isAdmin =
                            Convert.ToInt32(
                                command.ExecuteScalar()
                            ) > 0;

                        if (!isAdmin)
                        {
                            filterContext.Result =
                                new HttpStatusCodeResult(
                                    403,
                                    "Bạn không có quyền truy cập."
                                );

                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                filterContext.Result =
                    new HttpStatusCodeResult(
                        503,
                        "Không thể kiểm tra quyền Admin."
                    );

                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // Model dùng cho biểu đồ doanh thu
        public class MonthlyRevenue
        {
            public string Month { get; set; }
            public decimal Revenue { get; set; }
            public int OrderCount { get; set; }
        }

        // Model thống kê trạng thái đơn hàng
        public class OrderStatusStatistic
        {
            public string Status { get; set; }
            public int Count { get; set; }
        }

        // Model 5 đơn hàng mới nhất
        public class RecentOrder
        {
            public int OrderId { get; set; }
            public string CustomerName { get; set; }
            public decimal TotalAmount { get; set; }
            public string OrderStatus { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public ActionResult Index()
        {
            ViewBag.AdminName =
                Convert.ToString(Session["CustomerName"]);

            // Thống kê tổng quan
            ViewBag.TotalProducts = 0;
            ViewBag.TotalCategories = 0;
            ViewBag.TotalOrders = 0;
            ViewBag.TotalRevenue = 0m;
            ViewBag.UnconfirmedRevenue = 0m;
            ViewBag.PendingOrders = 0;

            // Thống kê nâng cao
            var monthlyRevenues =
                new List<MonthlyRevenue>();

            var orderStatuses =
                new List<OrderStatusStatistic>();

            var recentOrders =
                new List<RecentOrder>();

            // Tạo sẵn 12 tháng gần nhất
            DateTime currentMonth = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1
            );

            DateTime startMonth =
                currentMonth.AddMonths(-11);

            for (int i = 0; i < 12; i++)
            {
                DateTime month = startMonth.AddMonths(i);

                monthlyRevenues.Add(new MonthlyRevenue
                {
                    Month = month.ToString("MM/yyyy"),
                    Revenue = 0,
                    OrderCount = 0
                });
            }

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    // PHẦN 1: THỐNG KÊ TỔNG QUAN
                    const string overviewSql = @"
                        SELECT
                            (SELECT COUNT(*)
                             FROM dbo.Products)
                                AS TotalProducts,

                            (SELECT COUNT(*)
                             FROM dbo.Categories)
                                AS TotalCategories,

                            (SELECT COUNT(*)
                             FROM dbo.Orders)
                                AS TotalOrders,

                            (SELECT ISNULL(SUM(TotalAmount), 0)
                             FROM dbo.Orders
                             WHERE PaymentStatus = N'Đã thanh toán')
                                AS TotalRevenue,

                            (SELECT ISNULL(SUM(TotalAmount), 0)
                             FROM dbo.Orders
                             WHERE (PaymentStatus IS NULL
                                    OR PaymentStatus <> N'Đã thanh toán')
                               AND (OrderStatus IS NULL
                                    OR OrderStatus <> N'Đã hủy'))
                                AS UnconfirmedRevenue,

                            (SELECT COUNT(*)
                             FROM dbo.Orders
                             WHERE OrderStatus = N'Chờ xác nhận')
                                AS PendingOrders";

                    using (SqlCommand command =
                        new SqlCommand(overviewSql, connection))
                    {
                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ViewBag.TotalProducts =
                                    Convert.ToInt32(
                                        reader["TotalProducts"]
                                    );

                                ViewBag.TotalCategories =
                                    Convert.ToInt32(
                                        reader["TotalCategories"]
                                    );

                                ViewBag.TotalOrders =
                                    Convert.ToInt32(
                                        reader["TotalOrders"]
                                    );

                                ViewBag.TotalRevenue =
                                    Convert.ToDecimal(
                                        reader["TotalRevenue"]
                                    );

                                ViewBag.UnconfirmedRevenue =
                                    Convert.ToDecimal(
                                        reader["UnconfirmedRevenue"]
                                    );

                                ViewBag.PendingOrders =
                                    Convert.ToInt32(
                                        reader["PendingOrders"]
                                    );
                            }
                        }
                    }

                    // PHẦN 2: DOANH THU 12 THÁNG
                    // Chỉ tính đơn đã thanh toán
                    const string monthlySql = @"
                        SELECT
                            YEAR(CreatedAt) AS OrderYear,
                            MONTH(CreatedAt) AS OrderMonth,
                            COUNT(*) AS OrderCount,
                            ISNULL(SUM(TotalAmount), 0)
                                AS Revenue
                        FROM dbo.Orders
                        WHERE CreatedAt >= @StartDate
                          AND CreatedAt < @EndDate
                          AND PaymentStatus = N'Đã thanh toán'
                        GROUP BY
                            YEAR(CreatedAt),
                            MONTH(CreatedAt)
                        ORDER BY
                            OrderYear,
                            OrderMonth";

                    using (SqlCommand command =
                        new SqlCommand(monthlySql, connection))
                    {
                        command.Parameters.Add(
                            "@StartDate",
                            SqlDbType.DateTime
                        ).Value = startMonth;

                        command.Parameters.Add(
                            "@EndDate",
                            SqlDbType.DateTime
                        ).Value = currentMonth.AddMonths(1);

                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int year = Convert.ToInt32(
                                    reader["OrderYear"]
                                );

                                int month = Convert.ToInt32(
                                    reader["OrderMonth"]
                                );

                                string monthLabel =
                                    new DateTime(year, month, 1)
                                        .ToString("MM/yyyy");

                                MonthlyRevenue item =
                                    monthlyRevenues.Find(
                                        x => x.Month == monthLabel
                                    );

                                if (item != null)
                                {
                                    item.Revenue =
                                        Convert.ToDecimal(
                                            reader["Revenue"]
                                        );

                                    item.OrderCount =
                                        Convert.ToInt32(
                                            reader["OrderCount"]
                                        );
                                }
                            }
                        }
                    }

                    // PHẦN 3: THỐNG KÊ TRẠNG THÁI
                    const string statusSql = @"
                        SELECT
                            ISNULL(OrderStatus, N'Chưa xác định')
                                AS OrderStatus,
                            COUNT(*) AS Total
                        FROM dbo.Orders
                        GROUP BY OrderStatus
                        ORDER BY Total DESC";

                    using (SqlCommand command =
                        new SqlCommand(statusSql, connection))
                    {
                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                orderStatuses.Add(
                                    new OrderStatusStatistic
                                    {
                                        Status =
                                            Convert.ToString(
                                                reader["OrderStatus"]
                                            ),

                                        Count =
                                            Convert.ToInt32(
                                                reader["Total"]
                                            )
                                    }
                                );
                            }
                        }
                    }

                    // PHẦN 4: 5 ĐƠN HÀNG MỚI NHẤT
                    const string recentSql = @"
                        SELECT TOP 5
                            OrderId,
                            CustomerName,
                            TotalAmount,
                            OrderStatus,
                            CreatedAt
                        FROM dbo.Orders
                        ORDER BY
                            CreatedAt DESC,
                            OrderId DESC";

                    using (SqlCommand command =
                        new SqlCommand(recentSql, connection))
                    {
                        using (SqlDataReader reader =
                            command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                recentOrders.Add(new RecentOrder
                                {
                                    OrderId =
                                        Convert.ToInt32(
                                            reader["OrderId"]
                                        ),

                                    CustomerName =
                                        Convert.ToString(
                                            reader["CustomerName"]
                                        ),

                                    TotalAmount =
                                        Convert.ToDecimal(
                                            reader["TotalAmount"]
                                        ),

                                    OrderStatus =
                                        Convert.ToString(
                                            reader["OrderStatus"]
                                        ),

                                    CreatedAt =
                                        Convert.ToDateTime(
                                            reader["CreatedAt"]
                                        )
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                ViewBag.StatisticsError =
                    "Không thể tải đầy đủ thống kê từ SQL Server.";
            }

            ViewBag.MonthlyRevenues = monthlyRevenues;
            ViewBag.OrderStatuses = orderStatuses;
            ViewBag.RecentOrders = recentOrders;

            return View();
        }
    }
}

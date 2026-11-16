
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            bool isAdmin = false;
            int customerId;

            if (Session["CustomerId"] != null &&
                int.TryParse(
                    Convert.ToString(Session["CustomerId"]),
                    out customerId))
            {
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

                            isAdmin =
                                Convert.ToInt32(
                                    command.ExecuteScalar()
                                ) > 0;
                        }
                    }
                }
                catch (SqlException)
                {
                    isAdmin = false;
                }
            }

            Session["IsAdmin"] = isAdmin;
            ViewBag.IsAdmin = isAdmin;

            DataTable categories = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string categorySql = @"
                    SELECT
                        CategoryId,
                        CategoryName
                    FROM dbo.Categories
                    WHERE IsActive = 1
                    ORDER BY CategoryId ASC";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(categorySql, connection))
                {
                    adapter.Fill(categories);
                }
            }

            ViewBag.Categories = categories;

            return View();
        }
    }
}

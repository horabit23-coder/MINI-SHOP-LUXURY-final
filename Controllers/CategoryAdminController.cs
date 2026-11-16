
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class CategoryAdminController : Controller
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

            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index()
        {
            DataTable categories = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string sql = @"
                    SELECT
                        c.CategoryId,
                        c.CategoryName,
                        c.IsActive,
                        COUNT(p.ProductId) AS ProductCount
                    FROM dbo.Categories c
                    LEFT JOIN dbo.Products p
                        ON c.CategoryId = p.CategoryId
                    GROUP BY
                        c.CategoryId,
                        c.CategoryName,
                        c.IsActive
                    ORDER BY c.CategoryId DESC";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    adapter.Fill(categories);
                }
            }

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] =
                    "Tên danh mục không được để trống.";
                return RedirectToAction("Index");
            }

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string sql = @"
                    INSERT INTO dbo.Categories
                        (CategoryName, IsActive)
                    VALUES (@CategoryName, 1)";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@CategoryName", SqlDbType.NVarChar, 100
                    ).Value = categoryName.Trim();

                    command.ExecuteNonQuery();
                }
            }

            TempData["Success"] = "Thêm danh mục thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] =
                    "Tên danh mục không được để trống.";
                return RedirectToAction("Index");
            }

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string sql = @"
                    UPDATE dbo.Categories
                    SET CategoryName = @CategoryName
                    WHERE CategoryId = @Id";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    command.Parameters.Add(
                        "@CategoryName", SqlDbType.NVarChar, 100
                    ).Value = categoryName.Trim();

                    command.ExecuteNonQuery();
                }
            }

            TempData["Success"] = "Sửa danh mục thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string checkSql = @"
                    SELECT COUNT(*)
                    FROM dbo.Products
                    WHERE CategoryId = @Id";

                using (SqlCommand check =
                    new SqlCommand(checkSql, connection))
                {
                    check.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    int productCount =
                        Convert.ToInt32(check.ExecuteScalar());

                    if (productCount > 0)
                    {
                        TempData["Error"] =
                            "Danh mục đang có sản phẩm. " +
                            "Không thể xóa danh mục này.";

                        return RedirectToAction("Index");
                    }
                }

                const string deleteSql = @"
                    DELETE FROM dbo.Categories
                    WHERE CategoryId = @Id";

                using (SqlCommand command =
                    new SqlCommand(deleteSql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = id;

                    command.ExecuteNonQuery();
                }
            }

            TempData["Success"] = "Xóa danh mục thành công.";
            return RedirectToAction("Index");
        }
    }
}

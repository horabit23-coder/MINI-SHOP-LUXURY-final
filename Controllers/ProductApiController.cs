
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class ProductApiController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            var products = new List<object>();

            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string sql = @"
                    SELECT
                        p.ProductId,
                        p.ProductName,
                        c.CategoryName,
                        p.Price,
                        p.StockQuantity,
                        p.ImageUrl
                    FROM dbo.Products p
                    INNER JOIN dbo.Categories c
                        ON p.CategoryId = c.CategoryId
                    WHERE p.IsActive = 1
                      AND c.IsActive = 1
                    ORDER BY p.ProductId";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader =
                        command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string imageUrl = "";

                            if (reader["ImageUrl"] != DBNull.Value)
                            {
                                imageUrl = Convert.ToString(
                                    reader["ImageUrl"]).Trim();
                            }

                            if (!string.IsNullOrWhiteSpace(imageUrl))
                            {
                                if (imageUrl.StartsWith("~/"))
                                {
                                    imageUrl = Url.Content(imageUrl);
                                }
                                else if (
                                    !imageUrl.StartsWith("/") &&
                                    !imageUrl.StartsWith("http://",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    !imageUrl.StartsWith("https://",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    imageUrl = Url.Content(
                                        "~/Content/images san_pham/" +
                                        imageUrl);
                                }
                            }

                            products.Add(new
                            {
                                id = Convert.ToInt32(
                                    reader["ProductId"]),

                                name = Convert.ToString(
                                    reader["ProductName"]),

                                category = Convert.ToString(
                                    reader["CategoryName"]),

                                price = Convert.ToDecimal(
                                    reader["Price"]),

                                stock = Convert.ToInt32(
                                    reader["StockQuantity"]),

                                image = imageUrl
                            });
                        }
                    }
                }
            }

            return Json(
                products,
                JsonRequestBehavior.AllowGet
            );
        }
    }
}

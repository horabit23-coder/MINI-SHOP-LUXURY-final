
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class ProductAdminController : Controller
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
            catch (SqlException)
            {
                filterContext.Result =
                    new HttpStatusCodeResult(503);
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index()
        {
            DataTable products = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string sql = @"
                    SELECT p.ProductId,
                           p.ProductName,
                           c.CategoryName,
                           p.Price,
                           p.StockQuantity,
                           p.ImageUrl,
                           p.IsActive
                    FROM dbo.Products p
                    INNER JOIN dbo.Categories c
                        ON p.CategoryId = c.CategoryId
                    ORDER BY p.ProductId DESC";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    adapter.Fill(products);
                }
            }

            return View(products);
        }

        [HttpGet]
        public ActionResult Create()
        {
            LoadCategories();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            string productName,
            int categoryId,
            decimal price,
            int stockQuantity,
            string imageUrl,
            string description,
            HttpPostedFileBase imageFile)
        {
            if (string.IsNullOrWhiteSpace(productName) ||
                price < 0 || stockQuantity < 0)
            {
                ModelState.AddModelError(
                    "", "Thông tin sản phẩm không hợp lệ.");

                LoadCategories();
                return View();
            }

            string savedImage = null;

            try
            {
                if (imageFile != null &&
                    imageFile.ContentLength > 0)
                {
                    savedImage = SaveImage(imageFile);
                    imageUrl = savedImage;
                }

                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    const string sql = @"
                        INSERT INTO dbo.Products
                        (ProductName, CategoryId, Price,
                         StockQuantity, ImageUrl, Description)
                        VALUES
                        (@ProductName, @CategoryId, @Price,
                         @StockQuantity, @ImageUrl, @Description)";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        AddProductParameters(
                            command,
                            productName,
                            categoryId,
                            price,
                            stockQuantity,
                            imageUrl,
                            description);

                        command.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                DeleteUploadedFile(savedImage);

                ModelState.AddModelError("", ex.Message);
                LoadCategories();
                return View();
            }
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            DataTable product = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string sql = @"
                    SELECT *
                    FROM dbo.Products
                    WHERE ProductId = @Id";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int).Value = id;

                    using (SqlDataAdapter adapter =
                        new SqlDataAdapter(command))
                    {
                        adapter.Fill(product);
                    }
                }
            }

            if (product.Rows.Count == 0)
                return HttpNotFound();

            LoadCategories();

            return View(product.Rows[0]);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(
            int id,
            string productName,
            int categoryId,
            decimal price,
            int stockQuantity,
            string imageUrl,
            string description,
            HttpPostedFileBase imageFile)
        {
            if (string.IsNullOrWhiteSpace(productName) ||
                price < 0 || stockQuantity < 0)
            {
                ModelState.AddModelError(
                    "", "Thông tin sản phẩm không hợp lệ.");

                return Edit(id);
            }

            string savedImage = null;

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    string oldImageUrl = "";

                    const string selectSql = @"
                        SELECT ImageUrl
                        FROM dbo.Products
                        WHERE ProductId = @Id";

                    using (SqlCommand selectCommand =
                        new SqlCommand(selectSql, connection))
                    {
                        selectCommand.Parameters.Add(
                            "@Id", SqlDbType.Int).Value = id;

                        object result =
                            selectCommand.ExecuteScalar();

                        if (result == null)
                            return HttpNotFound();

                        if (result != DBNull.Value)
                        {
                            oldImageUrl =
                                Convert.ToString(result);
                        }
                    }

                    imageUrl = oldImageUrl;

                    if (imageFile != null &&
                        imageFile.ContentLength > 0)
                    {
                        savedImage = SaveImage(imageFile);
                        imageUrl = savedImage;
                    }

                    const string updateSql = @"
                        UPDATE dbo.Products
                        SET ProductName = @ProductName,
                            CategoryId = @CategoryId,
                            Price = @Price,
                            StockQuantity = @StockQuantity,
                            ImageUrl = @ImageUrl,
                            Description = @Description
                        WHERE ProductId = @Id";

                    using (SqlCommand command =
                        new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.Add(
                            "@Id", SqlDbType.Int).Value = id;

                        AddProductParameters(
                            command,
                            productName,
                            categoryId,
                            price,
                            stockQuantity,
                            imageUrl,
                            description);

                        command.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                DeleteUploadedFile(savedImage);

                ModelState.AddModelError("", ex.Message);
                return Edit(id);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (SqlConnection connection = Db.GetConnection())
            {
                connection.Open();

                const string sql = @"
                    DELETE FROM dbo.Products
                    WHERE ProductId = @Id";

                using (SqlCommand command =
                    new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@Id", SqlDbType.Int).Value = id;

                    command.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Index");
        }

        private string SaveImage(
            HttpPostedFileBase imageFile)
        {
            if (imageFile == null ||
                imageFile.ContentLength <= 0)
            {
                return null;
            }

            const int maxSize = 5 * 1024 * 1024;

            if (imageFile.ContentLength > maxSize)
            {
                throw new InvalidOperationException(
                    "Ảnh không được lớn hơn 5MB.");
            }

            string extension = Path.GetExtension(
                imageFile.FileName).ToLowerInvariant();

            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png" &&
                extension != ".webp")
            {
                throw new InvalidOperationException(
                    "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");
            }

            string folder = Server.MapPath(
                "~/Content/images san_pham/");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string fileName =
                Guid.NewGuid().ToString("N") + extension;

            string fullPath =
                Path.Combine(folder, fileName);

            try
            {
                using (FileStream output =
                    new FileStream(
                        fullPath,
                        FileMode.CreateNew,
                        FileAccess.Write))
                {
                    Stream input = imageFile.InputStream;

                    if (input.CanSeek)
                    {
                        input.Position = 0;
                    }

                    input.CopyTo(output);
                    output.Flush();
                }

                FileInfo savedFile =
                    new FileInfo(fullPath);

                if (!savedFile.Exists ||
                    savedFile.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Ảnh tải lên bị rỗng. Vui lòng chọn lại ảnh.");
                }

                return fileName;
            }
            catch
            {
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }

                throw;
            }
        }

        private void DeleteUploadedFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            string safeFileName =
                Path.GetFileName(fileName);

            string fullPath = Server.MapPath(
                "~/Content/images san_pham/" +
                safeFileName);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private void LoadCategories()
        {
            DataTable categories = new DataTable();

            using (SqlConnection connection = Db.GetConnection())
            {
                const string sql = @"
                    SELECT CategoryId, CategoryName
                    FROM dbo.Categories
                    WHERE IsActive = 1
                    ORDER BY CategoryId";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    adapter.Fill(categories);
                }
            }

            ViewBag.Categories = categories;
        }

        private void AddProductParameters(
            SqlCommand command,
            string name,
            int categoryId,
            decimal price,
            int stock,
            string image,
            string description)
        {
            command.Parameters.Add(
                "@ProductName",
                SqlDbType.NVarChar,
                200).Value = name.Trim();

            command.Parameters.Add(
                "@CategoryId",
                SqlDbType.Int).Value = categoryId;

            SqlParameter priceParameter =
                command.Parameters.Add(
                    "@Price", SqlDbType.Decimal);

            priceParameter.Precision = 18;
            priceParameter.Scale = 2;
            priceParameter.Value = price;

            command.Parameters.Add(
                "@StockQuantity",
                SqlDbType.Int).Value = stock;

            command.Parameters.Add(
                "@ImageUrl",
                SqlDbType.NVarChar,
                500).Value =
                    string.IsNullOrWhiteSpace(image)
                    ? (object)DBNull.Value
                    : image.Trim();

            command.Parameters.Add(
                "@Description",
                SqlDbType.NVarChar,
                -1).Value =
                    string.IsNullOrWhiteSpace(description)
                    ? (object)DBNull.Value
                    : description;
        }
    }
}

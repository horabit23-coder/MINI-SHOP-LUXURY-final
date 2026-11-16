
using System.Data.SqlClient;

namespace MINI_SHOP_LUXURY.Models
{
    public static class Db
    {
        private static readonly string connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;
              Initial Catalog=MINI_SHOP_LUXURY;
              Integrated Security=True;
              Connect Timeout=30;";

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(connectionString);
        }
    }
}

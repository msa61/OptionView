using System;
using System.Data.SQLite;
using System.IO;

namespace OptionView.Tests
{
    // Points App.ConnStr at a throwaway transactions.sqlite in a temp folder.
    // App.OpenConnection() looks for "transactions.sqlite" in the current directory
    // (and shows a MessageBox if it's missing), so the working directory is switched too.
    internal sealed class TestDatabase : IDisposable
    {
        private readonly string savedDirectory;
        private readonly string folder;

        public TestDatabase()
        {
            savedDirectory = Environment.CurrentDirectory;
            folder = Path.Combine(Path.GetTempPath(), "OptionView.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Environment.CurrentDirectory = folder;

            SQLiteConnection.CreateFile("transactions.sqlite");
            App.ConnStr = null;
            App.OpenConnection();

            Execute("CREATE TABLE Config (Prop TEXT, Value TEXT)");
            Execute("CREATE TABLE Transactions (ID INTEGER PRIMARY KEY AUTOINCREMENT, Time TEXT, TransType TEXT, TransSubType TEXT, Symbol TEXT, " +
                    "[Open-Close] TEXT, Quantity INTEGER, ExpireDate TEXT, Strike REAL, Type TEXT, Price REAL, Fees REAL, Amount REAL, " +
                    "Description TEXT, Account TEXT, TransGroupID INTEGER, UnderlyingPrice REAL, twSymbol TEXT)");
        }

        public void Execute(string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr))
                cmd.ExecuteNonQuery();
        }

        public void AddTransaction(int group, string time, string subType, string symbol, string type, string expDate, double? strike,
                                   int quantity, double amount, double? underlying = null)
        {
            string sql = "INSERT INTO Transactions (TransGroupID, Time, TransSubType, Symbol, Type, ExpireDate, Strike, Quantity, Amount, UnderlyingPrice) " +
                         "VALUES (@g, @t, @st, @sy, @ty, @ex, @k, @q, @a, @u)";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr))
            {
                cmd.Parameters.AddWithValue("g", group);
                cmd.Parameters.AddWithValue("t", time);
                cmd.Parameters.AddWithValue("st", subType);
                cmd.Parameters.AddWithValue("sy", symbol);
                cmd.Parameters.AddWithValue("ty", type);
                cmd.Parameters.AddWithValue("ex", (object)expDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("k", (object)strike ?? DBNull.Value);
                cmd.Parameters.AddWithValue("q", quantity);
                cmd.Parameters.AddWithValue("a", amount);
                cmd.Parameters.AddWithValue("u", (object)underlying ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            App.ConnStr?.Close();
            App.ConnStr?.Dispose();
            App.ConnStr = null;
            SQLiteConnection.ClearAllPools();
            Environment.CurrentDirectory = savedDirectory;

            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

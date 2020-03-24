using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Diagnostics;
using System.Security.Cryptography;

namespace OptionView
{

    public class Config
    {

        public static bool SetProp(string prop, string value)
        {
            bool ret = false;

            try
            {
                // establish connection
                App.OpenConnection();

                SQLiteCommand cmd = new SQLiteCommand("SELECT Count(*) FROM config WHERE prop = @p", App.ConnStr);
                cmd.Parameters.AddWithValue("p", prop);
                int rows = Convert.ToInt32(cmd.ExecuteScalar());

                if (rows == 0)
                {
                    // insert
                    string sql = "INSERT INTO config(prop, value) Values (@p,@v)";
                    cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("p", prop);
                    cmd.Parameters.AddWithValue("v", value);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    // update
                    string sql = "UPDATE config SET value = @v WHERE prop=@p";
                    cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("p", prop);
                    cmd.Parameters.AddWithValue("v", value);
                    cmd.ExecuteNonQuery();
                }

                ret = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR SetProp: " + ex.Message);
            }
            return ret;
        }


        public static string GetProp(string prop)
        {
            string ret = "";
            try
            {
                // establish connection
                App.OpenConnection();

                SQLiteCommand cmd = new SQLiteCommand("SELECT value FROM config WHERE prop = @p", App.ConnStr);
                cmd.Parameters.AddWithValue("p", prop);
                SQLiteDataReader rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    ret = rdr[0].ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return ret;
        }
        public static DateTime GetDateProp(string prop)
        {
            DateTime ret = DateTime.MinValue;
            try
            {
                string value = GetProp(prop);
                if (value.Length > 0) ret = Convert.ToDateTime(value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return ret;
        }

        static byte[] entropy = { 9, 8, 7, 6, 5 };

        public static bool SetEncryptedProp(string prop, string value)
        {
            byte[] encryptedString = ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(value), entropy, DataProtectionScope.CurrentUser);
            string encodedString = Convert.ToBase64String(encryptedString);

            return SetProp(prop, encodedString);
        }

        public static string GetEncryptedProp(string prop)
        {
            string value = GetProp(prop);
            if (value.Length > 0)
            {
                byte[] decodedString = Convert.FromBase64String(value);
                byte[] decryptedString = ProtectedData.Unprotect(decodedString, entropy, DataProtectionScope.CurrentUser);

                return System.Text.Encoding.UTF8.GetString(decryptedString);
            }

            return "";
        }
    }
}

    


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.IO;
using System.Text.RegularExpressions;

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
                Console.WriteLine("ERROR SetProp: " + ex.Message);
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
                Console.WriteLine(ex.Message);
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

        public static bool SetEncryptedProp(string prop, string value)
        {
            string encodedString = "";

            AesManaged aes = new AesManaged();
            ICryptoTransform encryptor = aes.CreateEncryptor();

            // Create the streams used for encryption.
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        //Write all data to the stream.
                        swEncrypt.Write(value);
                    }
                    encodedString = Convert.ToBase64String(msEncrypt.ToArray());
                }
            }

            encodedString += "-" + Convert.ToBase64String(aes.Key) + "-" + Convert.ToBase64String(aes.IV);

            return SetProp(prop, encodedString);
        }

        public static string GetEncryptedProp(string prop)
        {
            string value = GetProp(prop);
            if (value.Length > 0)
            {
                string pattern = @"(.+)\-(.+)\-(.+)";
                string[] subs = Regex.Split(value, pattern);

                byte[] decodedString = Convert.FromBase64String(subs[1]);
                byte[] key = Convert.FromBase64String(subs[2]);
                byte[] iv = Convert.FromBase64String(subs[3]);

                AesManaged aes = new AesManaged();

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aes.CreateDecryptor(key, iv);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(decodedString))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return "";
        }
    }
}

    


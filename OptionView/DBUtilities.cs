using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace OptionView
{
    public class DBUtilities
    {

        public static int GetMax(string sql)
        {
            int ret = 0;

            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);

            var maxVal = cmd.ExecuteScalar();
            if ((maxVal != DBNull.Value) && (maxVal.GetType() != typeof(string))) ret = Convert.ToInt32(maxVal);

            return ret;
        }
    }
}

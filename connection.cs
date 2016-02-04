using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Microsoft.Samples.Kinect.FaceBasics
{
    class connection
    {
        public MySqlConnection conection = new MySqlConnection("persistsecurityinfo=True;server=localhost;user=root;password=1234;database=kf_json");

        public void connect()
        {
            try
            {
                conection.Open();
            }
            catch (MySqlException ex)
            {
                string err = ex.ToString();
            }
        }
        public void desconectar()
        {
            conection.Close();
        }
    }
  }

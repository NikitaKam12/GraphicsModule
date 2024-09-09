using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsModule
{
    public static class ConnectionSettings
    {
        
        public static string ConnectionString { get; } = ConfigurationManager.ConnectionStrings["PostgresConnection"].ConnectionString;
    }
}

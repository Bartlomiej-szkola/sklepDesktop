using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace sklepDesktop
{
    public static class Config
    {
        public static string ServerIp { get; set; }= "192.168.1.21";
        public static string ServerPort { get; set; } = "8080";
        public static string StoreBackendUrl => $"http://{ServerIp}:{ServerPort}";
        public static string ZdroweZakupyUrl = "https://api.zdrowezakupy.org/api/2.0/product";
    }
}

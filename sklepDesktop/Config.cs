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
        public static string ServerIp = "172.31.114.133";
        public static string StoreBackendUrl = $"http://{ServerIp}:8080";
        public static string ZdroweZakupyUrl = "https://api.zdrowezakupy.org/api/2.0/product";
    }
}
